namespace Sinbas.Domain

open System.Text.RegularExpressions

type NombreUsuario = private NombreUsuario of string
type PasswordHash = PasswordHash of string

type NombreRol =
    | Administrador
    | Comercial
    | Almacen
    | Laboratorio
    | Asistente

module NombreRol =

    let toString =
        function
        | Administrador -> "Administrador"
        | Comercial -> "Comercial"
        | Almacen -> "Almacen"
        | Laboratorio -> "Laboratorio"
        | Asistente -> "Asistente"

    let fromString =
        function
        | "Administrador" -> Ok Administrador
        | "Comercial" -> Ok Comercial
        | "Almacen" -> Ok Almacen
        | "Laboratorio" -> Ok Laboratorio
        | "Asistente" -> Ok Asistente
        | x -> Error $"Rol desconocido: {x}"

type Rol = { Id: RolId; Nombre: NombreRol }

type Usuario =
    private
        { Id: UsuarioId
          EmpleadoId: EmpleadoId
          NombreUsuario: NombreUsuario
          Hash: PasswordHash
          Roles: Rol list
          Activo: bool }

type AuthError =
    | CredencialesInvalidas
    | UsuarioInactivo
    | NombreUsuarioInvalido of string
    | ErrorInterno of string

module Usuario =

    let private regex = Regex(@"^[a-zA-Z0-9_.]{3,20}$")

    let validarNombreUsuario raw =
        let t = (raw |> Option.ofObj |> Option.defaultValue "").Trim()

        if regex.IsMatch t then
            Ok(NombreUsuario(t.ToLowerInvariant()))
        else
            Error(NombreUsuarioInvalido raw)

    let reconstruir id empleadoId nombre hash roles activo =

        { Id = UsuarioId id
          EmpleadoId = EmpleadoId empleadoId
          NombreUsuario = NombreUsuario nombre
          Hash = PasswordHash hash
          Roles = roles
          Activo = activo }

    let id u = u.Id

    let nombreUsuario u = u.NombreUsuario

    let hash u = u.Hash

    let roles u = u.Roles

    let activo u = u.Activo

    let tieneRol rol u =
        u.Roles |> List.exists (fun r -> r.Nombre = rol)

module NombreUsuario =

    let valor (NombreUsuario n) = n