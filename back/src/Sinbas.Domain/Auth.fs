namespace Sinbas.Domain

open System
open System.Text.RegularExpressions

type NombreUsuario = private NombreUsuario of string
type PasswordHash = PasswordHash of string

// ─────────────────────────────────────────────────────────────
// Refresh Token — tipos de dominio puro para renovación
// silenciosa de sesión (F0.1 — MF-00-05)
//
// Principio de diseño: la pregunta "¿es válida mi sesión HTTP?"
// (autenticación en el borde) está SEPARADA de "¿es válida mi
// operación de negocio?" (dominio). El refresh token vive
// exclusivamente en la capa de autenticación.
//
// Seguridad:
// - Rotación obligatoria: cada uso del refresh token invalida
//   el anterior y emite uno nuevo.
// - Detección de robo: si un token ya reemplazado se presenta
//   de nuevo, se revoca toda la familia de tokens del usuario
//   (el atacante o el usuario legítimo perdió la carrera).
// - El token crudo NUNCA se persiste; solo su hash SHA-256.
// ─────────────────────────────────────────────────────────────

/// Hash SHA-256 del refresh token crudo — es lo único que se persiste.
/// El token en texto plano solo viaja en la cookie HttpOnly al cliente.
type TokenHash = TokenHash of string

/// Identificador técnico del refresh token en base de datos.
type RefreshTokenId = RefreshTokenId of Guid

/// Registro inmutable de un refresh token emitido.
/// Cada login o rotación crea una instancia nueva; la anterior
/// queda marcada con `ReemplazadoPor = Some nuevoId`.
type RefreshToken =
    { Id: RefreshTokenId
      UsuarioId: UsuarioId
      TokenHash: TokenHash
      ExpiraEn: DateTime
      Revocado: bool
      /// Apunta al token que lo reemplazó tras una rotación exitosa.
      /// Si es Some y alguien presenta este token, es una reutilización
      /// anómala (posible robo) — se debe revocar toda la familia.
      ReemplazadoPor: RefreshTokenId option
      CreadoEn: DateTime }

/// Errores específicos del ciclo de vida de refresh tokens.
/// Se mantienen separados de AuthError para evitar acoplar la
/// lógica de autenticación básica (login/password) con la de
/// renovación de sesión.
type RefreshTokenError =
    /// El token presentado no existe en la base de datos.
    | TokenNoEncontrado
    /// El token existe pero ya fue revocado explícitamente (logout o desactivación).
    | TokenRevocado
    /// El token existe pero su fecha de expiración ya pasó.
    | TokenExpirado
    /// El token ya fue reemplazado por otro (rotación previa) y alguien
    /// lo presenta de nuevo — señal de posible robo. Se debe revocar
    /// toda la familia de tokens del usuario como medida preventiva.
    | TokenReutilizado of UsuarioId

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
    | RefreshTokenInvalido of RefreshTokenError
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

// ─────────────────────────────────────────────────────────────
// Módulo de funciones puras sobre RefreshToken (F0.1)
//
// Estas funciones no tocan infraestructura — evalúan el estado
// de un RefreshToken ya cargado y devuelven Result con errores
// tipados que la capa de aplicación traduce a decisiones
// (emitir nuevo par, revocar familia, rechazar).
// ─────────────────────────────────────────────────────────────

module RefreshToken =

    /// Crea un nuevo RefreshToken. El `tokenHash` es el SHA-256
    /// del token crudo que se envía al cliente vía cookie HttpOnly.
    let crear (usuarioId: UsuarioId) (tokenHash: TokenHash) (duracion: TimeSpan) (ahora: DateTime) : RefreshToken =
        { Id = RefreshTokenId (Identidad.nuevo ())
          UsuarioId = usuarioId
          TokenHash = tokenHash
          ExpiraEn = ahora.Add(duracion)
          Revocado = false
          ReemplazadoPor = None
          CreadoEn = ahora }

    /// ¿El token aún no expiró respecto al instante dado?
    let estaVigente (ahora: DateTime) (rt: RefreshToken) : bool =
        not rt.Revocado && rt.ExpiraEn > ahora && rt.ReemplazadoPor.IsNone

    /// Evalúa si un refresh token es válido para ser usado en una rotación.
    /// Retorna Ok con el token si es válido, o Error con el motivo de rechazo.
    ///
    /// Caso crítico de seguridad — TokenReutilizado:
    /// Si `ReemplazadoPor` ya tiene valor, significa que este token ya fue
    /// rotado previamente y alguien lo está presentando de nuevo. Esto indica
    /// que o el atacante robó el token viejo y lo usa antes que el legítimo,
    /// o viceversa. La respuesta correcta es revocar TODOS los tokens del
    /// usuario (la capa de aplicación se encarga de eso al recibir este error).
    let validar (ahora: DateTime) (rt: RefreshToken) : Result<RefreshToken, RefreshTokenError> =
        if rt.Revocado then
            Error TokenRevocado
        elif rt.ReemplazadoPor.IsSome then
            // ⚠️ Posible robo: token ya fue rotado pero se presenta de nuevo.
            // La capa de aplicación debe revocar toda la familia del usuario.
            Error (TokenReutilizado rt.UsuarioId)
        elif rt.ExpiraEn <= ahora then
            Error TokenExpirado
        else
            Ok rt

    /// Marca un token como reemplazado por otro (rotación exitosa).
    /// El token viejo queda invalidado pero NO borrado — se mantiene
    /// para poder detectar reutilización posterior (theft detection).
    let marcarReemplazado (nuevoId: RefreshTokenId) (rt: RefreshToken) : RefreshToken =
        { rt with ReemplazadoPor = Some nuevoId }

    /// Marca un token como revocado explícitamente (logout o desactivación de cuenta).
    let revocar (rt: RefreshToken) : RefreshToken =
        { rt with Revocado = true }