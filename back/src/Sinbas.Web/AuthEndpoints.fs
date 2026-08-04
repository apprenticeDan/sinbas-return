namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module AuthEndpoints =

    let mapAuthEndpoints (app: WebApplication) =
        app.MapPost("/api/auth/login", Func<LoginCommand, Threading.Tasks.Task<IResult>>(fun cmd ->
            async {
                let buscarUsuario = AuthRepository.buscarUsuarioPorNombre
                let verificarHash = PasswordHasher.verify
                
                let config = app.Configuration
                let secreto = defaultArg (Option.ofObj (config.["Jwt:Secret"])) "SuperSecretKeyForSinbasDevSecurityOnly"
                let emisor = defaultArg (Option.ofObj (config.["Jwt:Issuer"])) "Sinbas"
                let audiencia = defaultArg (Option.ofObj (config.["Jwt:Audience"])) "SinbasClient"
                let expHoras = 24.0
                
                let emitirToken = JwtService.emitirToken secreto emisor audiencia expHoras
                
                let! result = AuthUseCase.login buscarUsuario verificarHash emitirToken cmd
                match result with
                | Ok res -> return Results.Ok(res)
                | Error (err: AuthError) -> 
                    match err with
                    | CredencialesInvalidas -> return Results.Unauthorized()
                    | UsuarioInactivo -> return Results.StatusCode(403)
                    | NombreUsuarioInvalido msg -> return Results.BadRequest({| error = msg |})
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
            } |> Async.StartAsTask
        ))
            .WithName("Login")
            .WithTags("Autenticación")
        |> ignore
