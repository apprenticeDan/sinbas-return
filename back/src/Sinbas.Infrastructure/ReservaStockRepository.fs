namespace Sinbas.Infrastructure

open System
open Dapper
open Npgsql
open Sinbas.Domain

[<CLIMutable>]
type private ReservaStockActivaRow =
    { lote_id: Guid
      cantidad: decimal
      unidad: string }

module ReservaStockRepository =

    let insertarBatch
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (ordenVentaId: OrdenId)
        (reservas: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            if List.isEmpty reservas then
                return Ok ()
            else
                let (OrdenId ovId) = ordenVentaId
                let rows =
                    reservas
                    |> List.map (fun (LoteId lId, cant) ->
                        {| id = Identidad.nuevo ()
                           origen_tipo = "VENTA"
                           orden_venta_id = box ovId
                           solicitud_id = null
                           lote_id = lId
                           cantidad = cant.Valor
                           unidad = UnidadMedida.aTexto cant.Unidad
                           estado = "Activa" |})
                    |> List.toArray

                let sql = """
                    INSERT INTO reserva_stock (
                        id, origen_tipo, orden_venta_id, solicitud_id, lote_id, cantidad, unidad, estado, movimiento_id
                    ) VALUES (
                        @id, @origen_tipo, @orden_venta_id, @solicitud_id, @lote_id, @cantidad, @unidad, @estado, NULL
                    )
                """
                let! affected = conn.ExecuteAsync(sql, rows, tx) |> Async.AwaitTask
                if affected = rows.Length then
                    return Ok ()
                else
                    return Error (sprintf "ReservaStockRepository.insertarBatch: se esperaban %d filas insertadas pero se afectaron %d" rows.Length affected)
        }

    let listarActivasPorVenta
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (ordenVentaId: OrdenId)
        : Async<Result<(LoteId * Cantidad) list, string>> =
        async {
            let (OrdenId ovId) = ordenVentaId
            let sql = """
                SELECT lote_id, cantidad, unidad
                FROM reserva_stock
                WHERE orden_venta_id = @ovId AND estado = 'Activa'
                FOR UPDATE
            """
            let! rows = conn.QueryAsync<ReservaStockActivaRow>(sql, {| ovId = ovId |}, tx) |> Async.AwaitTask
            let rec reconstruir acc = function
                | [] -> Ok (List.rev acc)
                | (row: ReservaStockActivaRow) :: rest ->
                    match UnidadMedida.desdeTexto row.unidad with
                    | Error err ->
                        Error (sprintf "Error reconstruyendo unidad '%s' en reserva_stock: %A" row.unidad err)
                    | Ok unidadMedida ->
                        match Cantidad.reconstruir row.cantidad unidadMedida with
                        | Error err ->
                            Error (sprintf "Error reconstruyendo cantidad %M %s en reserva_stock: %A" row.cantidad row.unidad err)
                        | Ok cant ->
                            reconstruir ((LoteId row.lote_id, cant) :: acc) rest

            return reconstruir [] (rows |> Seq.toList)
        }

    let marcarDespachada
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (ordenVentaId: OrdenId)
        (movimientoId: MovimientoId)
        : Async<Result<unit, string>> =
        async {
            let (OrdenId ovId) = ordenVentaId
            let (MovimientoId mId) = movimientoId
            let sql = """
                UPDATE reserva_stock
                SET estado = 'Despachada',
                    despachado_en = NOW(),
                    movimiento_id = @mId
                WHERE orden_venta_id = @ovId AND estado = 'Activa'
            """
            let! affected = conn.ExecuteAsync(sql, {| ovId = ovId; mId = mId |}, tx) |> Async.AwaitTask
            if affected > 0 then
                return Ok ()
            else
                return Error (sprintf "ReservaStockRepository.marcarDespachada: no se encontraron reservas activas para la orden %s" (ovId.ToString()))
        }

    let marcarLiberada
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (ordenVentaId: OrdenId)
        : Async<Result<unit, string>> =
        async {
            let (OrdenId ovId) = ordenVentaId
            let sql = """
                UPDATE reserva_stock
                SET estado = 'Liberada',
                    liberado_en = NOW()
                WHERE orden_venta_id = @ovId AND estado = 'Activa'
            """
            let! affected = conn.ExecuteAsync(sql, {| ovId = ovId |}, tx) |> Async.AwaitTask
            if affected > 0 then
                return Ok ()
            else
                return Error (sprintf "ReservaStockRepository.marcarLiberada: no se encontraron reservas activas para la orden %s" (ovId.ToString()))
        }
