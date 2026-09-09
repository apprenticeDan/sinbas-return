namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module UserEndpoints =

    let mapUserEndpoints (app: WebApplication) =
        
        let buscarPorId = AuthRepository.buscarUsuarioPorId
        let buscarPorNombre = AuthRepository.buscarUsuarioPorNombre
        let buscarUsuarioConEmpleado = AuthRepository.buscarUsuarioConEmpleadoPorId
        let guardarUsuario = AuthRepository.guardarUsuario
        let guardarUsuarioYEmpleado = AuthRepository.guardarUsuarioYEmpleado
        let hashPassword = PasswordHasher.hash
        let listarUsuarios = AuthRepository.listarUsuariosConEmpleado

        // GET /api/usuarios
        app.MapGet("/api/usuarios", Func<Threading.Tasks.Task<IResult>>(fun () ->
            async {
                let! usuarios = UserUseCase.listarUsuarios listarUsuarios
                return Results.Ok(usuarios)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("ListarUsuarios")
            .WithTags("Usuarios")
        |> ignore

        // GET /api/usuarios/{id}
        app.MapGet("/api/usuarios/{id}", Func<Guid, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let! result = UserUseCase.obtenerUsuarioPorId buscarUsuarioConEmpleado (UsuarioId id)
                match result with
                | Ok user -> return Results.Ok(user)
                | Error (ErrorInterno msg) -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
                | Error _ -> return Results.NotFound({| error = "Usuario no encontrado" |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("ObtenerUsuario")
            .WithTags("Usuarios")
        |> ignore

        // POST /api/usuarios
        app.MapPost("/api/usuarios", Func<CreateUserCommand, Threading.Tasks.Task<IResult>>(fun cmd ->
            async {
                printfn "[UserEndpoints] Petición POST /api/usuarios recibida: %+A" cmd
                let! result = UserUseCase.crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashPassword cmd
                match result with
                | Ok () ->
                    printfn "[UserEndpoints] Usuario creado exitosamente: %s" cmd.NombreUsuario
                    return Results.StatusCode(201)
                | Error (err: AuthError) ->
                    printfn "[UserEndpoints] Error al crear usuario: %+A" err
                    match err with
                    | NombreUsuarioInvalido msg -> return Results.BadRequest({| error = msg |})
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
                    | _ -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("CrearUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}
        app.MapPut("/api/usuarios/{id}", Func<Guid, UpdateUserCommand, Threading.Tasks.Task<IResult>>(fun id cmd ->
            async {
                let command = { cmd with UsuarioId = id }
                let! result = UserUseCase.actualizarUsuario buscarUsuarioConEmpleado buscarPorNombre guardarUsuarioYEmpleado hashPassword command
                match result with
                | Ok () -> return Results.Ok()
                | Error (err: AuthError) ->
                    match err with
                    | NombreUsuarioInvalido msg -> return Results.BadRequest({| error = msg |})
                    | RolesRequeridos msg -> return Results.BadRequest({| error = msg |})
                    | ErrorInterno msg -> return Results.Json({| error = msg |}, statusCode = Nullable 500)
                    | _ -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("ActualizarUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/roles
        app.MapPut("/api/usuarios/{id}/roles", Func<Guid, AssignRolesCommand, Threading.Tasks.Task<IResult>>(fun id cmd ->
            async {
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
            .RequireAuthorization("RequireAdmin")
            .WithName("AsignarRoles")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/activar
        app.MapPut("/api/usuarios/{id}/activar", Func<Guid, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let command : EnableUserCommand = { UsuarioId = id }
                let! result = UserUseCase.activarUsuario buscarPorId guardarUsuario command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("ActivarUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/desactivar
        app.MapPut("/api/usuarios/{id}/desactivar", Func<Guid, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let command : DisableUserCommand = { UsuarioId = id }
                let! result = UserUseCase.desactivarUsuario buscarPorId guardarUsuario command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("DesactivarUsuario")
            .WithTags("Usuarios")
        |> ignore

        // PUT /api/usuarios/{id}/password
        app.MapPut("/api/usuarios/{id}/password", Func<Guid, ChangePasswordCommand, Threading.Tasks.Task<IResult>>(fun id cmd ->
            async {
                let command = { cmd with UsuarioId = id }
                let! result = UserUseCase.cambiarContrasena buscarPorId guardarUsuario hashPassword command
                match result with
                | Ok () -> return Results.Ok()
                | Error err -> return Results.BadRequest({| error = sprintf "%A" err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAdmin")
            .WithName("CambiarPassword")
            .WithTags("Usuarios")
        |> ignore
