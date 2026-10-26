namespace Sinbas.Infrastructure

open System
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
        | PorcentajeInvalido m | FechaInvalida m | OperacionInvalida m -> m

    /// Inserción atómica transaccional de Confirmación de Venta (RNF05 / OBS-03)
    /// 1. Convierte Proforma a 'Convertida' de manera optimista (WHERE id=@id AND estado='Vigente')
    /// 2. Lock + re-reserva autoritativa con dominio (Lote.reservar bajo FOR UPDATE)
    /// 3. Inserta orden_venta, linea_orden_venta, orden_despacho, linea_despacho
    /// 4. Persiste lotes actualizados con stock y reserva coherentes
    let confirmarVentaTx
        (ordenVenta: OrdenVenta)
        (ordenDespacho: OrdenDespacho)
        (reservasLotes: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                let (OrdenId ovId) = ordenVenta.Id
                let (ProformaId pId) = ordenVenta.ProformaOrigenId
                let (ClienteId cId) = ordenVenta.ClienteId
                let (EmpleadoId eId) = ordenVenta.ResponsableId

                // 1. Convertir proforma atómicamente asegurando estado Vigente
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

                // 2. Lock + re-reserva autoritativa con dominio
                let loteIds = reservasLotes |> List.map fst
                let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                match lotesLockedRes with
                | Error msg -> tx.Rollback(); return Error msg
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
                                | Ok acc -> Ok (lr :: acc)

                match reservarTodo reservasLotes with
                | Error msg -> tx.Rollback(); return Error msg
                | Ok lotesReservados ->

                // 3. Insertar cabecera OrdenVenta
                let sqlCabVenta = """
                    INSERT INTO orden_venta (id, codigo, proforma_id, cliente_id, fecha, responsable_id, total, moneda, estado, creado_en)
                    VALUES (@id, @codigo, @proforma_id, @cliente_id, @fecha, @responsable_id, @total, @moneda, @estado, @creado_en)
                """
                let paramsCabVenta = {|
                    id = ovId
                    codigo = CodigoVenta.valor ordenVenta.Codigo
                    proforma_id = pId
                    cliente_id = cId
                    fecha = ordenVenta.Fecha
                    responsable_id = eId
                    total = ordenVenta.Total
                    moneda = ordenVenta.Moneda
                    estado = EstadoVenta.aTexto ordenVenta.Estado
                    creado_en = ordenVenta.CreadoEn
                |}
                let! insVenta = conn.ExecuteAsync(sqlCabVenta, paramsCabVenta, tx) |> Async.AwaitTask
                if insVenta <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo insertar orden_venta (rows=%d)" insVenta)
                else

                // 4. Insertar líneas de OrdenVenta
                let sqlLineaVenta = """
                    INSERT INTO linea_orden_venta (orden_venta_id, item, producto_id, cantidad, unidad, precio_unitario, subtotal)
                    VALUES (@orden_venta_id, @item, @producto_id, @cantidad, @unidad, @precio_unitario, @subtotal)
                """
                for (i, linea) in ordenVenta.Lineas |> List.mapi (fun i l -> (i + 1, l)) do
                    let (ProductoId pid) = linea.ProductoId
                    do! conn.ExecuteAsync(sqlLineaVenta, {|
                            orden_venta_id = ovId
                            item = i
                            producto_id = pid
                            cantidad = linea.Cantidad.Valor
                            unidad = UnidadMedida.aTexto linea.Cantidad.Unidad
                            precio_unitario = linea.PrecioUnitario
                            subtotal = Cotizacion.subtotal linea
                        |}, tx)
                        |> Async.AwaitTask |> Async.Ignore

                // 5. Insertar cabecera OrdenDespacho
                let (OrdenId odId) = ordenDespacho.Id
                let sqlCabDespacho = """
                    INSERT INTO orden_despacho (id, codigo, orden_venta_id, origen_tipo, cliente_id, estado, movimiento_id, creado_en)
                    VALUES (@id, @codigo, @orden_venta_id, @origen_tipo, @cliente_id, @estado, @movimiento_id, @creado_en)
                """
                let clienteDespachoGuid =
                    ordenDespacho.ClienteId
                    |> Option.map (fun (ClienteId id) -> box id)
                    |> Option.defaultValue (box DBNull.Value)

                let! insDesp =
                    conn.ExecuteAsync(sqlCabDespacho, {|
                        id = odId
                        codigo = CodigoDespacho.valor ordenDespacho.Codigo
                        orden_venta_id = box ovId
                        origen_tipo = OrigenDespacho.aTexto ordenDespacho.Origen
                        cliente_id = clienteDespachoGuid
                        estado = EstadoDespacho.aTexto ordenDespacho.Estado
                        movimiento_id = box DBNull.Value
                        creado_en = ordenDespacho.CreadoEn
                    |}, tx)
                    |> Async.AwaitTask
                if insDesp <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo insertar orden_despacho (rows=%d)" insDesp)
                else

                // 6. Insertar líneas de OrdenDespacho
                let sqlLineaDespacho = """
                    INSERT INTO linea_despacho (orden_despacho_id, item, producto_id, cantidad, unidad)
                    VALUES (@orden_despacho_id, @item, @producto_id, @cantidad, @unidad)
                """
                for (i, linea) in ordenDespacho.Lineas |> List.mapi (fun i l -> (i + 1, l)) do
                    let (ProductoId pid) = linea.Referencia
                    do! conn.ExecuteAsync(sqlLineaDespacho, {|
                            orden_despacho_id = odId
                            item = i
                            producto_id = pid
                            cantidad = linea.Cantidad.Valor
                            unidad = UnidadMedida.aTexto linea.Cantidad.Unidad
                        |}, tx)
                        |> Async.AwaitTask |> Async.Ignore

                // 7. Persistir lotes reservados (con affectedRows check)
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
                | Error msg -> tx.Rollback(); return Error msg
                | Ok () ->
                    tx.Commit()
                    return Ok ()
            with ex ->
                tx.Rollback()
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

    /// Confirmación física de salida de inventario (Almacén - RN12 / FIFO / RNF05 / OBS-03)
    /// Ejecuta atómicamente:
    /// 1. Lock orden_despacho + lotes afectados con FOR UPDATE
    /// 2. Aplica Lote.despacharReserva (descuento físico + liberación de reserva por dominio)
    /// 3. Inserta el movimiento de inventario (Salida / Venta) y sus lineas_movimiento
    /// 4. Cierra orden de despacho y orden de venta
    let confirmarDespachoFisicoTx
        (despachoId: OrdenId)
        (movimiento: MovimientoInventario)
        (deducciones: (LoteId * Cantidad) list)
        (contraparteNombre: string option)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                let (OrdenId did) = despachoId
                let (MovimientoId mId) = movimiento.Id
                let (EmpleadoId eId) = movimiento.Responsable

                // 1. Lock orden_despacho (verificar estado)
                let sqlCheckDesp = "SELECT * FROM orden_despacho WHERE id = @did FOR UPDATE"
                let! despRows = conn.QueryAsync<OrdenDespachoRow>(sqlCheckDesp, {| did = did |}, tx) |> Async.AwaitTask
                match despRows |> Seq.tryHead with
                | None -> tx.Rollback(); return Error "Orden de despacho no encontrada"
                | Some despRow when despRow.estado <> "Pendiente" ->
                    tx.Rollback()
                    return Error (sprintf "La orden de despacho no está Pendiente (estado actual: '%s')" despRow.estado)
                | Some despRow ->

                // 2. Lock lotes y despachar con dominio
                let loteIds = deducciones |> List.map fst
                let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                match lotesLockedRes with
                | Error msg -> tx.Rollback(); return Error msg
                | Ok lotesLocked ->

                let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList

                let rec despacharTodo = function
                    | [] -> Ok []
                    | ((loteId, cantidad) :: rest) ->
                        match Map.tryFind loteId lotesPorId with
                        | None ->
                            Error (sprintf "Lote %A no encontrado tras lock" loteId)
                        | Some lf ->
                            match Lote.despacharReserva cantidad lf with
                            | Error err ->
                                Error (sprintf "Despacho falló en lote '%s': %s"
                                        (CodigoLote.valor lf.Codigo) (errorToString err))
                            | Ok ld ->
                                match despacharTodo rest with
                                | Error e -> Error e
                                | Ok acc -> Ok (ld :: acc)

                match despacharTodo deducciones with
                | Error msg -> tx.Rollback(); return Error msg
                | Ok lotesDespachados ->

                // 3. Insertar movimiento_inventario
                let sqlInsertMov = """
                    INSERT INTO movimiento_inventario (id, fecha, responsable_id, tipo, motivo, orden_origen_id, contraparte_ref, contraparte_nombre, observaciones)
                    VALUES (@id, @fecha, @responsable_id, @tipo, @motivo, @orden_origen_id, @contraparte_ref, @contraparte_nombre, @observaciones)
                """
                let cRefGuid =
                    match movimiento.Tipo with
                    | Salida (Venta (ClienteId cid)) -> box cid
                    | _ -> box DBNull.Value

                let! insMov =
                    conn.ExecuteAsync(sqlInsertMov, {|
                        id = mId
                        fecha = movimiento.Fecha
                        responsable_id = eId
                        tipo = "Salida"
                        motivo = "Venta"
                        orden_origen_id = box did
                        contraparte_ref = cRefGuid
                        contraparte_nombre = contraparteNombre |> Option.defaultValue null
                        observaciones = movimiento.Observaciones |> Option.defaultValue null
                    |}, tx)
                    |> Async.AwaitTask
                if insMov <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo insertar movimiento_inventario (rows=%d)" insMov)
                else

                // 4. Insertar líneas de movimiento
                let sqlInsertLineaMov = """
                    INSERT INTO linea_movimiento (id, movimiento_id, lote_id, cantidad, unidad, observaciones)
                    VALUES (@id, @movimiento_id, @lote_id, @cantidad, @unidad, @observaciones)
                """
                for linea in movimiento.Lineas do
                    let (LoteId lId) = linea.Referencia
                    do! conn.ExecuteAsync(sqlInsertLineaMov, {|
                            id = Identidad.nuevo ()
                            movimiento_id = mId
                            lote_id = lId
                            cantidad = linea.Cantidad.Valor
                            unidad = UnidadMedida.aTexto linea.Cantidad.Unidad
                            observaciones = null
                        |}, tx)
                        |> Async.AwaitTask |> Async.Ignore

                // 5. Persistir lotes (decremento físico + reserva por dominio)
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
                | Error msg -> tx.Rollback(); return Error msg
                | Ok () ->

                // 6. Actualizar orden_despacho
                let sqlUpdateDespacho = """
                    UPDATE orden_despacho
                    SET estado = 'Despachado', movimiento_id = @mId
                    WHERE id = @did AND estado = 'Pendiente'
                """
                let! updDesp = conn.ExecuteAsync(sqlUpdateDespacho, {| did = did; mId = mId |}, tx) |> Async.AwaitTask
                if updDesp <> 1 then
                    tx.Rollback()
                    return Error (sprintf "UPDATE orden_despacho afectó %d filas" updDesp)
                else

                // 7. Actualizar orden_venta (si aplica)
                if despRow.orden_venta_id.HasValue then
                    let! updVenta =
                        conn.ExecuteAsync(
                            "UPDATE orden_venta SET estado = 'Despachada' WHERE id = @ovId",
                            {| ovId = despRow.orden_venta_id.Value |}, tx)
                        |> Async.AwaitTask
                    if updVenta <> 1 then
                        tx.Rollback()
                        return Error (sprintf "UPDATE orden_venta afectó %d filas" updVenta)
                    else
                        tx.Commit()
                        return Ok ()
                else
                    tx.Commit()
                    return Ok ()
            with ex ->
                tx.Rollback()
                eprintfn "[VentaRepository.confirmarDespachoFisicoTx] Error: %s" ex.Message
                return Error ex.Message
        }
