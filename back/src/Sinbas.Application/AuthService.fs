namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Contratos funcionales para inyección de dependencias (F0.1)
// ─────────────────────────────────────────────────────────────

type BuscarUsuarioPorNombre = NombreUsuario -> Async<Result<Usuario, AuthError>>
type BuscarUsuarioPorId = UsuarioId -> Async<Result<Usuario, AuthError>>
type GuardarUsuario = Usuario -> Async<Result<unit, AuthError>>
type VerificarHash = string -> PasswordHash -> bool
type EmitirToken = Usuario -> string

/// Generador criptográfico de refresh token en texto plano (32 bytes aleatorios).
type GenerarRefreshTokenRaw = unit -> string

/// Función pura o de hash para derivar el TokenHash SHA-256 a persistir.
type CalcularTokenHash = string -> TokenHash

/// Persistencia de un nuevo refresh token en base de datos.
type GuardarRefreshToken = RefreshToken -> Async<Result<unit, AuthError>>

/// Búsqueda de refresh token por su hash SHA-256.
type BuscarRefreshTokenPorHash = TokenHash -> Async<Result<RefreshToken option, AuthError>>

/// Marca el token anterior como reemplazado tras una rotación exitosa.
type MarcarRefreshTokenReemplazado = RefreshTokenId -> RefreshTokenId -> Async<Result<unit, AuthError>>

/// Revoca un refresh token individual (logout explícito).
type RevocarRefreshToken = RefreshTokenId -> Async<Result<unit, AuthError>>

/// Revoca toda la familia de tokens de un usuario (detección de robo o desactivación RN15).
type RevocarFamiliaTokens = UsuarioId -> Async<Result<unit, AuthError>>

type LoginCommand =
    { NombreUsuarioRaw: string
      Contrasena: string }

/// Resultado de autenticación exitosa (login o refresco).
/// Nota de seguridad: 'RefreshTokenRaw' se utiliza EXCLUSIVAMENTE
/// en la capa Web para estampar la cookie HttpOnly; NUNCA debe
/// devolverse en el cuerpo JSON de la respuesta.
type LoginResult =
    { Token: string
      RefreshTokenRaw: string
      NombreUsuario: string
      Roles: string list }

