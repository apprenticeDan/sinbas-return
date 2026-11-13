namespace Sinbas.Infrastructure

open System
open Npgsql
open Dapper
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// Feature F8: Ventas y Despacho Físico — Repositorio PostgreSQL
// (RF-08 | CU-11 | RN04 / RN12 / RN14 / RNF05)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type OrdenVentaRow =
    { id: Guid
      codigo: string
      proforma_id: Guid
      cliente_id: Guid
      fecha: DateOnly
      responsable_id: Guid
      total: decimal
      moneda: string
      estado: string
      creado_en: DateTime
      anulacion_motivo: string
      anulado_por: Nullable<Guid>
      anulado_en: Nullable<DateTime> }

[<CLIMutable>]
type LineaOrdenVentaRow =
    { orden_venta_id: Guid
      item: int
      producto_id: Guid
      cantidad: decimal
      unidad: string
      precio_unitario: decimal
      subtotal: decimal }

[<CLIMutable>]
type OrdenDespachoRow =
    { id: Guid
      codigo: string
      orden_venta_id: Nullable<Guid>
      origen_tipo: string
      cliente_id: Nullable<Guid>
      estado: string
      movimiento_id: Nullable<Guid>
      creado_en: DateTime
      anulacion_motivo: string
      anulado_por: Nullable<Guid>
      anulado_en: Nullable<DateTime> }

[<CLIMutable>]
type LineaDespachoRow =
    { orden_despacho_id: Guid
      item: int
      producto_id: Guid
      cantidad: decimal
      unidad: string }

