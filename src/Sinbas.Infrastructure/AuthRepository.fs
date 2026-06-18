namespace Sinbas.Infrastructure

open System
open System.Collections.Concurrent
open Sinbas.Domain

module AuthRepository =

    // In-memory store
    let private usuarios = ConcurrentDictionary<UsuarioId, Usuario>()
    let private lastId = ref 0

    // Seed initial data
    let seed adminPasswordHash =
        let username =
            match Usuario.validarNombreUsuario "admin" with
            | Ok u -> u
            | Error e -> failwithf "Error seeding admin username: %A" e
        
        let roles = Set.ofList [Administrador]
        
        // Reconstruct to assign ID = 1 and status = Activo
        let adminHydrated = Usuario.reconstruir 1 1 "admin" adminPasswordHash roles EstadoUsuario.Activo
        usuarios.TryAdd(UsuarioId 1, adminHydrated) |> ignore
        lastId := 1

    let buscarUsuarioPorNombre (nombre: NombreUsuario) : Async<Result<Usuario, AuthError>> =
        async {
            let valorNombre = NombreUsuario.valor nombre
            let usuarioOpt =
                usuarios.Values
                |> Seq.tryFind (fun u ->
                    (u |> Usuario.nombreUsuario |> NombreUsuario.valor) = valorNombre)
            
            match usuarioOpt with
            | Some u -> return Ok u
            | None -> return Error CredencialesInvalidas
        }

    let buscarUsuarioPorId (id: UsuarioId) : Async<Result<Usuario, AuthError>> =
        async {
            match usuarios.TryGetValue(id) with
            | true, u -> return Ok u
            | false, _ -> return Error (ErrorInterno "Usuario no encontrado")
        }

    let listarUsuarios () : Async<Usuario list> =
        async {
            return usuarios.Values |> Seq.toList
        }

    let guardarUsuario (usuario: Usuario) : Async<Result<unit, AuthError>> =
        async {
            let (UsuarioId id) = Usuario.id usuario
            if id = 0 then
                let newId = System.Threading.Interlocked.Increment(lastId)
                let userWithId = 
                    Usuario.reconstruir 
                        newId 
                        (usuario |> Usuario.empleadoId |> fun (EmpleadoId eid) -> eid)
                        (usuario |> Usuario.nombreUsuario |> NombreUsuario.valor)
                        (usuario |> Usuario.hash |> fun (PasswordHash h) -> h)
                        (Usuario.roles usuario)
                        (Usuario.estado usuario)

                if usuarios.TryAdd(UsuarioId newId, userWithId) then
                    return Ok ()
                else
                    return Error (ErrorInterno "No se pudo crear el usuario")
            else
                usuarios.[UsuarioId id] <- usuario
                return Ok ()
        }
