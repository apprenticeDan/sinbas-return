namespace Sinbas.Application

namespace Sinbas.Application

open Sinbas.Domain

type BuscarUsuarioPorNombre = NombreUsuario -> Async<Result<Usuario, AuthError>>

type VerificarHash = string -> PasswordHash -> bool

type EmitirToken = Usuario -> string

type LoginCommand =
    { NombreUsuarioRaw: string
      Contrasena: string }

type LoginResult =
    { Token: string
      NombreUsuario: string
      Roles: string list }

module AuthUseCase =

    let login buscarUsuario verificarHash emitirToken cmd =

        async {

            match Usuario.validarNombreUsuario cmd.NombreUsuarioRaw with
            | Error e -> return Error e

            | Ok nombreUsuario ->

                let! usuarioResult = buscarUsuario nombreUsuario

                match usuarioResult with

                | Error e -> return Error e

                | Ok usuario ->

                    if not (Usuario.activo usuario) then
                        return Error UsuarioInactivo

                    else

                        match verificarHash cmd.Contrasena (Usuario.hash usuario) with

                        | false -> return Error CredencialesInvalidas

                        | true ->

                            let token = emitirToken usuario

                            let roles =
                                usuario |> Usuario.roles |> List.map (fun r -> NombreRol.toString r.Nombre)

                            let nombre = Usuario.nombreUsuario usuario |> NombreUsuario.valor
                            return
                                Ok
                                    { Token = token
                                      NombreUsuario = nombre
                                      Roles = roles }
        }
