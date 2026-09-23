namespace Sinbas.Infrastructure

open System
open Dapper
open Sinbas.Domain

[<CLIMutable>]
type RegistroIdempotenciaRow =
    { clave: string
      endpoint: string
      usuario_id: Guid
      status_code: int
      cuerpo_respuesta: string
      creado_en: DateTime }

/// Repositorio de idempotencia para garantizar que reintentos de peticiones
/// (debidos a refresco de token o caídas momentáneas de red) no dupliquen
/// transacciones críticas en Kardex o Ventas (MF-00-07).
module IdempotenciaRepository =

    let buscar (clave: string) : Async<RegistroIdempotenciaRow option> =
        async {
            try
                use conn = DbConnection.crear ()
                let sql = """
                    select clave, endpoint, usuario_id, status_code, cuerpo_respuesta, creado_en
                    from registro_idempotencia
                    where clave = @clave
                    limit 1
                """
                let! row = conn.QueryFirstOrDefaultAsync<RegistroIdempotenciaRow>(sql, {| clave = clave |}) |> Async.AwaitTask
                if isNull (box row) || String.IsNullOrWhiteSpace row.clave then
                    return None
                else
                    return Some row
            with _ ->
                return None
        }

    let guardar (row: RegistroIdempotenciaRow) : Async<unit> =
        async {
            try
                use conn = DbConnection.crear ()
                let sql = """
                    insert into registro_idempotencia (clave, endpoint, usuario_id, status_code, cuerpo_respuesta, creado_en)
                    values (@clave, @endpoint, @usuario_id, @status_code, @cuerpo_respuesta, @creado_en)
                    on conflict (clave) do nothing
                """
                let! _ = conn.ExecuteAsync(sql, row) |> Async.AwaitTask
                return ()
            with _ ->
                return ()
        }
