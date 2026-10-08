namespace Sinbas.Infrastructure

open System
open Dapper
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// Feature F7: Proformas — Repositorio PostgreSQL
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type ProformaRow =
    { id: Guid
      fecha: DateOnly
      creado_en: DateTime
      responsable_id: Guid
      cliente_id: Nullable<Guid>
      cliente_nombre_libre: string
      estado: string
      fecha_vencimiento: Nullable<DateOnly>
      moneda: string
      total: decimal
      orden_venta_id: Nullable<Guid>
      anulacion_motivo: string
      anulado_por: Nullable<Guid>
      anulado_en: Nullable<DateTime>
      observaciones: string
      leyenda: string }

[<CLIMutable>]
type LineaProformaRow =
    { proforma_id: Guid
      item: int
      producto_id: Guid
      cantidad: decimal
      unidad: string
      precio_unitario: decimal
      subtotal: decimal }

module ProformaRepository =

    let private reconstruirProforma (row: ProformaRow) (lineaRows: LineaProformaRow list) : Result<Proforma, string> =
        let estadoRes = EstadoProforma.desdeTexto row.estado
        match estadoRes with
        | Error err ->
            eprintfn "[INTEGRIDAD_CRITICA] ProformaRepository: estado inválido '%s' para proforma %s: %A" row.estado (row.id.ToString()) err
            Error (sprintf "Estado inválido para proforma %s" (row.id.ToString()))
        | Ok estado ->

        let lineas =
            lineaRows
            |> List.sortBy (fun lr -> lr.item)
            |> List.choose (fun lr ->
                let unidadRes = UnidadMedida.desdeTexto lr.unidad
                match unidadRes with
                | Error _ ->
                    eprintfn "[INTEGRIDAD_CRITICA] ProformaRepository: unidad inválida '%s' en linea %d de proforma %s" lr.unidad lr.item (row.id.ToString())
                    None
                | Ok unidad ->
                    match Cantidad.reconstruir lr.cantidad unidad with
                    | Error _ -> None
                    | Ok cant ->
                        Some { ProductoId = ProductoId lr.producto_id
                               Cantidad = cant
                               PrecioUnitario = lr.precio_unitario })

        let fv = if row.fecha_vencimiento.HasValue then Some row.fecha_vencimiento.Value else None
        let clienteIdOpt = if row.cliente_id.HasValue then Some (ClienteId row.cliente_id.Value) else None
        let ordenVentaIdOpt = if row.orden_venta_id.HasValue then Some (OrdenId row.orden_venta_id.Value) else None
        let anuladoPorOpt = if row.anulado_por.HasValue then Some (UsuarioId row.anulado_por.Value) else None
        let anuladoEnOpt = if row.anulado_en.HasValue then Some row.anulado_en.Value else None
        let obsOpt = if String.IsNullOrWhiteSpace row.observaciones then None else Some (row.observaciones.Trim())
        let clienteNombreOpt = if String.IsNullOrWhiteSpace row.cliente_nombre_libre then None else Some (row.cliente_nombre_libre.Trim())
        let anulacionMotivoOpt = if String.IsNullOrWhiteSpace row.anulacion_motivo then None else Some (row.anulacion_motivo.Trim())
        let leyendaStr = if String.IsNullOrWhiteSpace row.leyenda then LeyendaNoReserva else row.leyenda

        Ok { Id = ProformaId row.id
             Fecha = row.fecha
             CreadoEn = row.creado_en
             ResponsableId = UsuarioId row.responsable_id
             ClienteId = clienteIdOpt
             ClienteNombreLibre = clienteNombreOpt
             Estado = estado
             FechaVencimiento = fv
             Moneda = row.moneda
             Lineas = lineas
             Total = row.total
             Leyenda = leyendaStr
             OrdenVentaId = ordenVentaIdOpt
             AnulacionMotivo = anulacionMotivoOpt
             AnuladoPor = anuladoPorOpt
             AnuladoEn = anuladoEnOpt
             Observaciones = obsOpt }

    /// Inserción atómica transaccional de cabecera + líneas
    let insertar (proforma: Proforma) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                let (ProformaId pid) = proforma.Id
                let (UsuarioId uid) = proforma.ResponsableId

                let sqlCabecera = """
                    INSERT INTO proforma (id, fecha, creado_en, responsable_id, cliente_id, cliente_nombre_libre,
                                          estado, fecha_vencimiento, moneda, total, orden_venta_id,
                                          anulacion_motivo, anulado_por, anulado_en, observaciones, leyenda)
                    VALUES (@id, @fecha, @creado_en, @responsable_id, @cliente_id, @cliente_nombre_libre,
                            @estado, @fecha_vencimiento, @moneda, @total, @orden_venta_id,
                            @anulacion_motivo, @anulado_por, @anulado_en, @observaciones, @leyenda)
                """

                let clienteIdGuid = proforma.ClienteId |> Option.map (fun (ClienteId cid) -> box cid) |> Option.defaultValue (box DBNull.Value)
                let ordenIdGuid = proforma.OrdenVentaId |> Option.map (fun (OrdenId oid) -> box oid) |> Option.defaultValue (box DBNull.Value)
                let anuladoPorGuid = proforma.AnuladoPor |> Option.map (fun (UsuarioId u) -> box u) |> Option.defaultValue (box DBNull.Value)
                let fvObj = proforma.FechaVencimiento |> Option.map (fun fv -> box fv) |> Option.defaultValue (box DBNull.Value)
                let anuladoEnObj = proforma.AnuladoEn |> Option.map box |> Option.defaultValue (box DBNull.Value)

                let paramsCab = {|
                    id = pid
                    fecha = proforma.Fecha
                    creado_en = proforma.CreadoEn
                    responsable_id = uid
                    cliente_id = clienteIdGuid
                    cliente_nombre_libre = proforma.ClienteNombreLibre |> Option.defaultValue null
                    estado = EstadoProforma.aTexto proforma.Estado
                    fecha_vencimiento = fvObj
                    moneda = proforma.Moneda
                    total = proforma.Total
                    orden_venta_id = ordenIdGuid
                    anulacion_motivo = proforma.AnulacionMotivo |> Option.defaultValue null
                    anulado_por = anuladoPorGuid
                    anulado_en = anuladoEnObj
                    observaciones = proforma.Observaciones |> Option.defaultValue null
                    leyenda = proforma.Leyenda
                |}

                do! conn.ExecuteAsync(sqlCabecera, paramsCab, tx) |> Async.AwaitTask |> Async.Ignore

                // Insertar líneas
                let sqlLinea = """
                    INSERT INTO linea_proforma (proforma_id, item, producto_id, cantidad, unidad, precio_unitario, subtotal)
                    VALUES (@proforma_id, @item, @producto_id, @cantidad, @unidad, @precio_unitario, @subtotal)
                """

                for (i, linea) in proforma.Lineas |> List.mapi (fun i l -> (i + 1, l)) do
                    let (ProductoId lpid) = linea.ProductoId
                    let paramsLinea = {|
                        proforma_id = pid
                        item = i
                        producto_id = lpid
                        cantidad = linea.Cantidad.Valor
                        unidad = UnidadMedida.aTexto linea.Cantidad.Unidad
                        precio_unitario = linea.PrecioUnitario
                        subtotal = Cotizacion.subtotal linea
                    |}
                    do! conn.ExecuteAsync(sqlLinea, paramsLinea, tx) |> Async.AwaitTask |> Async.Ignore

                tx.Commit()
                return Ok ()
            with ex ->
                tx.Rollback()
                eprintfn "[ProformaRepository.insertar] Error: %s" ex.Message
                return Error ex.Message
        }

    let obtenerPorId (id: ProformaId) : Async<Proforma option> =
        async {
            use conn = DbConnection.crear ()
            let (ProformaId pid) = id

            let sqlCab = "SELECT * FROM proforma WHERE id = @id"
            let! cabRows = conn.QueryAsync<ProformaRow>(sqlCab, {| id = pid |}) |> Async.AwaitTask

            match cabRows |> Seq.tryHead with
            | None -> return None
            | Some row ->
                let sqlLineas = "SELECT * FROM linea_proforma WHERE proforma_id = @pid ORDER BY item"
                let! lineaRows = conn.QueryAsync<LineaProformaRow>(sqlLineas, {| pid = pid |}) |> Async.AwaitTask
                let lineas = lineaRows |> Seq.toList

                match reconstruirProforma row lineas with
                | Ok p -> return Some p
                | Error msg ->
                    eprintfn "[INTEGRIDAD_CRITICA] ProformaRepository.obtenerPorId: %s" msg
                    return None
        }

    let listar (estadoFilter: string option) (clienteIdFilter: string option) : Async<Proforma list> =
        async {
            use conn = DbConnection.crear ()

            // Construir WHERE dinámico
            let mutable whereParts = ResizeArray<string>()
            let dynParams = System.Collections.Generic.Dictionary<string, obj>()

            match estadoFilter with
            | Some e when not (String.IsNullOrWhiteSpace e) ->
                whereParts.Add("p.estado = @estado")
                dynParams.["estado"] <- box e
            | _ -> ()

            match clienteIdFilter with
            | Some c when not (String.IsNullOrWhiteSpace c) ->
                match Guid.TryParse(c) with
                | true, cGuid ->
                    whereParts.Add("p.cliente_id = @cliente_id")
                    dynParams.["cliente_id"] <- box cGuid
                | false, _ -> ()
            | _ -> ()

            let whereClause =
                if whereParts.Count = 0 then ""
                else "WHERE " + String.Join(" AND ", whereParts)

            let sql = sprintf "SELECT * FROM proforma p %s ORDER BY p.creado_en DESC" whereClause

            let dapperParams = DynamicParameters()
            for kv in dynParams do
                dapperParams.Add(kv.Key, kv.Value)

            let! cabRows = conn.QueryAsync<ProformaRow>(sql, dapperParams) |> Async.AwaitTask
            let cabList = cabRows |> Seq.toList

            if List.isEmpty cabList then
                return []
            else

            // Cargar todas las líneas de una vez
            let ids = cabList |> List.map (fun r -> r.id)
            let sqlLineas = "SELECT * FROM linea_proforma WHERE proforma_id = ANY(@ids) ORDER BY proforma_id, item"
            let! lineaRows = conn.QueryAsync<LineaProformaRow>(sqlLineas, {| ids = ids |> List.toArray |}) |> Async.AwaitTask
            let lineasPorProforma =
                lineaRows
                |> Seq.groupBy (fun lr -> lr.proforma_id)
                |> Map.ofSeq

            return
                cabList
                |> List.choose (fun row ->
                    let lineas = lineasPorProforma |> Map.tryFind row.id |> Option.map Seq.toList |> Option.defaultValue []
                    match reconstruirProforma row lineas with
                    | Ok p -> Some p
                    | Error msg ->
                        eprintfn "[INTEGRIDAD_CRITICA] ProformaRepository.listar id=%s: %s" (row.id.ToString()) msg
                        None)
        }

    let actualizarEstado (proforma: Proforma) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let (ProformaId pid) = proforma.Id
            try
                let anuladoPorGuid = proforma.AnuladoPor |> Option.map (fun (UsuarioId u) -> box u) |> Option.defaultValue (box DBNull.Value)
                let anuladoEnObj = proforma.AnuladoEn |> Option.map box |> Option.defaultValue (box DBNull.Value)

                let sql = """
                    UPDATE proforma
                    SET estado = @estado,
                        anulacion_motivo = @anulacion_motivo,
                        anulado_por = @anulado_por,
                        anulado_en = @anulado_en
                    WHERE id = @id
                """
                let p = {|
                    id = pid
                    estado = EstadoProforma.aTexto proforma.Estado
                    anulacion_motivo = proforma.AnulacionMotivo |> Option.defaultValue null
                    anulado_por = anuladoPorGuid
                    anulado_en = anuladoEnObj
                |}
                do! conn.ExecuteAsync(sql, p) |> Async.AwaitTask |> Async.Ignore
                return Ok ()
            with ex ->
                return Error ex.Message
        }
