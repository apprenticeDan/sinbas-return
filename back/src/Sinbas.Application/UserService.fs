namespace Sinbas.Application

open System
open Sinbas.Domain

type CreateUserCommand =
    { EmpleadoId: Guid option
      NombreUsuario: string
      Contrasena: string
      Roles: string [] }

type AssignRolesCommand =
    { UsuarioId: Guid
      Roles: string [] }

type ChangePasswordCommand =
    { UsuarioId: Guid
      NuevaContrasena: string }

type DisableUserCommand =
    { UsuarioId: Guid }

type EnableUserCommand =
    { UsuarioId: Guid }

type ListarUsuarios = unit -> Async<Usuario list>

type UserListItem =
    { Id: Guid
      EmpleadoId: Guid
      NombreUsuario: string
      Roles: string list
      Activo: bool }

module UserUseCase =

    let private aUserListItem (u: Usuario) : UserListItem =
        { Id = let (UsuarioId x) = Usuario.id u in x
          EmpleadoId = let (EmpleadoId x) = Usuario.empleadoId u in x
          NombreUsuario = Usuario.nombreUsuario u |> NombreUsuario.valor
          Roles = Usuario.roles u |> Set.toList |> List.map NombreRol.toString
          Activo = Usuario.activo u }

    let listarUsuarios (listar: ListarUsuarios) =
        async {
            let! usuarios = listar ()
            return usuarios |> List.map aUserListItem
        }

    let obtenerUsuarioPorId buscarPorId (usuarioId: UsuarioId) =
        async {
            let! result = buscarPorId usuarioId
            match result with
            | Error e -> return Error e
            | Ok u -> return Ok (aUserListItem u)
        }

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
                        |> Array.choose (fun r ->
                            match NombreRol.fromString r with
                            | Ok role -> Some role
                            | Error _ -> None)
                        |> Set.ofArray

                    let empId =
                        match cmd.EmpleadoId with
                        | Some id -> EmpleadoId id
                        | None -> EmpleadoId (Identidad.nuevo ())

                    match Usuario.crear empId nombreUsuario hash parsedRoles with
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
                    |> Array.choose (fun r ->
                        match NombreRol.fromString r with
                        | Ok role -> Some role
                        | Error _ -> None)
                    |> Array.toList

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
