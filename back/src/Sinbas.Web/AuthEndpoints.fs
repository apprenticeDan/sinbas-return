namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module AuthEndpoints =

    let private CookieName = "sinbas_refresh_token"
    let private CookiePath = "/api/auth"

    /// Configuración de Cookie segura para transportar el Refresh Token (MF-00-05)
    /// - HttpOnly: inaccesible desde JavaScript (mitigación XSS)
    /// - SameSite=Lax: compatible con frontend en puerto separado (5173 vs 8080)
    /// - Path=/api/auth: el navegador solo envía la cookie a endpoints de autenticación
    let private crearCookieOptions (duracionHoras: float) (isHttps: bool) : CookieOptions =
        CookieOptions(
            HttpOnly = true,
            Secure = isHttps,
            SameSite = SameSiteMode.Lax,
            Path = CookiePath,
            Expires = Nullable(DateTimeOffset.UtcNow.AddHours(duracionHoras))
        )

    /// Cabeceras estrictas anti-caché para prevenir que el historial o la caché
    /// del navegador retenga credenciales sensibles al presionar "Atrás".
    let private aplicarCabecerasNoCache (ctx: HttpContext) =
        ctx.Response.Headers["Cache-Control"] <- "no-store, no-cache, must-revalidate, max-age=0"
        ctx.Response.Headers["Pragma"] <- "no-cache"

    let mapAuthEndpoints (app: WebApplication) =

        let config = app.Configuration
        let secreto = defaultArg (Option.ofObj (config.["Jwt:Secret"])) "SuperSecretKeyForSinbasDevSecurityOnly"
        let emisor = defaultArg (Option.ofObj (config.["Jwt:Issuer"])) "Sinbas"
        let audiencia = defaultArg (Option.ofObj (config.["Jwt:Audience"])) "SinbasClient"
        
        // Access Token: 15 minutos (0.25 horas) para minimizar la ventana de exposición
        let expHorasStr = config.["Jwt:ExpiracionHoras"]
        let expHoras =
            match Double.TryParse(if isNull expHorasStr then "" else expHorasStr) with
            | true, v when v > 0.0 -> v
            | _ -> 0.25 // Default 15 min

        // Refresh Token: 12 horas por defecto
        let refreshExpHorasStr = config.["Jwt:RefreshTokenExpiracionHoras"]
        let refreshExpHoras =
            match Double.TryParse(if isNull refreshExpHorasStr then "" else refreshExpHorasStr) with
            | true, v when v > 0.0 -> v
            | _ -> 12.0

        let duracionRefresh = TimeSpan.FromHours(refreshExpHoras)
        let emitirAccessToken = JwtService.emitirToken secreto emisor audiencia expHoras
        let generarRefreshTokenRaw = JwtService.generarRefreshToken
        let calcularTokenHash = JwtService.calcularTokenHash

        let buscarUsuarioPorNombre = AuthRepository.buscarUsuarioPorNombre
        let buscarUsuarioPorId = AuthRepository.buscarUsuarioPorId
        let verificarHash = PasswordHasher.verify

        let guardarRefreshToken = AuthRepository.guardarRefreshToken
        let buscarRefreshTokenPorHash = AuthRepository.buscarRefreshTokenPorHash
        let marcarReemplazado = AuthRepository.marcarRefreshTokenReemplazado
        let revocarRefreshToken = AuthRepository.revocarRefreshToken
        let revocarFamiliaTokens = AuthRepository.revocarTodosLosTokensDeUsuario

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/login
        // ─────────────────────────────────────────────────────────────
        app.MapPost("/api/auth/login", Func<HttpContext, LoginCommand, Threading.Tasks.Task<IResult>>(fun ctx cmd ->
            async {
                aplicarCabecerasNoCache ctx
                let ahora = DateTime.UtcNow

                let! result =
                    AuthUseCase.login
                        buscarUsuarioPorNombre
                        verificarHash
                        emitirAccessToken
                        generarRefreshTokenRaw
                        calcularTokenHash
                        guardarRefreshToken
                        duracionRefresh
                        ahora
                        cmd

                match result with
                | Ok res ->
                    // Guardar Refresh Token en Cookie HttpOnly
                    let cookieOpts = crearCookieOptions refreshExpHoras ctx.Request.IsHttps
                    ctx.Response.Cookies.Append(CookieName, res.RefreshTokenRaw, cookieOpts)

                    // Retornar en JSON SOLO el access token (el refresh token no se expone en body)
                    return Results.Ok({|
                        token = res.Token
                        nombreUsuario = res.NombreUsuario
                        roles = res.Roles
                    |})

                | Error (err: AuthError) ->
                    match err with
                    | CredencialesInvalidas -> return Results.Unauthorized()
                    | UsuarioInactivo -> return Results.StatusCode(403)
                    | NombreUsuarioInvalido msg -> return Results.BadRequest({| error = msg |})
                    | NombreUsuarioExistente msg -> return Results.Conflict({| error = msg |})
                    | EmpleadoYaTieneUsuario msg -> return Results.Conflict({| error = msg |})
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | RefreshTokenInvalido _ -> return Results.Unauthorized()
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
            } |> Async.StartAsTask
        ))
            .WithName("Login")
            .WithTags("Autenticación")
        |> ignore

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/refresh (MF-00-05)
        // Renovación silenciosa con rotación criptográfica
        // ─────────────────────────────────────────────────────────────
        app.MapPost("/api/auth/refresh", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                aplicarCabecerasNoCache ctx

                let tokenRaw =
                    match ctx.Request.Cookies.TryGetValue(CookieName) with
                    | true, v when not (String.IsNullOrWhiteSpace v) -> v
                    | _ -> ""

                if String.IsNullOrWhiteSpace tokenRaw then
                    return Results.Unauthorized()
                else
                    let ahora = DateTime.UtcNow
                    let! result =
                        AuthUseCase.refrescarToken
                            buscarRefreshTokenPorHash
                            guardarRefreshToken
                            marcarReemplazado
                            revocarFamiliaTokens
                            buscarUsuarioPorId
                            emitirAccessToken
                            generarRefreshTokenRaw
                            calcularTokenHash
                            duracionRefresh
                            ahora
                            tokenRaw

                    match result with
                    | Ok res ->
                        // Rotación: actualizamos la cookie con el nuevo refresh token
                        let cookieOpts = crearCookieOptions refreshExpHoras ctx.Request.IsHttps
                        ctx.Response.Cookies.Append(CookieName, res.RefreshTokenRaw, cookieOpts)

                        return Results.Ok({|
                            token = res.Token
                            nombreUsuario = res.NombreUsuario
                            roles = res.Roles
                        |})

                    | Error err ->
                        // Si falla el refresco (expirado, revocado, o detección de robo),
                        // limpiamos la cookie en el navegador
                        let deleteOpts = CookieOptions(Path = CookiePath, HttpOnly = true, SameSite = SameSiteMode.Lax)
                        ctx.Response.Cookies.Delete(CookieName, deleteOpts)

                        match err with
                        | UsuarioInactivo -> return Results.StatusCode(403)
                        | _ -> return Results.Unauthorized()
            } |> Async.StartAsTask
        ))
            .WithName("RefreshToken")
            .WithTags("Autenticación")
        |> ignore

        // ─────────────────────────────────────────────────────────────
        // POST /api/auth/logout (MF-00-05)
        // Cierre de sesión explícito y expiración de cookies
        // ─────────────────────────────────────────────────────────────
        app.MapPost("/api/auth/logout", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                aplicarCabecerasNoCache ctx

                let tokenRaw =
                    match ctx.Request.Cookies.TryGetValue(CookieName) with
                    | true, v when not (String.IsNullOrWhiteSpace v) -> v
                    | _ -> ""

                if not (String.IsNullOrWhiteSpace tokenRaw) then
                    let! _ = AuthUseCase.logout buscarRefreshTokenPorHash revocarRefreshToken calcularTokenHash tokenRaw
                    ()

                // Expirar la cookie inmediatamente
                let deleteOpts = CookieOptions(Path = CookiePath, HttpOnly = true, SameSite = SameSiteMode.Lax)
                ctx.Response.Cookies.Delete(CookieName, deleteOpts)

                return Results.NoContent()
            } |> Async.StartAsTask
        ))
            .WithName("Logout")
            .WithTags("Autenticación")
        |> ignore
