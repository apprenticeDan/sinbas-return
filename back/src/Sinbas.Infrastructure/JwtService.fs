namespace Sinbas.Infrastructure

open System
open System.Text
open System.Security.Claims
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