module VentaRepository =

    let private reconstruirOrdenVenta (row: OrdenVentaRow) (lineaRows: LineaOrdenVentaRow list) : Result<OrdenVenta, string> =
        let anulacionOpt =
            if not (String.IsNullOrWhiteSpace row.anulacion_motivo) && row.anulado_por.HasValue && row.anulado_en.HasValue then
                Some { Fecha = row.anulado_en.Value
                       Motivo = row.anulacion_motivo
                       AnuladoPor = EmpleadoId row.anulado_por.Value }
            else None

        let estadoRes = EstadoVenta.desdeTexto row.estado anulacionOpt
        match estadoRes with
        | Error err ->
            eprintfn "[INTEGRIDAD] VentaRepository: estado venta inválido '%s' para orden %s: %A" row.estado (row.id.ToString()) err
            Error (sprintf "Estado de venta inválido para orden %s" (row.id.ToString()))
        | Ok estado ->

        let lineas =
            lineaRows
            |> List.sortBy (fun lr -> lr.item)
            |> List.choose (fun lr ->
                let unidadRes = UnidadMedida.desdeTexto lr.unidad
                match unidadRes with
                | Error _ -> None
                | Ok unidad ->
                    match Cantidad.reconstruir lr.cantidad unidad with
                    | Error _ -> None
                    | Ok cant ->
                        Some { ProductoId = ProductoId lr.producto_id
                               Cantidad = cant
                               PrecioUnitario = lr.precio_unitario })

        let codVenta = CodigoVenta.reconstruir row.codigo

        Ok { Id = OrdenId row.id
             Codigo = codVenta
             ProformaOrigenId = ProformaId row.proforma_id
             ClienteId = ClienteId row.cliente_id
             Fecha = row.fecha
             ResponsableId = EmpleadoId row.responsable_id
             Lineas = lineas
             Total = row.total
             Moneda = row.moneda
             Estado = estado
             CreadoEn = row.creado_en }

    let private reconstruirOrdenDespacho (row: OrdenDespachoRow) (lineaRows: LineaDespachoRow list) : Result<OrdenDespacho, string> =
        let anulacionOpt =
            if not (String.IsNullOrWhiteSpace row.anulacion_motivo) && row.anulado_por.HasValue && row.anulado_en.HasValue then
                Some { Fecha = row.anulado_en.Value
                       Motivo = row.anulacion_motivo
                       AnuladoPor = EmpleadoId row.anulado_por.Value }
            else None

        let movIdOpt = if row.movimiento_id.HasValue then Some row.movimiento_id.Value else None
        let estadoRes = EstadoDespacho.desdeTexto row.estado movIdOpt anulacionOpt
        match estadoRes with
        | Error err ->
            eprintfn "[INTEGRIDAD] VentaRepository: estado despacho inválido '%s' para orden %s: %A" row.estado (row.id.ToString()) err
            Error (sprintf "Estado de despacho inválido para orden %s" (row.id.ToString()))
        | Ok estado ->

        let origenRes =
            let oGuid = if row.orden_venta_id.HasValue then row.orden_venta_id.Value else row.id
            OrigenDespacho.desdeTexto row.origen_tipo oGuid

        match origenRes with
        | Error err ->
            Error (sprintf "Origen de despacho inválido: %A" err)
        | Ok origen ->

        let clienteIdOpt = if row.cliente_id.HasValue then Some (ClienteId row.cliente_id.Value) else None
        let codDespacho = CodigoDespacho.reconstruir row.codigo

        let lineas =
            lineaRows
            |> List.sortBy (fun lr -> lr.item)
            |> List.choose (fun lr ->
                let unidadRes = UnidadMedida.desdeTexto lr.unidad
                match unidadRes with
                | Error _ -> None
                | Ok unidad ->
                    match Cantidad.reconstruir lr.cantidad unidad with
                    | Error _ -> None
                    | Ok cant ->
                        Some { Referencia = ProductoId lr.producto_id; Cantidad = cant } : LineaSolicitud option)

        Ok { Id = OrdenId row.id
             Codigo = codDespacho
             Origen = origen
             ClienteId = clienteIdOpt
             Lineas = lineas
             Estado = estado
             CreadoEn = row.creado_en }

    let private errorToString (err: DomainError) : string =
        match err with
        | CantidadInvalida m | UnidadIncompatible m | CodigoLoteInvalido m
        | NombreInvalido m | CIInvalido m | ValorRequerido m | SecuenciaInvalida m
        | StockInsuficiente m | SimbolosNoPermitidos m | LetrasNoPermitidas m
        | EmpleadoDuplicado m | SinAnalisisLaboratorio m | LoteRechazado m
        | PorcentajeInvalido m | FechaInvalida m | OperacionInvalida m
        | PermisoDenegado m -> m

    /// Configura la sesión de BD para la transacción actual.
    /// Q6: lock_timeout previene cuelgues indefinidos ante bloqueos imprevistos.
    let private configurarTx (conn: NpgsqlConnection) (tx: NpgsqlTransaction) =
        conn.Execute("SET LOCAL lock_timeout = '5s';", null, tx) |> ignore
        conn.Execute("SET LOCAL statement_timeout = '30s';", null, tx) |> ignore

    let private esTransitorio (ex: exn) =
        match ex with
        | :? PostgresException as pg ->
            pg.SqlState = "40P01" || pg.SqlState = "40001" || pg.SqlState = "55P03"
        | _ -> false

    /// Secuencia real por (tipo, periodo). El UPSERT toma el lock de fila
    /// hasta el commit; dos confirmaciones del mes se serializan aquí.
    /// Si la tx rollbackea, el incremento también se revierte → sin huecos.
    let private siguienteSecuencia
        (conn: NpgsqlConnection) (tx: NpgsqlTransaction)
        (tipo: string) (periodo: int) : Async<int> =
        conn.ExecuteScalarAsync<int>(
            """
            INSERT INTO secuencia_documento (tipo, periodo, ultimo)
            VALUES (@tipo, @periodo, 1)
            ON CONFLICT (tipo, periodo)
            DO UPDATE SET ultimo = secuencia_documento.ultimo + 1
            RETURNING ultimo
            """,
            {| tipo = tipo; periodo = periodo |}, tx)
        |> Async.AwaitTask

    let private periodo (fecha: DateOnly) = fecha.Year * 100 + fecha.Month

    /// Construye `CodigoVenta.generar` a partir de la secuencia real.
    /// La función de dominio sigue siendo pura; el lado I/O vive aquí.
    let private generarCodigo
        (tipo: string) (fecha: DateOnly) (secuencia: int)
        : Result<string, string> =
        if tipo = "OV" then
            CodigoVenta.generar fecha secuencia
            |> Result.map CodigoVenta.valor
            |> Result.mapError errorToString
        else
            CodigoDespacho.generar fecha secuencia
            |> Result.map CodigoDespacho.valor
            |> Result.mapError errorToString

    /// Inserción atómica transaccional de Confirmación de Venta (RNF05 / OBS-03)
    /// L1: Convierte Proforma a 'Convertida' de manera optimista (WHERE id=@id AND estado='Vigente')
    /// L2: Secuencia atómica de códigos de venta y despacho
    /// L6: Lock + re-reserva autoritativa con dominio (Lote.reservar bajo FOR UPDATE)
    /// Inserta orden_venta, linea_orden_venta, orden_despacho, linea_despacho
    /// Persiste lotes actualizados con stock y reserva coherentes
    let confirmarVentaTx
        (ordenVentaBase: OrdenVenta)
        (ordenDespachoBase: OrdenDespacho)
        (reservasLotes: (LoteId * Cantidad) list)
        : Async<Result<CodigoVenta * CodigoDespacho, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                configurarTx conn tx
                let (OrdenId ovId) = ordenVentaBase.Id
                let (ProformaId pId) = ordenVentaBase.ProformaOrigenId
                let (ClienteId cId) = ordenVentaBase.ClienteId
                let (EmpleadoId eId) = ordenVentaBase.ResponsableId
                let (OrdenId odId) = ordenDespachoBase.Id

                // ── L1: claim de proforma (allow-list: solo Vigente) ──
                let sqlUpdateProforma = """
                    UPDATE proforma
                    SET estado = 'Convertida', orden_venta_id = @ovId
                    WHERE id = @pId AND estado = 'Vigente'
                """
                let! affectedProforma =
                    conn.ExecuteAsync(sqlUpdateProforma, {| ovId = ovId; pId = pId |}, tx)
                    |> Async.AwaitTask

                if affectedProforma = 0 then
                    tx.Rollback()
                    return Error "La proforma ya fue convertida previamente o no se encuentra en estado Vigente."
                else

                // ── L2: secuencia real por (tipo, periodo) ───────
                let per = periodo ordenVentaBase.Fecha
                let! secOV  = siguienteSecuencia conn tx "OV"  per
                let! secDSP = siguienteSecuencia conn tx "DSP" per

                match generarCodigo "OV" ordenVentaBase.Fecha secOV,
                      generarCodigo "DSP" ordenVentaBase.Fecha secDSP with
                | Error msg, _ | _, Error msg ->
                    tx.Rollback()
                    return Error (sprintf "No se pudo generar código: %s" msg)
                | Ok cvStr, Ok cdStr ->

                let codigoVenta    = CodigoVenta.reconstruir cvStr
                let codigoDespacho = CodigoDespacho.reconstruir cdStr

                // ── L6: lock de lotes en orden determinista ─────
                let loteIds = reservasLotes |> List.map fst
                let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                match lotesLockedRes with
                | Error msg ->
                    tx.Rollback()
                    return Error (sprintf "Error de integridad al bloquear lotes: %s" msg)
                | Ok lotesLocked ->

                let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList

                let rec reservarTodo = function
                    | [] -> Ok []
                    | ((loteId, cantidad) :: rest) ->
                        match Map.tryFind loteId lotesPorId with
                        | None ->
                            Error (sprintf "Lote %A no encontrado tras lock" loteId)
                        | Some lf ->
                            match Lote.reservar cantidad lf with
                            | Error err ->
                                Error (sprintf "Re-reserva falló en lote '%s': %s"
                                        (CodigoLote.valor lf.Codigo) (errorToString err))
                            | Ok lr ->
                                match reservarTodo rest with
                                | Error e -> Error e
                                | Ok acc  -> Ok (lr :: acc)

                match reservarTodo reservasLotes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok lotesReservados ->

                // ── Construir entidades finales con códigos reales ──
                let ordenVentaFinal    = { ordenVentaBase    with Codigo = codigoVenta    }
                let ordenDespachoFinal = { ordenDespachoBase with Codigo = codigoDespacho }

                // ── INSERT cabecera OrdenVenta ────────────────────
                let sqlCabVenta = """
                    INSERT INTO orden_venta (id, codigo, proforma_id, cliente_id, fecha, responsable_id, total, moneda, estado, creado_en)
                    VALUES (@id, @codigo, @proforma_id, @cliente_id, @fecha, @responsable_id, @total, @moneda, @estado, @creado_en)
                """
                let paramsCabVenta = {|
                    id = ovId
                    codigo = CodigoVenta.valor codigoVenta
                    proforma_id = pId
                    cliente_id = cId
                    fecha = ordenVentaFinal.Fecha
                    responsable_id = eId
                    total = ordenVentaFinal.Total
                    moneda = ordenVentaFinal.Moneda
                    estado = EstadoVenta.aTexto ordenVentaFinal.Estado
                    creado_en = ordenVentaFinal.CreadoEn
                |}
                let! affectedVenta = conn.ExecuteAsync(sqlCabVenta, paramsCabVenta, tx) |> Async.AwaitTask
                if affectedVenta <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo insertar orden_venta (affectedRows=%d)" affectedVenta)
                else

                // ── INSERT líneas de OrdenVenta ────────────────────
                let sqlLineaVenta = """
                    INSERT INTO linea_orden_venta (orden_venta_id, item, producto_id, cantidad, unidad, precio_unitario, subtotal)
                    VALUES (@orden_venta_id, @item, @producto_id, @cantidad, @unidad, @precio_unitario, @subtotal)
                """
                let mutable errorLineaVenta : string option = None
                for (i, linea) in ordenVentaFinal.Lineas |> List.mapi (fun i l -> (i + 1, l)) do
                    if errorLineaVenta.IsNone then
                        let (ProductoId pid) = linea.ProductoId
                        let! affected =
                            conn.ExecuteAsync(sqlLineaVenta, {|
                                orden_venta_id = ovId
                                item           = i
                                producto_id    = pid
                                cantidad       = linea.Cantidad.Valor
                                unidad         = UnidadMedida.aTexto linea.Cantidad.Unidad
                                precio_unitario = linea.PrecioUnitario
                                subtotal       = Cotizacion.subtotal linea
                            |}, tx)
                            |> Async.AwaitTask
                        if affected <> 1 then
                            errorLineaVenta <- Some (sprintf "INSERT linea_orden_venta item=%d affected=%d" i affected)

                match errorLineaVenta with
                | Some msg ->
                    tx.Rollback()
                    return Error msg
                | None ->

                // ── INSERT cabecera OrdenDespacho ─────────────────
                let sqlCabDespacho = """
                    INSERT INTO orden_despacho (id, codigo, orden_venta_id, origen_tipo, cliente_id, estado, movimiento_id, creado_en)
                    VALUES (@id, @codigo, @orden_venta_id, @origen_tipo, @cliente_id, @estado, @movimiento_id, @creado_en)
                """
                let clienteDespachoGuid =
                    ordenDespachoFinal.ClienteId
                    |> Option.map (fun (ClienteId id) -> box id)
                    |> Option.defaultValue (box DBNull.Value)

                let! affectedDesp =
                    conn.ExecuteAsync(sqlCabDespacho, {|
                        id = odId
                        codigo = CodigoDespacho.valor codigoDespacho
                        orden_venta_id = box ovId
                        origen_tipo = OrigenDespacho.aTexto ordenDespachoFinal.Origen
                        cliente_id = clienteDespachoGuid
                        estado = EstadoDespacho.aTexto ordenDespachoFinal.Estado
                        movimiento_id = box DBNull.Value
                        creado_en = ordenDespachoFinal.CreadoEn
                    |}, tx)
                    |> Async.AwaitTask
                if affectedDesp <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo insertar orden_despacho (affectedRows=%d)" affectedDesp)
                else

                // ── INSERT líneas de OrdenDespacho ─────────────────
                let sqlLineaDespacho = """
                    INSERT INTO linea_despacho (orden_despacho_id, item, producto_id, cantidad, unidad)
                    VALUES (@orden_despacho_id, @item, @producto_id, @cantidad, @unidad)
                """
                let mutable errorLineaDesp : string option = None
                for (i, linea) in ordenDespachoFinal.Lineas |> List.mapi (fun i l -> (i + 1, l)) do
                    if errorLineaDesp.IsNone then
                        let (ProductoId pid) = linea.Referencia
                        let! affected =
                            conn.ExecuteAsync(sqlLineaDespacho, {|
                                orden_despacho_id = odId
                                item        = i
                                producto_id = pid
                                cantidad    = linea.Cantidad.Valor
                                unidad      = UnidadMedida.aTexto linea.Cantidad.Unidad
                            |}, tx)
                            |> Async.AwaitTask
                        if affected <> 1 then
                            errorLineaDesp <- Some (sprintf "INSERT linea_despacho item=%d affected=%d" i affected)

                match errorLineaDesp with
                | Some msg ->
                    tx.Rollback()
                    return Error msg
                | None ->

                // ── Persistir lotes reservados ─────────────────────
                let rec persistirLotes = function
                    | [] -> async { return Ok () }
                    | lote :: rest ->
                        async {
                            let! r = LoteRepository.actualizarStockYEstadoTx conn tx lote
                            match r with
                            | Ok () -> return! persistirLotes rest
                            | Error msg -> return Error msg
                        }
                let! persistRes = persistirLotes lotesReservados
                match persistRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->
                    match! ReservaStockRepository.insertarBatch conn tx ordenVentaFinal.Id reservasLotes with
                    | Error msg ->
                        tx.Rollback()
                        return Error msg
                    | Ok () ->
                        tx.Commit()
                        return Ok (codigoVenta, codigoDespacho)
            with ex ->
                try tx.Rollback() with _ -> ()
                if esTransitorio ex then
                    eprintfn "[VentaRepository.confirmarVentaTx] Error transitorio: %s" ex.Message
                    return Error (sprintf "Error transitorio: %s" ex.Message)
                else
                    eprintfn "[VentaRepository.confirmarVentaTx] Error: %s" ex.Message
                    return Error ex.Message
        }

    let obtenerVentaPorId (id: OrdenId) : Async<OrdenVenta option> =
        async {
            use conn = DbConnection.crear ()
            let (OrdenId oid) = id

            let sqlCab = "SELECT * FROM orden_venta WHERE id = @id"
            let! cabRows = conn.QueryAsync<OrdenVentaRow>(sqlCab, {| id = oid |}) |> Async.AwaitTask

            match cabRows |> Seq.tryHead with
            | None -> return None
            | Some row ->
                let sqlLineas = "SELECT * FROM linea_orden_venta WHERE orden_venta_id = @oid ORDER BY item"
                let! lineaRows = conn.QueryAsync<LineaOrdenVentaRow>(sqlLineas, {| oid = oid |}) |> Async.AwaitTask
                let lineas = lineaRows |> Seq.toList

                match reconstruirOrdenVenta row lineas with
                | Ok v -> return Some v
                | Error msg ->
                    eprintfn "[INTEGRIDAD] VentaRepository.obtenerVentaPorId: %s" msg
                    return None
        }

    let listarVentas
        (clienteIdFilter: string option)
        (estadoFilter: string option)
        (fechaFilter: string option)
        : Async<OrdenVenta list> =
        async {
            use conn = DbConnection.crear ()

            let mutable whereParts = ResizeArray<string>()
            let dynParams = System.Collections.Generic.Dictionary<string, obj>()

            match clienteIdFilter with
            | Some c when not (String.IsNullOrWhiteSpace c) ->
                match Guid.TryParse(c) with
                | true, cGuid ->
                    whereParts.Add("v.cliente_id = @cliente_id")
                    dynParams.["cliente_id"] <- box cGuid
                | false, _ -> ()
            | _ -> ()

            match estadoFilter with
            | Some e when not (String.IsNullOrWhiteSpace e) ->
                whereParts.Add("v.estado = @estado")
                dynParams.["estado"] <- box e
            | _ -> ()

            match fechaFilter with
            | Some f when not (String.IsNullOrWhiteSpace f) ->
                match DateOnly.TryParse(f) with
                | true, fDate ->
                    whereParts.Add("v.fecha = @fecha")
                    dynParams.["fecha"] <- box fDate
                | false, _ -> ()
            | _ -> ()

            let whereClause =
                if whereParts.Count = 0 then ""
                else "WHERE " + String.Join(" AND ", whereParts)

            let sql = sprintf "SELECT * FROM orden_venta v %s ORDER BY v.creado_en DESC" whereClause

            let dapperParams = DynamicParameters()
            for kv in dynParams do
                dapperParams.Add(kv.Key, kv.Value)

            let! cabRows = conn.QueryAsync<OrdenVentaRow>(sql, dapperParams) |> Async.AwaitTask
            let cabList = cabRows |> Seq.toList

            if List.isEmpty cabList then
                return []
            else

            let ids = cabList |> List.map (fun r -> r.id)
            let sqlLineas = "SELECT * FROM linea_orden_venta WHERE orden_venta_id = ANY(@ids) ORDER BY orden_venta_id, item"
            let! lineaRows = conn.QueryAsync<LineaOrdenVentaRow>(sqlLineas, {| ids = ids |> List.toArray |}) |> Async.AwaitTask
            let lineasPorVenta =
                lineaRows
                |> Seq.groupBy (fun lr -> lr.orden_venta_id)
                |> Map.ofSeq

            return
                cabList
                |> List.choose (fun row ->
                    let lineas = lineasPorVenta |> Map.tryFind row.id |> Option.map Seq.toList |> Option.defaultValue []
                    match reconstruirOrdenVenta row lineas with
                    | Ok v -> Some v
                    | Error msg ->
                        eprintfn "[INTEGRIDAD] VentaRepository.listarVentas id=%s: %s" (row.id.ToString()) msg
                        None)
        }

    let obtenerDespachoPorId (id: OrdenId) : Async<OrdenDespacho option> =
        async {
            use conn = DbConnection.crear ()
            let (OrdenId oid) = id

            let sqlCab = "SELECT * FROM orden_despacho WHERE id = @id"
            let! cabRows = conn.QueryAsync<OrdenDespachoRow>(sqlCab, {| id = oid |}) |> Async.AwaitTask

            match cabRows |> Seq.tryHead with
            | None -> return None
            | Some row ->
                let sqlLineas = "SELECT * FROM linea_despacho WHERE orden_despacho_id = @oid ORDER BY item"
                let! lineaRows = conn.QueryAsync<LineaDespachoRow>(sqlLineas, {| oid = oid |}) |> Async.AwaitTask
                let lineas = lineaRows |> Seq.toList

                match reconstruirOrdenDespacho row lineas with
                | Ok d -> return Some d
                | Error msg ->
                    eprintfn "[INTEGRIDAD] VentaRepository.obtenerDespachoPorId: %s" msg
                    return None
        }

    let obtenerDespachoPorVentaId (ventaId: OrdenId) : Async<OrdenDespacho option> =
        async {
            use conn = DbConnection.crear ()
            let (OrdenId vid) = ventaId

            let sqlCab = "SELECT * FROM orden_despacho WHERE orden_venta_id = @vid"
            let! cabRows = conn.QueryAsync<OrdenDespachoRow>(sqlCab, {| vid = vid |}) |> Async.AwaitTask

            match cabRows |> Seq.tryHead with
            | None -> return None
            | Some row ->
                let sqlLineas = "SELECT * FROM linea_despacho WHERE orden_despacho_id = @oid ORDER BY item"
                let! lineaRows = conn.QueryAsync<LineaDespachoRow>(sqlLineas, {| oid = row.id |}) |> Async.AwaitTask
                let lineas = lineaRows |> Seq.toList

                match reconstruirOrdenDespacho row lineas with
                | Ok d -> return Some d
                | Error msg ->
                    eprintfn "[INTEGRIDAD] VentaRepository.obtenerDespachoPorVentaId: %s" msg
                    return None
        }

    let listarDespachos (estadoFilter: string option) : Async<OrdenDespacho list> =
        async {
            use conn = DbConnection.crear ()

            let whereClause =
                match estadoFilter with
                | Some e when not (String.IsNullOrWhiteSpace e) -> "WHERE d.estado = @estado"
                | _ -> ""

            let sql = sprintf "SELECT * FROM orden_despacho d %s ORDER BY d.creado_en DESC" whereClause
            let p = {| estado = defaultArg estadoFilter "" |}

            let! cabRows = conn.QueryAsync<OrdenDespachoRow>(sql, p) |> Async.AwaitTask
            let cabList = cabRows |> Seq.toList

            if List.isEmpty cabList then
                return []
            else

            let ids = cabList |> List.map (fun r -> r.id)
            let sqlLineas = "SELECT * FROM linea_despacho WHERE orden_despacho_id = ANY(@ids) ORDER BY orden_despacho_id, item"
            let! lineaRows = conn.QueryAsync<LineaDespachoRow>(sqlLineas, {| ids = ids |> List.toArray |}) |> Async.AwaitTask
            let lineasPorDespacho =
                lineaRows
                |> Seq.groupBy (fun lr -> lr.orden_despacho_id)
                |> Map.ofSeq

            return
                cabList
                |> List.choose (fun row ->
                    let lineas = lineasPorDespacho |> Map.tryFind row.id |> Option.map Seq.toList |> Option.defaultValue []
                    match reconstruirOrdenDespacho row lineas with
                    | Ok d -> Some d
                    | Error msg ->
                        eprintfn "[INTEGRIDAD] VentaRepository.listarDespachos id=%s: %s" (row.id.ToString()) msg
                        None)
        }

    /// Confirmación física de salida de inventario (Almacén - RN12 / RNF05 / OBS-03 / Fase 4)
    /// Consume atómicamente las reservas activas registradas en reserva_stock para la venta.
    /// L3 (orden_venta) → L4 (orden_despacho) → L5 (reserva_stock) → L6 (lotes) → updates
    let confirmarDespachoFisicoTx
        (despachoId: OrdenId)
        (movimiento: MovimientoInventario)
        (contraparteNombre: string option)
        : Async<Result<LineaMovimientoDto list, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                configurarTx conn tx

                let (OrdenId did) = despachoId
                let (MovimientoId mId) = movimiento.Id
                let (EmpleadoId eId) = movimiento.Responsable

                // ── Paso 0: leer sin lock el orden_venta_id (inmutable) ──
                let! cab =
                    conn.QueryFirstOrDefaultAsync<{| id: Guid; orden_venta_id: Nullable<Guid>; origen_tipo: string |}>(
                        "SELECT id, orden_venta_id, origen_tipo FROM orden_despacho WHERE id = @did",
                        {| did = did |}, tx)
                    |> Async.AwaitTask
                if isNull (box cab) then
                    tx.Rollback()
                    return Error "Orden de despacho no encontrada"
                else

                let origenTipoNorm =
                    if isNull cab.origen_tipo then "" else cab.origen_tipo.Trim().ToLowerInvariant()

                match origenTipoNorm with
                | "deventa" | "venta" when not cab.orden_venta_id.HasValue ->
                    tx.Rollback()
                    return Error "Integridad: despacho DeVenta sin orden_venta_id"
                | "deventa" | "venta" when cab.orden_venta_id.HasValue ->
                    let ovId = OrdenId cab.orden_venta_id.Value

                    // ── L3: lock orden_venta + re-verificar estado ──
                    let! vRow =
                        conn.QueryFirstOrDefaultAsync<OrdenVentaRow>(
                            "SELECT * FROM orden_venta WHERE id = @ovId FOR UPDATE",
                            {| ovId = cab.orden_venta_id.Value |}, tx)
                        |> Async.AwaitTask
                    if isNull (box vRow) then
                        tx.Rollback()
                        return Error "Orden de venta no encontrada"
                    elif vRow.estado <> "Confirmada" then
                        tx.Rollback()
                        return Error (sprintf "La orden de venta no está Confirmada (estado: '%s')" vRow.estado)
                    else

                    // ── L4: lock orden_despacho + re-verificar estado ──
                    let! dRow =
                        conn.QueryFirstOrDefaultAsync<OrdenDespachoRow>(
                            "SELECT * FROM orden_despacho WHERE id = @did FOR UPDATE",
                            {| did = did |}, tx)
                        |> Async.AwaitTask
                    if isNull (box dRow) || dRow.estado <> "Pendiente" then
                        tx.Rollback()
                        return Error (
                            sprintf "La orden de despacho no está Pendiente (estado: '%s')"
                                (if isNull (box dRow) then "?" else dRow.estado))
                    elif not dRow.orden_venta_id.HasValue || dRow.orden_venta_id.Value <> cab.orden_venta_id.Value then
                        // invariante: orden_venta_id es inmutable (B5)
                        tx.Rollback()
                        return Error "Inconsistencia: orden_venta_id cambió durante la tx"
                    else

                    // ── L5: lock reservas activas (ORDER BY id) ─────
                    let! reservasRes = ReservaStockRepository.listarActivasPorVenta conn tx ovId
                    match reservasRes with
                    | Error msg ->
                        tx.Rollback()
                        return Error msg
                    | Ok [] ->
                        tx.Rollback()
                        return Error "No hay reservas activas para la orden"
                    | Ok reservas ->

                    // ── L6: lock lotes (ORDER BY id) ─────────────────
                    let loteIds = reservas |> List.map fst
                    let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                    match lotesLockedRes with
                    | Error msg ->
                        tx.Rollback()
                        return Error (sprintf "Integridad al bloquear lotes: %s" msg)
                    | Ok lotesLocked ->

                    let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList

                    let rec despacharTodo = function
                        | [] -> Ok []
                        | ((loteId, cantidad) :: rest) ->
                            match Map.tryFind loteId lotesPorId with
                            | None -> Error (sprintf "Lote %A no encontrado tras lock" loteId)
                            | Some lf ->
                                match Lote.despacharReserva cantidad lf with
                                | Error err ->
                                    Error (sprintf "Despacho falló en lote '%s': %s"
                                              (CodigoLote.valor lf.Codigo) (errorToString err))
                                | Ok ld ->
                                    match despacharTodo rest with
                                    | Error e -> Error e
                                    | Ok acc  -> Ok (ld :: acc)

                    match despacharTodo reservas with
                    | Error msg ->
                        tx.Rollback()
                        return Error msg
                    | Ok lotesDespachados ->

                    // ── INSERT movimiento_inventario ──────────────────
                    let cRefGuid =
                        match movimiento.Tipo with
                        | Salida (Venta (ClienteId cid)) -> box cid
                        | _ -> box DBNull.Value

                    let! affectedMov =
                        conn.ExecuteAsync(
                            """
                            INSERT INTO movimiento_inventario (id, fecha, responsable_id, tipo, motivo, orden_origen_id, contraparte_ref, contraparte_nombre, observaciones)
                            VALUES (@id, @fecha, @responsable_id, @tipo, @motivo, @orden_origen_id, @contraparte_ref, @contraparte_nombre, @observaciones)
                            """,
                            {| id = mId
                               fecha = movimiento.Fecha
                               responsable_id = eId
                               tipo = "Salida"
                               motivo = "Venta"
                               orden_origen_id = box did
                               contraparte_ref = cRefGuid
                               contraparte_nombre = contraparteNombre |> Option.defaultValue null
                               observaciones = movimiento.Observaciones |> Option.defaultValue null |},
                            tx)
                        |> Async.AwaitTask
                    if affectedMov <> 1 then
                        tx.Rollback()
                        return Error (sprintf "INSERT movimiento_inventario affected=%d" affectedMov)
                    else

                    // ── INSERT linea_movimiento (con affectedRows check) ──
                    let sqlLineaMov = """
                        INSERT INTO linea_movimiento (id, movimiento_id, lote_id, cantidad, unidad, observaciones)
                        VALUES (@id, @movimiento_id, @lote_id, @cantidad, @unidad, NULL)
                    """
                    let mutable errorLineaMov : string option = None
                    for (LoteId lId, cant) in reservas do
                        if errorLineaMov.IsNone then
                            let! affected =
                                conn.ExecuteAsync(sqlLineaMov, {|
                                    id = Identidad.nuevo ()
                                    movimiento_id = mId
                                    lote_id = lId
                                    cantidad = cant.Valor
                                    unidad = UnidadMedida.aTexto cant.Unidad
                                |}, tx)
                                |> Async.AwaitTask
                            if affected <> 1 then
                                errorLineaMov <- Some (sprintf "INSERT linea_movimiento lote=%s affected=%d" (lId.ToString()) affected)

                    match errorLineaMov with
                    | Some msg ->
                        tx.Rollback()
                        return Error msg
                    | None ->

                    // ── UPDATE lotes ─────────────────────────────────
                    let rec persistirLotes = function
                        | [] -> async { return Ok () }
                        | lote :: rest ->
                            async {
                                let! r = LoteRepository.actualizarStockYEstadoTx conn tx lote
                                match r with
                                | Ok () -> return! persistirLotes rest
                                | Error msg -> return Error msg
                            }
                    let! persistRes = persistirLotes lotesDespachados
                    match persistRes with
                    | Error msg ->
                        tx.Rollback()
                        return Error msg
                    | Ok () ->

                    // ── UPDATE reserva_stock → Despachada ───────────
                    let! marcRes = ReservaStockRepository.marcarDespachada conn tx ovId movimiento.Id
                    match marcRes with
                    | Error msg ->
                        tx.Rollback()
                        return Error msg
                    | Ok () ->

                    // ── UPDATE orden_despacho (guard) ───────────────
                    let! affectedDesp =
                        conn.ExecuteAsync(
                            "UPDATE orden_despacho SET estado = 'Despachado', movimiento_id = @mId WHERE id = @did AND estado = 'Pendiente'",
                            {| did = did
                               mId = mId |},
                            tx)
                        |> Async.AwaitTask
                    if affectedDesp <> 1 then
                        tx.Rollback()
                        return Error (sprintf "UPDATE orden_despacho affected=%d" affectedDesp)
                    else

                    // ── UPDATE orden_venta (guard) ──────────────────
                    let! affectedVenta =
                        conn.ExecuteAsync(
                            "UPDATE orden_venta SET estado = 'Despachada' WHERE id = @ovId AND estado = 'Confirmada'",
                            {| ovId = cab.orden_venta_id.Value |},
                            tx)
                        |> Async.AwaitTask
                    if affectedVenta <> 1 then
                        tx.Rollback()
                        return Error (sprintf "UPDATE orden_venta affected=%d" affectedVenta)
                    else

                    let lineasDto =
                        reservas
                        |> List.map (fun (lId, cant) ->
                            let (LoteId gId) = lId
                            let codigoStr =
                                lotesPorId |> Map.tryFind lId
                                |> Option.map (fun l -> CodigoLote.valor l.Codigo)
                                |> Option.defaultValue ""
                            { LoteId = gId.ToString()
                              CodigoLote = codigoStr
                              Cantidad = cant.Valor
                              Unidad = UnidadMedida.aTexto cant.Unidad })

                    tx.Commit()
                    return Ok lineasDto

                | _ ->
                    tx.Rollback()
                    return Error (sprintf "Despacho para origen_tipo '%s' aún no soportado (F11)" cab.origen_tipo)

            with ex ->
                try tx.Rollback() with _ -> ()
                if esTransitorio ex then
                    eprintfn "[VentaRepository.confirmarDespachoFisicoTx] Error transitorio: %s" ex.Message
                    return Error (sprintf "Error transitorio: %s" ex.Message)
                else
                    eprintfn "[VentaRepository.confirmarDespachoFisicoTx] Error: %s" ex.Message
                    return Error (sprintf "Error al confirmar despacho: %s" ex.Message)
        }

    /// Anulación de orden de venta con liberación transaccional de reservas (Fase 4 / RN04)
    /// L3 (orden_venta) → L4 (orden_despacho) → L5 (reserva_stock) → L6 (lotes) → updates
    let anularVentaTx
        (ventaId: OrdenId)
        (motivo: string)
        (anuladoPor: EmpleadoId)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                configurarTx conn tx

                let (OrdenId vid) = ventaId
                let (EmpleadoId uId) = anuladoPor

                // ── L3: lock orden_venta + re-verificar estado ──
                let! vRow =
                    conn.QueryFirstOrDefaultAsync<OrdenVentaRow>(
                        "SELECT * FROM orden_venta WHERE id = @vid FOR UPDATE",
                        {| vid = vid |}, tx)
                    |> Async.AwaitTask
                if isNull (box vRow) then
                    tx.Rollback()
                    return Error (sprintf "Orden de venta %s no encontrada" (vid.ToString()))
                elif vRow.estado <> "Confirmada" then
                    tx.Rollback()
                    return Error (sprintf "La orden de venta no está en estado Confirmada (estado actual: '%s')" vRow.estado)
                else

                // ── L4: lock orden_despacho (por orden_venta_id) ──
                let! dRowOpt =
                    conn.QueryFirstOrDefaultAsync<OrdenDespachoRow>(
                        "SELECT * FROM orden_despacho WHERE orden_venta_id = @vid FOR UPDATE",
                        {| vid = vid |}, tx)
                    |> Async.AwaitTask

                // ── L5: lock reservas activas (ORDER BY id) ───────
                let! reservasRes = ReservaStockRepository.listarActivasPorVenta conn tx ventaId
                match reservasRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok [] ->
                    tx.Rollback()
                    return Error "La venta no tiene reservas activas; posible inconsistencia. Contactar soporte."
                | Ok reservas ->

                // ── L6: lock lotes (ORDER BY id) ─────────────────
                let loteIds = reservas |> List.map fst
                let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                match lotesLockedRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok lotesLocked ->

                let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList

                let rec liberarTodo = function
                    | [] -> Ok []
                    | ((loteId, cantidad) :: rest) ->
                        match Map.tryFind loteId lotesPorId with
                        | None ->
                            Error (sprintf "Lote %A no encontrado tras lock" loteId)
                        | Some lf ->
                            match Lote.liberarReserva cantidad lf with
                            | Error err ->
                                Error (sprintf "Liberación de reserva falló en lote '%s': %s"
                                        (CodigoLote.valor lf.Codigo) (errorToString err))
                            | Ok lr ->
                                match liberarTodo rest with
                                | Error e -> Error e
                                | Ok acc -> Ok (lr :: acc)

                match liberarTodo reservas with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok lotesLiberados ->

                // ── UPDATE lotes ─────────────────────────────────
                let rec persistirLotes = function
                    | [] -> async { return Ok () }
                    | lote :: rest ->
                        async {
                            let! r = LoteRepository.actualizarStockYEstadoTx conn tx lote
                            match r with
                            | Ok () -> return! persistirLotes rest
                            | Error msg -> return Error msg
                        }
                let! persistRes = persistirLotes lotesLiberados
                match persistRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->

                // ── UPDATE reserva_stock → Liberada ──────────────
                let! marcRes = ReservaStockRepository.marcarLiberada conn tx ventaId
                match marcRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->

                // ── UPDATE orden_venta (guard) ───────────────────
                let! affectedVenta =
                    conn.ExecuteAsync(
                        """
                        UPDATE orden_venta
                        SET estado = 'Anulada',
                            anulacion_motivo = @m,
                            anulado_por = @u,
                            anulado_en = NOW()
                        WHERE id = @vid AND estado = 'Confirmada'
                        """,
                        {| vid = vid; m = motivo; u = uId |}, tx)
                    |> Async.AwaitTask
                if affectedVenta <> 1 then
                    tx.Rollback()
                    return Error (sprintf "UPDATE orden_venta afectó %d filas (esperado 1)" affectedVenta)
                else

                // ── UPDATE orden_despacho si existe ───────────────
                if isNull (box dRowOpt) then
                    tx.Commit()
                    return Ok ()
                else
                    let! affectedDesp =
                        conn.ExecuteAsync(
                            """
                            UPDATE orden_despacho
                            SET estado = 'Anulado',
                                anulacion_motivo = @m,
                                anulado_por = @u,
                                anulado_en = NOW()
                            WHERE id = @did AND estado = 'Pendiente'
                            """,
                            {| did = dRowOpt.id; m = motivo; u = uId |}, tx)
                        |> Async.AwaitTask
                    if affectedDesp <> 1 then
                        tx.Rollback()
                        return Error (sprintf "UPDATE orden_despacho afectó %d filas (esperado 1)" affectedDesp)
                    else
                        tx.Commit()
                        return Ok ()

            with ex ->
                try tx.Rollback() with _ -> ()
                if esTransitorio ex then
                    eprintfn "[VentaRepository.anularVentaTx] Error transitorio: %s" ex.Message
                    return Error (sprintf "Error transitorio: %s" ex.Message)
                else
                    eprintfn "[VentaRepository.anularVentaTx] Error: %s" ex.Message
                    return Error (sprintf "Error al anular venta: %s" ex.Message)
        }
