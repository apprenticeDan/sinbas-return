namespace Sinbas.Application

open Sinbas.Domain

type CreateUserCommand =
    { EmpleadoId: int
      NombreUsuario: string
      Contrasena: string
      Roles: string list }

type AssignRolesCommand =
    { UsuarioId: int
      Roles: string list }

type ChangePasswordCommand =
    { UsuarioId: int
      NuevaContrasena: string }

type DisableUserCommand =
    { UsuarioId: int }

type EnableUserCommand =
    { UsuarioId: int }

module UserUseCase =

    let crearUsuario buscarPorNombre guardarUsuario hashPassword (cmd: CreateUserCommand) =
        async {
            match Usuario.validarNombreUsuario cmd.NombreUsuario with
            | Error e -> return Error e
            | Ok nombreUsuario ->
                let! existenteResult = buscarPorNombre nombreUsuario
                match existenteResult with
                | Ok _ ->
                    return Error (NombreUsuarioInvalido "El nombre de usuario ya está registrado")
                | Error _ ->
                    let hash = hashPassword cmd.Contrasena
                    let parsedRoles =
                        cmd.Roles
                        |> List.choose (fun r ->
                            match NombreRol.fromString r with
                            | Ok role -> Some role
                            | Error _ -> None)
                        |> Set.ofList

                    match Usuario.crear (EmpleadoId cmd.EmpleadoId) nombreUsuario hash parsedRoles with
                    | Error e -> return Error e
                    | Ok nuevoUsuario ->
                        return! guardarUsuario nuevoUsuario
        }

    let asignarRoles buscarPorId guardarUsuario (cmd: AssignRolesCommand) =
        async {
            let! usuarioResult = buscarPorId (UsuarioId cmd.UsuarioId)
            match usuarioResult with
            | Error e -> return Error e
            | Ok usuario ->
                let parsedRoles =
                    cmd.Roles
                    |> List.choose (fun r ->
                        match NombreRol.fromString r with
                        | Ok role -> Some role
                        | Error _ -> None)

                let usuarioActualizado = Usuario.asignarRoles parsedRoles usuario
                if Set.isEmpty (Usuario.roles usuarioActualizado) then
                    return Error (RolesRequeridos "El usuario debe tener al menos un rol")
                else
                    return! guardarUsuario usuarioActualizado
        }

    let cambiarContrasena buscarPorId guardarUsuario hashPassword (cmd: ChangePasswordCommand) =
        async {
            let! usuarioResult = buscarPorId (UsuarioId cmd.UsuarioId)
            match usuarioResult with
            | Error e -> return Error e
            | Ok usuario ->
                let nuevoHash = hashPassword cmd.NuevaContrasena
                let usuarioActualizado = Usuario.cambiarHash nuevoHash usuario
                return! guardarUsuario usuarioActualizado
        }

    let activarUsuario buscarPorId guardarUsuario (cmd: EnableUserCommand) =
        async {
            let! usuarioResult = buscarPorId (UsuarioId cmd.UsuarioId)
            match usuarioResult with
            | Error e -> return Error e
            | Ok usuario ->
                let usuarioActualizado = Usuario.activar usuario
                return! guardarUsuario usuarioActualizado
        }

    let desactivarUsuario buscarPorId guardarUsuario (cmd: DisableUserCommand) =
        async {
            let! usuarioResult = buscarPorId (UsuarioId cmd.UsuarioId)
            match usuarioResult with
            | Error e -> return Error e
            | Ok usuario ->
                let usuarioActualizado = Usuario.desactivar usuario
                return! guardarUsuario usuarioActualizado
        }
