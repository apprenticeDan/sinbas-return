namespace Sinbas.Application

open System
open Sinbas.Domain

type UsuarioConEmpleado =
    { Usuario: Usuario
      Empleado: Empleado }

type CreateUserCommand =
    { EmpleadoId: Guid option
      Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string
      CiComplemento: string option
      Telefono: string option
      Email: string option
      NombreUsuario: string
      Contrasena: string
      Roles: string [] }

type UpdateUserCommand =
    { UsuarioId: Guid
      Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string
      CiComplemento: string option
      Telefono: string option
      Email: string option
      NombreUsuario: string
      Roles: string []
      NuevaContrasena: string option }

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

type ListarUsuarios = unit -> Async<UsuarioConEmpleado list>

type UserListItem =
    { Id: Guid
      EmpleadoId: Guid
      NombreUsuario: string
      Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      NombreCompleto: string
      Ci: string
      CiNumero: string
      CiComplemento: string option
      Telefono: string option
      Email: string option
      Roles: string list
      Activo: bool }

module UserUseCase =

    let private aUserListItem (u: Usuario) (e: Empleado) : UserListItem =
        { Id = let (UsuarioId x) = Usuario.id u in x
          EmpleadoId = let (EmpleadoId x) = Usuario.empleadoId u in x
          NombreUsuario = Usuario.nombreUsuario u |> NombreUsuario.valor
          Nombres = e.Nombres
          ApellidoPaterno = e.ApellidoPaterno
          ApellidoMaterno = e.ApellidoMaterno
          NombreCompleto = e.NombreCompleto
          Ci = CI.formatear e.CI
          CiNumero = e.CI.Numero
          CiComplemento = e.CI.Complemento
          Telefono = e.Telefono
          Email = e.Email
          Roles = Usuario.roles u |> Set.toList |> List.map NombreRol.toString
          Activo = Usuario.activo u }

    let listarUsuarios (listar: ListarUsuarios) =
        async {
            let! usuarios = listar ()
            return usuarios |> List.map (fun x -> aUserListItem x.Usuario x.Empleado)
        }

    let obtenerUsuarioPorId buscarUsuarioConEmpleado (usuarioId: UsuarioId) =
        async {
            let! result = buscarUsuarioConEmpleado usuarioId
            match result with
            | Error e -> return Error e
            | Ok item -> return Ok (aUserListItem item.Usuario item.Empleado)
        }

    let crearUsuario buscarPorNombre guardarUsuarioYEmpleado hashPassword (cmd: CreateUserCommand) =
        async {
            match Usuario.validarNombreUsuario cmd.NombreUsuario with
            | Error e -> return Error e
            | Ok nombreUsuario ->
                let! existenteResult = buscarPorNombre nombreUsuario
                match existenteResult with
                | Ok _ ->
                    return Error (NombreUsuarioInvalido "El nombre de usuario ya está registrado")
                | Error _ ->
                    match CI.crear cmd.CiNumero cmd.CiComplemento with
                    | Error (CIInvalido msg) -> return Error (NombreUsuarioInvalido msg)
                    | Error err -> return Error (NombreUsuarioInvalido (sprintf "%A" err))
                    | Ok ci ->
                        match Empleado.crear cmd.Nombres cmd.ApellidoPaterno cmd.ApellidoMaterno ci cmd.Telefono cmd.Email with
                        | Error (ValorRequerido msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error (SimbolosNoPermitidos msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error (LetrasNoPermitidas msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error err -> return Error (NombreUsuarioInvalido (sprintf "%A" err))
                        | Ok nuevoEmpleado ->
                            let hash = hashPassword cmd.Contrasena
                            let parsedRoles =
                                cmd.Roles
                                |> Array.choose (fun r ->
                                    match NombreRol.fromString r with
                                    | Ok role -> Some role
                                    | Error _ -> None)
                                |> Set.ofArray

                            match Usuario.crear nuevoEmpleado.Id nombreUsuario hash parsedRoles with
                            | Error e -> return Error e
                            | Ok nuevoUsuario ->
                                return! guardarUsuarioYEmpleado nuevoUsuario nuevoEmpleado
        }

    let actualizarUsuario buscarUsuarioConEmpleado buscarPorNombre guardarUsuarioYEmpleado hashPassword (cmd: UpdateUserCommand) =
        async {
            let! usuarioResult = buscarUsuarioConEmpleado (UsuarioId cmd.UsuarioId)
            match usuarioResult with
            | Error e -> return Error e
            | Ok item ->
                let usuarioActual = item.Usuario
                let empleadoActual = item.Empleado

                let nombreActualStr = Usuario.nombreUsuario usuarioActual |> NombreUsuario.valor
                let nombreNuevoStr = cmd.NombreUsuario.Trim().ToLowerInvariant()

                let! validarNombreRes =
                    async {
                        if nombreActualStr <> nombreNuevoStr then
                            match Usuario.validarNombreUsuario cmd.NombreUsuario with
                            | Error e -> return Error e
                            | Ok nuevoNombre ->
                                let! existente = buscarPorNombre nuevoNombre
                                match existente with
                                | Ok _ -> return Error (NombreUsuarioInvalido "El nombre de usuario ya está registrado por otra cuenta")
                                | Error _ -> return Ok nuevoNombre
                        else
                            return Usuario.validarNombreUsuario cmd.NombreUsuario
                    }

                match validarNombreRes with
                | Error e -> return Error e
                | Ok nuevoNombreUsuario ->
                    match CI.crear cmd.CiNumero cmd.CiComplemento with
                    | Error (CIInvalido msg) -> return Error (NombreUsuarioInvalido msg)
                    | Error err -> return Error (NombreUsuarioInvalido (sprintf "%A" err))
                    | Ok ci ->
                        match Empleado.actualizar cmd.Nombres cmd.ApellidoPaterno cmd.ApellidoMaterno ci cmd.Telefono cmd.Email empleadoActual with
                        | Error (ValorRequerido msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error (SimbolosNoPermitidos msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error (LetrasNoPermitidas msg) -> return Error (NombreUsuarioInvalido msg)
                        | Error err -> return Error (NombreUsuarioInvalido (sprintf "%A" err))
                        | Ok empleadoActualizado ->
                            let parsedRoles =
                                cmd.Roles
                                |> Array.choose (fun r ->
                                    match NombreRol.fromString r with
                                    | Ok role -> Some role
                                    | Error _ -> None)
                                |> Array.toList

                            if parsedRoles.IsEmpty then
                                return Error (RolesRequeridos "El usuario debe tener al menos un rol")
                            else
                                let conRoles = Usuario.asignarRoles parsedRoles usuarioActual
                                let conNombre =
                                    Usuario.reconstruir
                                        (let (UsuarioId uid) = Usuario.id conRoles in uid)
                                        (let (EmpleadoId eid) = Usuario.empleadoId conRoles in eid)
                                        (NombreUsuario.valor nuevoNombreUsuario)
                                        (let (PasswordHash h) = Usuario.hash conRoles in h)
                                        (Usuario.roles conRoles)
                                        (Usuario.estado conRoles)

                                let conPassword =
                                    match cmd.NuevaContrasena with
                                    | Some p when not (String.IsNullOrWhiteSpace p) ->
                                        match Validacion.validarPassword p with
                                        | Error (ValorRequerido msg) -> Error (NombreUsuarioInvalido msg)
                                        | Error _ -> Error (NombreUsuarioInvalido "Contraseña no válida")
                                        | Ok pwd ->
                                            let nuevoHash = hashPassword pwd
                                            Ok (Usuario.cambiarHash nuevoHash conNombre)
                                    | _ -> Ok conNombre

                                match conPassword with
                                | Error e -> return Error e
                                | Ok usuarioFinal ->
                                    return! guardarUsuarioYEmpleado usuarioFinal empleadoActualizado
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

