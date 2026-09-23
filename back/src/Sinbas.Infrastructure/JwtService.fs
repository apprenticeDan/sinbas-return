namespace Sinbas.Infrastructure

open System
open System.Text
open System.Security.Claims
open System.Security.Cryptography
open System.IdentityModel.Tokens.Jwt
open Microsoft.IdentityModel.Tokens
open Sinbas.Domain

module JwtService =

    let emitirToken (secreto: string) (emisor: string) (audiencia: string) (expiracionHoras: float) (usuario: Usuario) : string =
        let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto))
        let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        
        let claims = [
            Claim(JwtRegisteredClaimNames.Sub, (usuario |> Usuario.id |> fun (UsuarioId id) -> string id))
            Claim("empleado_id", (usuario |> Usuario.empleadoId |> fun (EmpleadoId id) -> string id))
            Claim(JwtRegisteredClaimNames.UniqueName, (usuario |> Usuario.nombreUsuario |> NombreUsuario.valor))
        ]
        
        let roleClaims = 
            usuario 
            |> Usuario.roles 
            |> Set.toList 
            |> List.map (fun r -> Claim(ClaimTypes.Role, NombreRol.toString r))

        let allClaims = claims @ roleClaims

        let token = JwtSecurityToken(
            issuer = emisor,
            audience = audiencia,
            claims = allClaims,
            expires = Nullable(DateTime.UtcNow.AddHours(expiracionHoras)),
            signingCredentials = creds
        )

        JwtSecurityTokenHandler().WriteToken(token)

    /// Genera un token criptográficamente seguro de 32 bytes de alta entropía (Base64 URL-safe).
    /// Este valor viaja al cliente ÚNICAMENTE a través de una cookie HttpOnly.
    let generarRefreshToken () : string =
        let bytes = Array.zeroCreate<byte> 32
        RandomNumberGenerator.Fill(bytes)
        Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=')

    /// Calcula el hash SHA-256 en formato hexadecimal de un refresh token en texto plano.
    /// Este hash es lo ÚNICO que se almacena en la base de datos (MF-00-05).
    let calcularTokenHash (tokenRaw: string) : TokenHash =
        let bytes = Encoding.UTF8.GetBytes(tokenRaw)
        let hashBytes = SHA256.HashData(bytes)
        Convert.ToHexString(hashBytes).ToLowerInvariant()
        |> TokenHash
