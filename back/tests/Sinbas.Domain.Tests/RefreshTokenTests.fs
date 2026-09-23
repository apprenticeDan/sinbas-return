module RefreshTokenTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

let private usuarioIdEjemplo = UsuarioId (Guid.Parse("11111111-1111-1111-1111-111111111111"))
let private tokenHashEjemplo = TokenHash "sha256_fake_hash_value_for_testing"

[<Fact>]
let ``Crear refresh token inicializa campos correctamente con vigencia calculada`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let duracion = TimeSpan.FromHours(12.0)
    
    let rt = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo duracion ahora

    Assert.Equal(usuarioIdEjemplo, rt.UsuarioId)
    Assert.Equal(tokenHashEjemplo, rt.TokenHash)
    Assert.Equal(ahora.AddHours(12.0), rt.ExpiraEn)
    Assert.False(rt.Revocado)
    Assert.True(rt.ReemplazadoPor.IsNone)
    Assert.Equal(ahora, rt.CreadoEn)

[<Fact>]
let ``estaVigente retorna true cuando el token no esta revocado, no expirado y no reemplazado`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(1.0)) ahora
    
    let instanteVerificacion = ahora.AddMinutes(30.0)
    Assert.True(RefreshToken.estaVigente instanteVerificacion rt)

[<Fact>]
let ``estaVigente retorna false si ya expiro en el tiempo`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromMinutes(15.0)) ahora
    
    let tiempoDespues = ahora.AddMinutes(16.0)
    Assert.False(RefreshToken.estaVigente tiempoDespues rt)

[<Fact>]
let ``estaVigente retorna false si el token fue revocado explicitamente`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt =
        RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(1.0)) ahora
        |> RefreshToken.revocar
    
    Assert.False(RefreshToken.estaVigente ahora rt)

[<Fact>]
let ``estaVigente retorna false si el token ya fue reemplazado por rotacion`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let nuevoId = RefreshTokenId (Guid.NewGuid())
    let rt =
        RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(1.0)) ahora
        |> RefreshToken.marcarReemplazado nuevoId
    
    Assert.False(RefreshToken.estaVigente ahora rt)

[<Fact>]
let ``validar retorna Ok para token vigente y activo`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(12.0)) ahora

    let resultado = RefreshToken.validar (ahora.AddHours(1.0)) rt
    match resultado with
    | Ok tokenValido -> Assert.Equal(rt.Id, tokenValido.Id)
    | Error err -> failwithf "Token valido no deberia fallar: %A" err

