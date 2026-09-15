namespace Sinbas.Domain

open System.Text.RegularExpressions

type NombreUsuario = private NombreUsuario of string
type PasswordHash = PasswordHash of string

type NombreRol =
    | Administrador
    | Gerencia
    | Comercial
    | Almacen
    | Laboratorio

module NombreRol =

    let toString =
        function
        | Administrador -> "Administrador"
        | Gerencia -> "Gerencia"
        | Comercial -> "Comercial"
        | Almacen -> "Almacen"
        | Laboratorio -> "Laboratorio"

    let fromString =
        function
        | "Administrador" -> Ok Administrador
        | "Gerencia" -> Ok Gerencia
        | "Comercial" -> Ok Comercial
        | "Almacen" -> Ok Almacen
        | "Laboratorio" -> Ok Laboratorio
        | x -> Error $"Rol desconocido: {x}"

type EstadoUsuario =
    | Activo
    | Desactivado

type Usuario =
    private
        { Id: UsuarioId
          EmpleadoId: EmpleadoId
          NombreUsuario: NombreUsuario
          Hash: PasswordHash
          Roles: Set<NombreRol>
          Estado: EstadoUsuario }

type AuthError =
    | CredencialesInvalidas
    | UsuarioInactivo
    | NombreUsuarioInvalido of string
    | NombreUsuarioExistente of string
    | EmpleadoYaTieneUsuario of EmpleadoId
    | RolesRequeridos of string
    | ErrorInterno of string

module NombreUsuario =

    let private regex = Regex(@"^[a-zA-Z0-9_.]{3,20}$")

    let crear raw =
        let t = (raw |> Option.ofObj |> Option.defaultValue "").Trim()

        if regex.IsMatch t then
            Ok(NombreUsuario(t.ToLowerInvariant()))
        else
            Error(NombreUsuarioInvalido raw)

    let valor (NombreUsuario n) = n

module Usuario =

    let validarNombreUsuario = NombreUsuario.crear

    let crear empleadoId nombreUsuario hash roles =
        if Set.isEmpty roles then
            Error (RolesRequeridos "El usuario debe tener al menos un rol asignado")
        else
            Ok { Id = UsuarioId (Identidad.nuevo ())
                 EmpleadoId = empleadoId
                 NombreUsuario = nombreUsuario
                 Hash = hash
                 Roles = roles
                 Estado = Activo }

    let reconstruir (id: System.Guid) (empleadoId: System.Guid) nombre hash roles estado =
        { Id = UsuarioId id
          EmpleadoId = EmpleadoId empleadoId
          NombreUsuario = NombreUsuario nombre
          Hash = PasswordHash hash
          Roles = roles
          Estado = estado }

    let id u = u.Id

    let empleadoId u = u.EmpleadoId

    let nombreUsuario u = u.NombreUsuario

    let hash u = u.Hash

    let roles u = u.Roles

    let estado u = u.Estado

    let activo u = u.Estado = Activo

    let puedeIniciarSesion u =
        u.Estado = Activo && not (Set.isEmpty u.Roles)

    let asignarRoles nuevosRoles (usuario: Usuario) =
        { usuario with Roles = Set.ofList nuevosRoles }

    let activar (usuario: Usuario) =
        { usuario with Estado = Activo }

    let desactivar (usuario: Usuario) =
        { usuario with Estado = Desactivado }

    let cambiarHash nuevoHash (usuario: Usuario) =
        { usuario with Hash = nuevoHash }

    let agregarRol rol (usuario: Usuario) =
        { usuario with Roles = usuario.Roles.Add rol }

    let quitarRol rol (usuario: Usuario) =
        { usuario with Roles = usuario.Roles.Remove rol }

    let tieneRol rol u =
        u.Roles.Contains rol

    let esAdministrador u =
        tieneRol Administrador u || tieneRol Gerencia u

    let puedeGestionarUsuarios u =
        esAdministrador u

    let puedeRegistrarAnalisis u =
        tieneRol Laboratorio u

    let puedePrepararPedidos u =
        tieneRol Almacen u

    let puedeGestionarVentas u =
        tieneRol Comercial u