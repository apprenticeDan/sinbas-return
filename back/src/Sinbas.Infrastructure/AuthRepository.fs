namespace Sinbas.Infrastructure

open System
open Dapper
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Tipos de fila para mapeo con Dapper
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type UsuarioRow =
    { id            : int
      empleado_id   : int
      nombre_usuario: string
      password_hash : string
      estado        : string }

[<CLIMutable>]
type UsuarioRolRow =
    { usuario_id: int
      rol       : string }

// ─────────────────────────────────────────────────────────────
// Helpers privados
// ─────────────────────────────────────────────────────────────

module private AuthRepositoryHelpers =

    let reconstruirDesdeFilas (row: UsuarioRow) (roles: UsuarioRolRow seq) : Usuario =
        let rolSet =
            roles
            |> Seq.choose (fun r ->
                match NombreRol.fromString r.rol with
                | Ok rol -> Some rol
                | Error _ -> None)
            |> Set.ofSeq

        let estado =
            match row.estado with
            | "Activo" -> EstadoUsuario.Activo
            | _        -> EstadoUsuario.Bloqueado

        Usuario.reconstruir
            row.id
            row.empleado_id
            row.nombre_usuario
            row.password_hash
            rolSet
            estado

    let cargarRoles (conn: Npgsql.NpgsqlConnection) (usuarioId: int) =
        conn.QueryAsync<UsuarioRolRow>(
            "SELECT usuario_id, rol FROM usuario_rol WHERE usuario_id = @Id",
            {| Id = usuarioId |}
        )

    let persistirRoles (conn: Npgsql.NpgsqlConnection) (usuarioId: int) (roles: NombreRol Set) =
        async {
            do! conn.ExecuteAsync(
                    "DELETE FROM usuario_rol WHERE usuario_id = @Id",
                    {| Id = usuarioId |}
                ) |> Async.AwaitTask |> Async.Ignore

            let rolRows =
                roles
                |> Set.toList
                |> List.map (fun r -> {| usuario_id = usuarioId; rol = NombreRol.toString r |})

            if not rolRows.IsEmpty then
                do! conn.ExecuteAsync(
                        "INSERT INTO usuario_rol (usuario_id, rol) VALUES (@usuario_id, @rol)",
                        rolRows
                    ) |> Async.AwaitTask |> Async.Ignore
        }

// ─────────────────────────────────────────────────────────────
// Módulo público del repositorio
// ─────────────────────────────────────────────────────────────

module AuthRepository =

    open AuthRepositoryHelpers

    let buscarUsuarioPorNombre (nombre: NombreUsuario) : Async<Result<Usuario, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let valorNombre = NombreUsuario.valor nombre

            let! rowOpt =
                conn.QueryFirstOrDefaultAsync<UsuarioRow>(
                    "SELECT id, empleado_id, nombre_usuario, password_hash, estado
                     FROM usuario
                     WHERE nombre_usuario = @Nombre",
                    {| Nombre = valorNombre |}
                ) |> Async.AwaitTask

            match box rowOpt with
            | null -> return Error CredencialesInvalidas
            | _ ->
                let! rolesSeq = cargarRoles conn rowOpt.id |> Async.AwaitTask
                return Ok (reconstruirDesdeFilas rowOpt rolesSeq)
        }

    let buscarUsuarioPorId (id: UsuarioId) : Async<Result<Usuario, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let (UsuarioId rawId) = id

            let! rowOpt =
                conn.QueryFirstOrDefaultAsync<UsuarioRow>(
                    "SELECT id, empleado_id, nombre_usuario, password_hash, estado
                     FROM usuario
                     WHERE id = @Id",
                    {| Id = rawId |}
                ) |> Async.AwaitTask

            match box rowOpt with
            | null -> return Error (ErrorInterno "Usuario no encontrado")
            | _ ->
                let! rolesSeq = cargarRoles conn rowOpt.id |> Async.AwaitTask
                return Ok (reconstruirDesdeFilas rowOpt rolesSeq)
        }

    let listarUsuarios () : Async<Usuario list> =
        async {
            use conn = DbConnection.crear ()

            let! filas =
                conn.QueryAsync<UsuarioRow>(
                    "SELECT id, empleado_id, nombre_usuario, password_hash, estado FROM usuario ORDER BY id"
                ) |> Async.AwaitTask

            let! roles =
                conn.QueryAsync<UsuarioRolRow>(
                    "SELECT usuario_id, rol FROM usuario_rol"
                ) |> Async.AwaitTask

            let rolesPorUsuario =
                roles
                |> Seq.groupBy (fun r -> r.usuario_id)
                |> Map.ofSeq

            return
                filas
                |> Seq.map (fun row ->
                    let rolesDelUsuario =
                        rolesPorUsuario
                        |> Map.tryFind row.id
                        |> Option.defaultValue Seq.empty
                    reconstruirDesdeFilas row rolesDelUsuario)
                |> Seq.toList
        }

    let guardarUsuario (usuario: Usuario) : Async<Result<unit, AuthError>> =
        async {
            use conn = DbConnection.crear ()
            let (UsuarioId id)       = Usuario.id usuario
            let (EmpleadoId empId)   = Usuario.empleadoId usuario
            let nombre               = Usuario.nombreUsuario usuario |> NombreUsuario.valor
            let (PasswordHash hash)  = Usuario.hash usuario
            let estado               = sprintf "%A" (Usuario.estado usuario)
            let roles                = Usuario.roles usuario

            try
                if id = 0 then
                    // INSERT — PostgreSQL genera el ID
                    let! nuevoId =
                        conn.ExecuteScalarAsync<int>(
                            "INSERT INTO usuario (empleado_id, nombre_usuario, password_hash, estado)
                             VALUES (@EmpleadoId, @NombreUsuario, @PasswordHash, @Estado)
                             RETURNING id",
                            {| EmpleadoId = empId
                               NombreUsuario = nombre
                               PasswordHash = hash
                               Estado = estado |}
                        ) |> Async.AwaitTask

                    do! persistirRoles conn nuevoId roles
                    return Ok ()
                else
                    // UPDATE
                    do! conn.ExecuteAsync(
                            "UPDATE usuario
                             SET nombre_usuario = @NombreUsuario,
                                 password_hash  = @PasswordHash,
                                 estado         = @Estado
                             WHERE id = @Id",
                            {| Id = id
                               NombreUsuario = nombre
                               PasswordHash = hash
                               Estado = estado |}
                        ) |> Async.AwaitTask |> Async.Ignore

                    do! persistirRoles conn id roles
                    return Ok ()

            with ex ->
                return Error (ErrorInterno ex.Message)
        }
