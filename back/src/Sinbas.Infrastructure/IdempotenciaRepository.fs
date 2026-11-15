namespace Sinbas.Infrastructure

open System
open Npgsql
open Dapper
open Sinbas.Domain

/// Repositorio de idempotencia transaccional (Q5 / MF-00-07)
module IdempotenciaRepository =

    [<CLIMutable>]
    type private IdempotenciaRow =
        { request_hash: string
          cuerpo_respuesta: string
          status_code: int
          expirado: bool }

    let consultar
        (claim: IdempotenciaClaim)
        : Async<Result<ClaimOutcome, string>> =
        async {
            try
                use conn = DbConnection.crear ()
                do! conn.OpenAsync() |> Async.AwaitTask
                let (UsuarioId uId) = claim.UsuarioId
                let sqlSelect = """
                    SELECT request_hash, cuerpo_respuesta, status_code, (expira_en < NOW()) AS expirado
                    FROM registro_idempotencia
                    WHERE usuario_id = @u AND endpoint = @e AND clave = @k
                """
                let! existente =
                    conn.QueryFirstOrDefaultAsync<IdempotenciaRow>(
                        sqlSelect,
                        {| u = uId; e = claim.Endpoint; k = claim.Clave |})
                    |> Async.AwaitTask

                if isNull (box existente) || existente.expirado then
                    return Ok ClaimOutcome.Nuevo
                elif existente.request_hash <> claim.RequestHash then
                    return Ok ClaimOutcome.HashMismatch
                elif existente.status_code > 0 then
                    return Ok (ClaimOutcome.Replay (existente.cuerpo_respuesta, existente.status_code))
                else
                    return Ok ClaimOutcome.Nuevo
            with ex ->
                return Error (sprintf "Error al consultar idempotencia: %s" ex.Message)
        }

    let claimEnTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (claim: IdempotenciaClaim)
        : Async<Result<ClaimOutcome, string>> =
        async {
            try
                let (UsuarioId uId) = claim.UsuarioId
                let ttlHours = max 1 claim.TtlHours
                let sqlInsert = """
                    INSERT INTO registro_idempotencia
                        (usuario_id, endpoint, clave, request_hash, status_code, cuerpo_respuesta, creado_en, expira_en)
                    VALUES
                        (@u, @e, @k, @h, 0, '', NOW(), NOW() + (@ttl || ' hours')::interval)
                    ON CONFLICT (usuario_id, endpoint, clave) DO NOTHING
                """
                let! affected =
                    conn.ExecuteAsync(
                        sqlInsert,
                        {| u = uId; e = claim.Endpoint; k = claim.Clave; h = claim.RequestHash; ttl = ttlHours |},
                        tx)
                    |> Async.AwaitTask

                if affected > 0 then
                    return Ok ClaimOutcome.Nuevo
                else
                    // La fila ya existía: comparar huella y vigencia de TTL evaluado en BD
                    let sqlSelect = """
                        SELECT request_hash, cuerpo_respuesta, status_code, (expira_en < NOW()) AS expirado
                        FROM registro_idempotencia
                        WHERE usuario_id = @u AND endpoint = @e AND clave = @k
                        FOR UPDATE
                    """
                    let! existente =
                        conn.QueryFirstOrDefaultAsync<IdempotenciaRow>(
                            sqlSelect,
                            {| u = uId; e = claim.Endpoint; k = claim.Clave |},
                            tx)
                        |> Async.AwaitTask

                    if isNull (box existente) then
                        return Error "Idempotencia: fila no encontrada tras conflicto"
                    elif existente.expirado then
                        // Expiró TTL: reclamo nuevo tras expiración
                        let sqlUpdateExp = """
                            UPDATE registro_idempotencia
                            SET request_hash = @h,
                                expira_en = NOW() + (@ttl || ' hours')::interval,
                                cuerpo_respuesta = '',
                                status_code = 0
                            WHERE usuario_id = @u AND endpoint = @e AND clave = @k
                        """
                        let! _ =
                            conn.ExecuteAsync(
                                sqlUpdateExp,
                                {| u = uId; e = claim.Endpoint; k = claim.Clave; h = claim.RequestHash; ttl = ttlHours |},
                                tx)
                            |> Async.AwaitTask
                        return Ok ClaimOutcome.Nuevo
                    elif existente.request_hash <> claim.RequestHash then
                        return Ok ClaimOutcome.HashMismatch
                    else
                        return Ok (ClaimOutcome.Replay (existente.cuerpo_respuesta, existente.status_code))
            with ex ->
                return Error (sprintf "Error en claim de idempotencia: %s" ex.Message)
        }

    let actualizarRespuestaEnTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (claim: IdempotenciaClaim)
        (status: int)
        (cuerpo: string)
        : Async<Result<unit, string>> =
        async {
            try
                let (UsuarioId uId) = claim.UsuarioId
                let sqlUpdate = """
                    UPDATE registro_idempotencia
                    SET cuerpo_respuesta = @c, status_code = @s
                    WHERE usuario_id = @u AND endpoint = @e AND clave = @k
                """
                let! affected =
                    conn.ExecuteAsync(
                        sqlUpdate,
                        {| c = cuerpo; s = status; u = uId; e = claim.Endpoint; k = claim.Clave |},
                        tx)
                    |> Async.AwaitTask
                if affected > 0 then
                    return Ok ()
                else
                    return Error "No se pudo actualizar registro de idempotencia con la respuesta"
            with ex ->
                return Error (sprintf "Error al actualizar respuesta de idempotencia: %s" ex.Message)
        }