[<Fact>]
let ``validar retorna TokenRevocado cuando el token fue revocado explicitamente`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt =
        RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(1.0)) ahora
        |> RefreshToken.revocar

    let resultado = RefreshToken.validar ahora rt
    match resultado with
    | Error TokenRevocado -> ()
    | otros -> failwithf "Esperaba TokenRevocado, se obtuvo: %A" otros

[<Fact>]
let ``validar retorna TokenExpirado cuando la fecha actual supera ExpiraEn`` () =
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let rt = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromMinutes(10.0)) ahora

    let resultado = RefreshToken.validar (ahora.AddMinutes(11.0)) rt
    match resultado with
    | Error TokenExpirado -> ()
    | otros -> failwithf "Esperaba TokenExpirado, se obtuvo: %A" otros

[<Fact>]
let ``validar detecta reuso o robo y retorna TokenReutilizado con el UsuarioId correspondiente`` () =
    // Escenario de seguridad critico:
    // Un atacante intercepto un refresh token previo o un cliente envia un token viejo ya rotado.
    // El sistema debe detectar que ReemplazadoPor ya estaba poblado y emitir TokenReutilizado
    // para que la capa de aplicacion revoque toda la sesion/familia de tokens del usuario.
    let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
    let tokenViejo = RefreshToken.crear usuarioIdEjemplo tokenHashEjemplo (TimeSpan.FromHours(12.0)) ahora
    let nuevoTokenId = RefreshTokenId (Guid.NewGuid())
    let tokenYaRotado = RefreshToken.marcarReemplazado nuevoTokenId tokenViejo

    let resultado = RefreshToken.validar (ahora.AddMinutes(5.0)) tokenYaRotado
    match resultado with
    | Error (TokenReutilizado uid) -> Assert.Equal(usuarioIdEjemplo, uid)
    | otros -> failwithf "Esperaba TokenReutilizado, se obtuvo: %A" otros

[<Fact>]
let ``refrescarToken rotacion exitosa emite nuevo par e invalida el anterior`` () =
    async {
        let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
        let tokenRawOriginal = "raw_token_xyz_123"
        let tokenHashOriginal = TokenHash "hash_original"
        let tokenOriginal = RefreshToken.crear usuarioIdEjemplo tokenHashOriginal (TimeSpan.FromHours(12.0)) ahora

        let mutable tokenGuardado = None
        let mutable tokenReemplazado = None
        let mutable familiaRevocada = false

        let buscarToken (th: TokenHash) =
            async {
                if th = tokenHashOriginal then return Ok (Some tokenOriginal)
                else return Ok None
            }
        let guardarToken (rt: RefreshToken) =
            async {
                tokenGuardado <- Some rt
                return Ok ()
            }
        let marcarReemplazado (viejoId: RefreshTokenId) (nuevoId: RefreshTokenId) =
            async {
                tokenReemplazado <- Some (viejoId, nuevoId)
                return Ok ()
            }
        let revocarFamilia _ =
            async {
                familiaRevocada <- true
                return Ok ()
            }
        let buscarUsuario (uid: UsuarioId) =
            async {
                let dummyUser =
                    Usuario.reconstruir
                        (let (UsuarioId id) = uid in id)
                        (Guid.NewGuid())
                        "testuser"
                        "hash"
                        (Set.ofList [ NombreRol.Comercial ])
                        EstadoUsuario.Activo
                return Ok dummyUser
            }
        let emitirToken _ = "nuevo_jwt_access_token_15min"
        let generarRefreshToken () = "nuevo_raw_refresh_token_456"
        let calcularTokenHash (raw: string) =
            if raw = tokenRawOriginal then tokenHashOriginal
            else TokenHash ($"hash_{raw}")

        let! resultado =
            AuthUseCase.refrescarToken
                buscarToken
                guardarToken
                marcarReemplazado
                revocarFamilia
                buscarUsuario
                emitirToken
                generarRefreshToken
                calcularTokenHash
                (TimeSpan.FromHours(12.0))
                (ahora.AddMinutes(30.0))
                tokenRawOriginal

        match resultado with
        | Ok res ->
            Assert.Equal("nuevo_jwt_access_token_15min", res.Token)
            Assert.Equal("nuevo_raw_refresh_token_456", res.RefreshTokenRaw)
            Assert.Equal("testuser", res.NombreUsuario)
            Assert.True(tokenGuardado.IsSome)
            Assert.True(tokenReemplazado.IsSome)
            Assert.False(familiaRevocada)
            let (vId, nId) = tokenReemplazado.Value
            Assert.Equal(tokenOriginal.Id, vId)
            Assert.Equal(tokenGuardado.Value.Id, nId)
        | Error err ->
            failwithf "Refresco deberia haber sido exitoso pero dio: %A" err
    }

[<Fact>]
let ``refrescarToken detecta reuso fraudulento y revoca de inmediato toda la familia de tokens (anti-robo)`` () =
    async {
        let ahora = DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc)
        let tokenRawReusado = "stolen_or_repeated_token"
        let tokenHashReusado = TokenHash "hash_stolen"
        let tokenViejo = RefreshToken.crear usuarioIdEjemplo tokenHashReusado (TimeSpan.FromHours(12.0)) ahora
        let tokenYaRotado = RefreshToken.marcarReemplazado (RefreshTokenId (Guid.NewGuid())) tokenViejo

        let mutable familiaRevocadaParaUsuario = None

        let buscarToken _ = async { return Ok (Some tokenYaRotado) }
        let guardarToken _ = async { return Ok () }
        let marcarReemplazado _ _ = async { return Ok () }
        let revocarFamilia (uid: UsuarioId) =
            async {
                familiaRevocadaParaUsuario <- Some uid
                return Ok ()
            }
        let buscarUsuario _ = failwith "No deberia buscar usuario si se detecto robo"
        let emitirToken _ = failwith "No deberia emitir token"
        let generarRefreshToken () = failwith "No deberia generar token"
        let calcularTokenHash _ = tokenHashReusado

        let! resultado =
            AuthUseCase.refrescarToken
                buscarToken
                guardarToken
                marcarReemplazado
                revocarFamilia
                buscarUsuario
                emitirToken
                generarRefreshToken
                calcularTokenHash
                (TimeSpan.FromHours(12.0))
                (ahora.AddMinutes(5.0))
                tokenRawReusado

        match resultado with
        | Error (RefreshTokenInvalido (TokenReutilizado uid)) ->
            Assert.Equal(usuarioIdEjemplo, uid)
            Assert.Equal(Some usuarioIdEjemplo, familiaRevocadaParaUsuario)
        | otros ->
            failwithf "Esperaba deteccion de robo TokenReutilizado y revocacion de familia, se obtuvo: %A" otros
    }

[<Fact>]
let ``desactivarUsuario revoca en cascada todos los refresh tokens activos (RN15)`` () =
    async {
        let uidGuid = Guid.NewGuid()
        let usuarioId = UsuarioId uidGuid
        let usuarioActivo =
            Usuario.reconstruir
                uidGuid
                (Guid.NewGuid())
                "operario"
                "hash"
                (Set.ofList [ NombreRol.Almacen ])
                EstadoUsuario.Activo

        let mutable usuarioGuardado = None
        let mutable tokensRevocadosPara = None

        let buscarPorId (id: UsuarioId) =
            async {
                if id = usuarioId then return Ok usuarioActivo
                else return Error (ErrorInterno "No encontrado")
            }
        let guardarUsuario (u: Usuario) =
            async {
                usuarioGuardado <- Some u
                return Ok ()
            }
        let revocarFamiliaTokens (uid: UsuarioId) =
            async {
                tokensRevocadosPara <- Some uid
                return Ok ()
            }

        let cmd : DisableUserCommand = { UsuarioId = uidGuid }
        let! res = UserUseCase.desactivarUsuario buscarPorId guardarUsuario revocarFamiliaTokens cmd

        match res with
        | Ok () ->
            Assert.True(usuarioGuardado.IsSome)
            Assert.Equal(EstadoUsuario.Desactivado, Usuario.estado usuarioGuardado.Value)
            Assert.Equal(Some usuarioId, tokensRevocadosPara)
        | Error err ->
            failwithf "Desactivar usuario deberia haber triunfado: %A" err
    }