module AuthUseCase =

    /// Caso de uso: Inicio de sesión con emisión de par de tokens (Access + Refresh).
    /// Si las credenciales son válidas y el usuario está activo:
    /// 1. Genera un Access Token JWT de corta duración (15 min).
    /// 2. Genera un Refresh Token criptográfico de alta entropía.
    /// 3. Guarda únicamente el hash SHA-256 del refresh token en BD.
    /// 4. Retorna el par de tokens junto a los roles del usuario.
    let login
        (buscarUsuario: BuscarUsuarioPorNombre)
        (verificarHash: VerificarHash)
        (emitirToken: EmitirToken)
        (generarRefreshToken: GenerarRefreshTokenRaw)
        (calcularTokenHash: CalcularTokenHash)
        (guardarRefreshToken: GuardarRefreshToken)
        (duracionRefreshToken: TimeSpan)
        (ahora: DateTime)
        (cmd: LoginCommand) : Async<Result<LoginResult, AuthError>> =

        async {
            match Usuario.validarNombreUsuario cmd.NombreUsuarioRaw with
            | Error e -> return Error e
            | Ok nombreUsuario ->
                let! usuarioResult = buscarUsuario nombreUsuario
                match usuarioResult with
                | Error e -> return Error e
                | Ok usuario ->
                    if not (Usuario.puedeIniciarSesion usuario) then
                        return Error UsuarioInactivo
                    else
                        match verificarHash cmd.Contrasena (Usuario.hash usuario) with
                        | false -> return Error CredencialesInvalidas
                        | true ->
                            // 1. Emitir Access Token JWT
                            let accessToken = emitirToken usuario

                            // 2. Generar y persistir Refresh Token
                            let rawRefreshToken = generarRefreshToken ()
                            let tokenHash = calcularTokenHash rawRefreshToken
                            let usuarioId = Usuario.id usuario
                            let rt = RefreshToken.crear usuarioId tokenHash duracionRefreshToken ahora

                            let! guardarResult = guardarRefreshToken rt
                            match guardarResult with
                            | Error e -> return Error e
                            | Ok () ->
                                let roles =
                                    usuario
                                    |> Usuario.roles
                                    |> Set.toList
                                    |> List.map NombreRol.toString

                                let nombre = Usuario.nombreUsuario usuario |> NombreUsuario.valor
                                return
                                    Ok
                                        { Token = accessToken
                                          RefreshTokenRaw = rawRefreshToken
                                          NombreUsuario = nombre
                                          Roles = roles }
        }

    /// Caso de uso: Renovación silenciosa de sesión (Rotación con Detección de Robo).
    /// Flujo de seguridad (MF-00-05):
    /// 1. Busca el token por el hash SHA-256 del token crudo recibido.
    /// 2. Valida vigencia, revocación y reutilización usando funciones puras del dominio.
    /// 3. SI SE DETECTA REUSO (TokenReutilizado): Señal de ataque o fuga. Revoca
    ///    INMEDIATAMENTE todos los refresh tokens del usuario y rechaza la petición.
    /// 4. Si el token es válido: genera un nuevo refresh token, lo persiste, marca el
    ///    anterior como reemplazado (enlace de familia), emite nuevo JWT y retorna el nuevo par.
    let refrescarToken
        (buscarRefreshToken: BuscarRefreshTokenPorHash)
        (guardarRefreshToken: GuardarRefreshToken)
        (marcarReemplazado: MarcarRefreshTokenReemplazado)
        (revocarFamilia: RevocarFamiliaTokens)
        (buscarUsuarioPorId: BuscarUsuarioPorId)
        (emitirToken: EmitirToken)
        (generarRefreshToken: GenerarRefreshTokenRaw)
        (calcularTokenHash: CalcularTokenHash)
        (duracionRefreshToken: TimeSpan)
        (ahora: DateTime)
        (tokenRaw: string) : Async<Result<LoginResult, AuthError>> =

        async {
            if String.IsNullOrWhiteSpace tokenRaw then
                return Error (RefreshTokenInvalido TokenNoEncontrado)
            else
                let tokenHash = calcularTokenHash tokenRaw
                let! busquedaResult = buscarRefreshToken tokenHash

                match busquedaResult with
                | Error e -> return Error e
                | Ok None -> return Error (RefreshTokenInvalido TokenNoEncontrado)
                | Ok (Some tokenActual) ->
                    // Validación en dominio puro
                    match RefreshToken.validar ahora tokenActual with
                    | Error (TokenReutilizado uid) ->
                        // ⚠️ DETECCIÓN DE ROBO: Un token ya rotado fue presentado de nuevo.
                        // Revocamos de forma preventiva toda la familia de tokens del usuario.
                        let! _ = revocarFamilia uid
                        return Error (RefreshTokenInvalido (TokenReutilizado uid))

                    | Error err ->
                        return Error (RefreshTokenInvalido err)

                    | Ok tokenValido ->
                        // Verificar vigencia y estado del usuario asociado
                        let! usuarioResult = buscarUsuarioPorId tokenValido.UsuarioId
                        match usuarioResult with
                        | Error e -> return Error e
                        | Ok usuario when not (Usuario.puedeIniciarSesion usuario) ->
                            return Error UsuarioInactivo
                        | Ok usuario ->
                            // Proceder con la rotación
                            let nuevoRaw = generarRefreshToken ()
                            let nuevoHash = calcularTokenHash nuevoRaw
                            let nuevoToken = RefreshToken.crear tokenValido.UsuarioId nuevoHash duracionRefreshToken ahora

                            let! gRes = guardarRefreshToken nuevoToken
                            match gRes with
                            | Error e -> return Error e
                            | Ok () ->
                                let! rRes = marcarReemplazado tokenValido.Id nuevoToken.Id
                                match rRes with
                                | Error e -> return Error e
                                | Ok () ->
                                    let nuevoAccessToken = emitirToken usuario
                                    let roles =
                                        usuario
                                        |> Usuario.roles
                                        |> Set.toList
                                        |> List.map NombreRol.toString

                                    let nombre = Usuario.nombreUsuario usuario |> NombreUsuario.valor
                                    return
                                        Ok
                                            { Token = nuevoAccessToken
                                              RefreshTokenRaw = nuevoRaw
                                              NombreUsuario = nombre
                                              Roles = roles }
        }

    /// Caso de uso: Cierre de sesión explícito (Logout).
    /// Revoca el refresh token presentado para impedir su reuso.
    let logout
        (buscarRefreshToken: BuscarRefreshTokenPorHash)
        (revocarRefreshToken: RevocarRefreshToken)
        (calcularTokenHash: CalcularTokenHash)
        (tokenRaw: string) : Async<Result<unit, AuthError>> =

        async {
            if String.IsNullOrWhiteSpace tokenRaw then
                return Ok () // Si no hay token, la sesión ya es nula
            else
                let tokenHash = calcularTokenHash tokenRaw
                let! busquedaResult = buscarRefreshToken tokenHash
                match busquedaResult with
                | Ok (Some token) ->
                    let! revRes = revocarRefreshToken token.Id
                    return revRes
                | _ ->
                    // Si el token no existe o hubo error de lectura, consideramos el logout exitoso
                    return Ok ()
        }
