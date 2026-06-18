namespace Sinbas.Infrastructure

open Sinbas.Domain
open BCrypt.Net

module PasswordHasher =

    let hash (password: string) : PasswordHash =
        let rawHash = BCrypt.HashPassword(password)
        PasswordHash rawHash

    let verify (password: string) (hash: PasswordHash) : bool =
        let (PasswordHash rawHash) = hash
        BCrypt.Verify(password, rawHash)
