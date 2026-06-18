namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module UserEndpoints =

    let mapUserEndpoints (app: WebApplication) =
        
        // POST /api/usuarios
        app.MapPost("/api/usuarios", Func<CreateUserCommand, Threading.Tasks.Task<IResult>>(fun cmd ->
            async {
                let buscarPorNombre = AuthRepository.buscarUsuarioPorNombre
                let guardarUsuario = AuthRepository.guardarUsuario
                let hashPassword = PasswordHasher.hash
                
                let! result = UserUseCase.crearUsuario buscarPorNombre guardarUsuario hashPassword cmd
                match result with
                | Ok () -> return Results.StatusCode(201)
                | Error (err: AuthError) ->
                    match err with
                    | NombreUsuarioInvalido msg -> return Results.BadRequest({| error = msg |})
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
                    | _ -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .WithName("CrearUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/roles
        app.MapPut("/api/usuarios/{id}/roles", Func<int, AssignRolesCommand, Threading.Tasks.Task<IResult>>(fun id cmd ->
            async {
                let buscarPorId = AuthRepository.buscarUsuarioPorId
                let guardarUsuario = AuthRepository.guardarUsuario
                
                let command = { cmd with UsuarioId = id }
                let! result = UserUseCase.asignarRoles buscarPorId guardarUsuario command
                match result with
                | Ok () -> return Results.Ok()
                | Error (err: AuthError) ->
                    match err with
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
                    | _ -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .WithName("AsignarRoles")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/activar
        app.MapPut("/api/usuarios/{id}/activar", Func<int, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let buscarPorId = AuthRepository.buscarUsuarioPorId
                let guardarUsuario = AuthRepository.guardarUsuario
                
                let command : EnableUserCommand = { UsuarioId = id }
                let! result = UserUseCase.activarUsuario buscarPorId guardarUsuario command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .WithName("ActivarUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/desactivar
        app.MapPut("/api/usuarios/{id}/desactivar", Func<int, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let buscarPorId = AuthRepository.buscarUsuarioPorId
                let guardarUsuario = AuthRepository.guardarUsuario
                
                let command : DisableUserCommand = { UsuarioId = id }
                let! result = UserUseCase.desactivarUsuario buscarPorId guardarUsuario command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .WithName("DesactivarUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/password
        app.MapPut("/api/usuarios/{id}/password", Func<int, ChangePasswordCommand, Threading.Tasks.Task<IResult>>(fun id cmd ->
            async {
                let buscarPorId = AuthRepository.buscarUsuarioPorId
                let guardarUsuario = AuthRepository.guardarUsuario
                let hashPassword = PasswordHasher.hash
                
                let command = { cmd with UsuarioId = id }
                let! result = UserUseCase.cambiarContrasena buscarPorId guardarUsuario hashPassword command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .WithName("CambiarPassword")
            .WithTags("Usuarios")
        |> ignore
