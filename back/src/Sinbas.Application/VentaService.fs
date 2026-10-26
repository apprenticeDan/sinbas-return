namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F8: Confirmación de Venta y Despacho Físico
// DTOs y Servicio de Aplicación (RF-08 | CU-11)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type LineaVentaDto =
    { ProductoId: string
      NombreProducto: string
      Cantidad: decimal
      Unidad: string
      PrecioUnitario: decimal
      Subtotal: decimal }

[<CLIMutable>]
type OrdenVentaDto =
    { Id: string
      Codigo: string
      ProformaOrigenId: string
      ClienteId: string
      ClienteNombre: string
      Fecha: string
      ResponsableId: string
      Lineas: LineaVentaDto list
      Total: decimal
      Moneda: string
      Estado: string
      CreadoEn: string
      AnulacionMotivo: string option
      AnuladoPor: string option
      AnuladoEn: string option }

[<CLIMutable>]
type ConfirmarVentaRequest =
    { /// Obligatorio si la proforma origen fue emitida como anónima / libre (D4)
      ClienteId: string option }

[<CLIMutable>]
type AnularVentaRequest =
    { Motivo: string }

[<CLIMutable>]
type LineaDespachoDto =
    { ProductoId: string
      NombreProducto: string
      Cantidad: decimal
      Unidad: string }

[<CLIMutable>]
type SugerenciaLoteDespachoDto =
    { ProductoId: string
      NombreProducto: string
      CantidadRequerida: decimal
      Unidad: string
      LotesSugeridos: LineaMovimientoDto list }

[<CLIMutable>]
type OrdenDespachoDto =
    { Id: string
      Codigo: string
      OrdenVentaId: string option
      CodigoVenta: string option
      OrigenTipo: string
      ClienteId: string option
      ClienteNombre: string option
      Estado: string
      MovimientoId: string option
      CreadoEn: string
      Lineas: LineaDespachoDto list }

[<CLIMutable>]
type ConfirmarDespachoRequest =
    { Observaciones: string option }

[<CLIMutable>]
type VentaConfirmadaDto =
    { Venta: OrdenVentaDto
      Despacho: OrdenDespachoDto }

[<CLIMutable>]
type DespachoConfirmadoDto =
    { DespachoId: string
      CodigoDespacho: string
      MovimientoId: string
      Fecha: string
      LineasDespachadas: LineaMovimientoDto list
      Mensaje: string }

module VentaService =

    let private errorToString (err: DomainError) : string =
        match err with
        | CantidadInvalida msg
        | UnidadIncompatible msg
        | CodigoLoteInvalido msg
        | NombreInvalido msg
        | CIInvalido msg
        | ValorRequerido msg
        | SecuenciaInvalida msg
        | StockInsuficiente msg
        | SimbolosNoPermitidos msg
        | LetrasNoPermitidas msg
        | EmpleadoDuplicado msg
        | SinAnalisisLaboratorio msg
        | LoteRechazado msg
        | PorcentajeInvalido msg
        | FechaInvalida msg
        | OperacionInvalida msg -> msg

    let private desmapearUnidad (u: UnidadMedida) : string =
        UnidadMedida.aTexto u

    let toVentaDto
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (v: OrdenVenta)
        : OrdenVentaDto =
        let (OrdenId vid) = v.Id
        let (ProformaId pid) = v.ProformaOrigenId
        let (ClienteId cid) = v.ClienteId
        let (EmpleadoId eid) = v.ResponsableId

        let anulacionOpt =
            match v.Estado with
            | EstadoVenta.Anulada an -> Some an
            | _ -> None

        let lineasDto =
            v.Lineas
            |> List.map (fun l ->
                let (ProductoId lpid) = l.ProductoId
                { ProductoId = lpid.ToString()
                  NombreProducto = nombreProductoResolver l.ProductoId
                  Cantidad = l.Cantidad.Valor
                  Unidad = desmapearUnidad l.Cantidad.Unidad
                  PrecioUnitario = l.PrecioUnitario
                  Subtotal = Cotizacion.subtotal l })

        { Id = vid.ToString()
          Codigo = CodigoVenta.valor v.Codigo
          ProformaOrigenId = pid.ToString()
          ClienteId = cid.ToString()
          ClienteNombre = nombreClienteResolver v.ClienteId
          Fecha = v.Fecha.ToString("yyyy-MM-dd")
          ResponsableId = eid.ToString()
          Lineas = lineasDto
          Total = v.Total
          Moneda = v.Moneda
          Estado = EstadoVenta.aTexto v.Estado
          CreadoEn = v.CreadoEn.ToString("yyyy-MM-ddTHH:mm:ssZ")
          AnulacionMotivo = anulacionOpt |> Option.map (fun a -> a.Motivo)
          AnuladoPor = anulacionOpt |> Option.map (fun a -> let (EmpleadoId uid) = a.AnuladoPor in uid.ToString())
          AnuladoEn = anulacionOpt |> Option.map (fun a -> a.Fecha.ToString("yyyy-MM-ddTHH:mm:ssZ")) }

    let toDespachoDto
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (codigoVentaResolver: OrdenId -> string option)
        (d: OrdenDespacho)
        : OrdenDespachoDto =
        let (OrdenId did) = d.Id
        let ordenVentaIdOpt =
            match d.Origen with
            | OrigenDespacho.DeVenta (OrdenId vid) -> Some (vid.ToString())
            | OrigenDespacho.InternaSolicitud (OrdenId vid) -> Some (vid.ToString())

        let codVentaOpt =
            match d.Origen with
            | OrigenDespacho.DeVenta vid -> codigoVentaResolver vid
            | OrigenDespacho.InternaSolicitud vid -> codigoVentaResolver vid

        let cliNombreOpt =
            d.ClienteId |> Option.map nombreClienteResolver

        let movIdOpt =
            match d.Estado with
            | EstadoDespacho.Despachado (MovimientoId mid) -> Some (mid.ToString())
            | _ -> None

        let lineasDto =
            d.Lineas
            |> List.map (fun l ->
                let (ProductoId lpid) = l.Referencia
                { ProductoId = lpid.ToString()
                  NombreProducto = nombreProductoResolver l.Referencia
                  Cantidad = l.Cantidad.Valor
                  Unidad = desmapearUnidad l.Cantidad.Unidad })

        { Id = did.ToString()
          Codigo = CodigoDespacho.valor d.Codigo
          OrdenVentaId = ordenVentaIdOpt
          CodigoVenta = codVentaOpt
          OrigenTipo = OrigenDespacho.aTexto d.Origen
          ClienteId = d.ClienteId |> Option.map (fun (ClienteId cid) -> cid.ToString())
          ClienteNombre = cliNombreOpt
          Estado = EstadoDespacho.aTexto d.Estado
          MovimientoId = movIdOpt
          CreadoEn = d.CreadoEn.ToString("yyyy-MM-ddTHH:mm:ssZ")
          Lineas = lineasDto }

    /// RF-08 / CU-11: Confirmar venta a partir de una proforma vigente
    /// Reglas:
    /// - RN14: Proforma debe estar Vigente y no vencida a la fecha actual (hoy).
    /// - D4: Exige ClienteId registrado obligatorio. Si la proforma era anónima, se toma del request.
    /// - RN04 / RN05: Re-valida en tiempo real que exista stock vendible disponible para todas las líneas.
    /// - Bug 8: Re-valida que cada producto siga apto para venta al confirmar.
    /// - OBS-03: Reserva formal de stock sobre lotes al confirmar venta (cantidad_reservada).
    /// - RNF05: Convierte proforma y genera OrdenVenta + OrdenDespacho en una sola transacción atómica.
    let confirmarVenta
        (obtenerProforma: ProformaId -> Async<Proforma option>)
        (obtenerCliente: ClienteId -> Async<Cliente option>)
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (confirmarVentaTx: OrdenVenta -> OrdenDespacho -> (LoteId * Cantidad) list -> Async<Result<unit, string>>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (responsableId: Guid)
        (proformaIdRaw: string)
        (req: ConfirmarVentaRequest)
        : Async<Result<VentaConfirmadaDto, string * int>> =
        async {
            // 1. Validar ID de proforma
            match Guid.TryParse(proformaIdRaw) with
            | false, _ -> return Error ("Identificador de proforma inválido", 400)
            | true, pGuid ->
                let pId = ProformaId pGuid
                let! proformaOpt = obtenerProforma pId
                match proformaOpt with
                | None -> return Error (sprintf "No se encontró la proforma con ID '%s'" proformaIdRaw, 404)
                | Some proforma ->

                    let hoy = DateOnly.FromDateTime(DateTime.UtcNow)

                    // 2. Validar estado de la proforma (RN14 + OBS-04: no bloquea por fecha)
                    if proforma.Estado = EstadoProforma.Anulada then
                        return Error ("No se puede confirmar una proforma que ha sido anulada.", 409)
                    elif proforma.Estado = EstadoProforma.Convertida then
                        return Error ("La proforma ya fue convertida a orden de venta previamente.", 409)
                    else

                    // 3. Resolver y validar ClienteId obligatorio (D4)
                    let! clienteIdFinal =
                        async {
                            match proforma.ClienteId with
                            | Some cid -> return Ok cid
                            | None ->
                                match req.ClienteId with
                                | Some cidRaw when not (String.IsNullOrWhiteSpace cidRaw) ->
                                    match Guid.TryParse(cidRaw) with
                                    | true, cGuid -> return Ok (ClienteId cGuid)
                                    | false, _ -> return Error ("El ID de cliente proporcionado no es un UUID válido", 400)
                                | _ ->
                                    return Error ("Para confirmar la venta de una proforma anónima es obligatorio asociar un Cliente registrado (D4).", 400)
                        }

                    match clienteIdFinal with
                    | Error err -> return Error err
                    | Ok cid ->

                    let! clienteOpt = obtenerCliente cid
                    match clienteOpt with
                    | None ->
                        let (ClienteId cGuid) = cid
                        return Error (sprintf "El cliente con ID '%s' no existe en el registro." (cGuid.ToString()), 404)
                    | Some cliente when not (Cliente.estaActivo cliente) ->
                        return Error ("El cliente seleccionado se encuentra inactivo.", 400)
                    | Some cliente ->

                    // 4. Re-validar que los productos sigan existiendo y aptos para venta (bug 8)
                    let! validacionesProducto =
                        proforma.Lineas
                        |> List.map (fun linea ->
                            async {
                                let! prodOpt = obtenerProducto linea.ProductoId
                                match prodOpt with
                                | None ->
                                    let nomProd = nombreProductoResolver linea.ProductoId
                                    return Error (sprintf "Producto '%s' no encontrado en el catálogo" nomProd, 404)
                                | Some prod when not (Producto.esAptoParaVenta prod) ->
                                    let nomProd = Producto.nombreVisible prod
                                    return Error (sprintf "Producto '%s' no está apto para venta (fue desactivado después de emitir la proforma)" nomProd, 422)
                                | Some _ ->
                                    return Ok ()
                            })
                        |> Async.Parallel

                    let errorProdOpt =
                        validacionesProducto
                        |> Array.tryPick (fun r -> match r with Error e -> Some e | Ok () -> None)

                    match errorProdOpt with
                    | Some err -> return Error err
                    | None ->

                    // 5. Resolver FIFO y validar stock vendible (RN04 / RN05 / OBS-03)
                    // Se resuelve la asignación FIFO al momento de confirmar la venta para saber
                    // exactamente qué lotes y cantidades reservar atómicamente.
                    let! resolucionesFifoPorLinea =
                        proforma.Lineas
                        |> List.map (fun linea ->
                            async {
                                let! lotes = listarLotes (Some linea.ProductoId) (Some Activo)
                                let fifoRes = Fifo.resolverFIFO linea.ProductoId linea.Cantidad lotes
                                match fifoRes with
                                | Error err ->
                                    let nomProd = nombreProductoResolver linea.ProductoId
                                    return Error (sprintf "%s: %s" nomProd (errorToString err))
                                | Ok lineasMov ->
                                    // Se conserva la Cantidad (con unidad), no decimal suelto
                                    let reservasPorLote =
                                        lineasMov
                                        |> List.map (fun lm -> (lm.Referencia, lm.Cantidad))
                                    return Ok reservasPorLote
                            })
                        |> Async.Parallel

                    let erroresFifo =
                        resolucionesFifoPorLinea
                        |> Array.choose (fun r -> match r with Error e -> Some e | _ -> None)
                        |> Array.toList

                    if not (List.isEmpty erroresFifo) then
                        let detalle = String.concat "; " erroresFifo
                        return Error (sprintf "Stock insuficiente para confirmar la venta. Productos con déficit: %s" detalle, 409)
                    else

                    // Consolidar reservas por lote preservando unidad
                    // (un mismo lote puede aparecer en varias líneas; FIFO retorna Cantidad en la unidad nativa del lote)
                    let reservasLotes : (LoteId * Cantidad) list =
                        resolucionesFifoPorLinea
                        |> Array.choose (fun r -> match r with Ok rs -> Some rs | _ -> None)
                        |> Array.toList
                        |> List.concat
                        |> List.groupBy fst
                        |> List.map (fun (loteId, items) ->
                            let cantidades = items |> List.map snd
                            let unidad = (List.head cantidades).Unidad
                            let totalValor = cantidades |> List.sumBy (fun c -> c.Valor)
                            let cantConsolidada =
                                match Cantidad.reconstruir totalValor unidad with
                                | Ok c -> c
                                | Error _ -> List.head cantidades
                            (loteId, cantConsolidada))

                    // 5. Generar entidades de dominio OrdenVenta y OrdenDespacho
                    let ordenVentaId = OrdenId (Identidad.nuevo ())
                    let ordenDespachoId = OrdenId (Identidad.nuevo ())
                    let respEmpleadoId = EmpleadoId responsableId

                    // Generar códigos identificadores basados en fecha y sufijo único
                    let ticks = DateTime.UtcNow.Ticks % 10000L
                    let seqNum = int (max 1L ticks)
                    let codVenta =
                        match CodigoVenta.generar hoy seqNum with
                        | Ok c -> c
                        | Error _ -> CodigoVenta.reconstruir (sprintf "OV-%d%02d-%04d" hoy.Year hoy.Month seqNum)

                    let codDespacho =
                        match CodigoDespacho.generar hoy seqNum with
                        | Ok c -> c
                        | Error _ -> CodigoDespacho.reconstruir (sprintf "DSP-%d%02d-%04d" hoy.Year hoy.Month seqNum)

                    match OrdenVenta.crearDesdeProforma hoy ordenVentaId codVenta proforma cid respEmpleadoId with
                    | Error domErr -> return Error (errorToString domErr, 400)
                    | Ok (ordenVenta, _) ->

                    let ordenDespacho = Despacho.desdeVenta ordenDespachoId codDespacho ordenVenta

                    // 6. Ejecutar transacción atómica de persistencia con reservas (RNF05 / OBS-03)
                    let! txRes = confirmarVentaTx ordenVenta ordenDespacho reservasLotes
                    match txRes with
                    | Error msg ->
                        return Error (sprintf "Error transaccional al confirmar venta: %s" msg, 500)
                    | Ok () ->

                    let ventaDto = toVentaDto nombreProductoResolver (fun _ -> Cliente.nombreVisible cliente) ordenVenta
                    let despachoDto = toDespachoDto nombreProductoResolver (fun _ -> Cliente.nombreVisible cliente) (fun _ -> Some (CodigoVenta.valor codVenta)) ordenDespacho

                    return Ok { Venta = ventaDto; Despacho = despachoDto }
        }

    /// Listar órdenes de venta con filtros opcionales
    let listarVentas
        (listarVentasRepo: string option -> string option -> string option -> Async<OrdenVenta list>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (clienteIdFilter: string option)
        (estadoFilter: string option)
        (fechaFilter: string option)
        : Async<OrdenVentaDto list> =
        async {
            let! ventas = listarVentasRepo clienteIdFilter estadoFilter fechaFilter
            return ventas |> List.map (toVentaDto nombreProductoResolver nombreClienteResolver)
        }

    /// Obtener detalle completo de una orden de venta por ID
    let obtenerVentaPorId
        (obtenerVentaRepo: OrdenId -> Async<OrdenVenta option>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (idRaw: string)
        : Async<Result<OrdenVentaDto, string * int>> =
        async {
            match Guid.TryParse(idRaw) with
            | false, _ -> return Error ("Identificador de orden de venta inválido", 400)
            | true, vGuid ->
                let! vOpt = obtenerVentaRepo (OrdenId vGuid)
                match vOpt with
                | None -> return Error ("Orden de venta no encontrada", 404)
                | Some v -> return Ok (toVentaDto nombreProductoResolver nombreClienteResolver v)
        }

    /// Listar órdenes de despacho pendientes para el módulo de Almacén
    let listarDespachos
        (listarDespachosRepo: string option -> Async<OrdenDespacho list>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (codigoVentaResolver: OrdenId -> string option)
        (estadoFilter: string option)
        : Async<OrdenDespachoDto list> =
        async {
            let! despachos = listarDespachosRepo estadoFilter
            return despachos |> List.map (toDespachoDto nombreProductoResolver nombreClienteResolver codigoVentaResolver)
        }

    /// Obtener detalle de orden de despacho con sugerencias FIFO de lotes activos para Almacén
    let obtenerDespachoDetalle
        (obtenerDespachoRepo: OrdenId -> Async<OrdenDespacho option>)
        (obtenerVentaRepo: OrdenId -> Async<OrdenVenta option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (idRaw: string)
        : Async<Result<OrdenDespachoDto * SugerenciaLoteDespachoDto list, string * int>> =
        async {
            match Guid.TryParse(idRaw) with
            | false, _ -> return Error ("Identificador de despacho inválido", 400)
            | true, dGuid ->
                let! dOpt = obtenerDespachoRepo (OrdenId dGuid)
                match dOpt with
                | None -> return Error ("Orden de despacho no encontrada", 404)
                | Some despacho ->

                    let codVentaResolver vid =
                        let v = obtenerVentaRepo vid |> Async.RunSynchronously
                        v |> Option.map (fun ven -> CodigoVenta.valor ven.Codigo)

                    let dto = toDespachoDto nombreProductoResolver nombreClienteResolver codVentaResolver despacho

                    // TODO FASE-6: sugerencias de lotes basadas en reserva_stock
                    let! sugerencias =
                        despacho.Lineas
                        |> List.map (fun linea ->
                            async {
                                let prodId = linea.Referencia
                                return { ProductoId = (let (ProductoId pid) = prodId in pid.ToString())
                                         NombreProducto = nombreProductoResolver prodId
                                         CantidadRequerida = linea.Cantidad.Valor
                                         Unidad = desmapearUnidad linea.Cantidad.Unidad
                                         LotesSugeridos = [] }
                            })
                        |> Async.Parallel

                    return Ok (dto, Array.toList sugerencias)
        }

    /// RF-08 / CU-11: Confirmar despacho físico en Almacén
    /// Consume las reservas de stock asociadas a la orden de venta (Fase 4).
    let confirmarDespachoFisico
        (obtenerDespacho: OrdenId -> Async<OrdenDespacho option>)
        (confirmarDespachoFisicoTx: OrdenId -> MovimientoInventario -> string option -> Async<Result<LineaMovimientoDto list, string>>)
        (responsableId: Guid)
        (despachoIdRaw: string)
        (req: ConfirmarDespachoRequest)
        : Async<Result<DespachoConfirmadoDto, string * int>> =
        async {
            match Guid.TryParse(despachoIdRaw) with
            | false, _ -> return Error ("Identificador de orden de despacho inválido", 400)
            | true, dGuid ->
                let dId = OrdenId dGuid
                let! dOpt = obtenerDespacho dId
                match dOpt with
                | None -> return Error ("Orden de despacho no encontrada", 404)
                | Some despacho ->

                    if despacho.Estado <> EstadoDespacho.Pendiente then
                        return Error (sprintf "La orden de despacho no está en estado Pendiente (estado actual: '%s')" (EstadoDespacho.aTexto despacho.Estado), 409)
                    else

                    let movId = MovimientoId (Identidad.nuevo ())
                    let ahora = DateTime.UtcNow

                    let clienteRef =
                        match despacho.ClienteId with
                        | Some cid -> cid
                        | None -> ClienteId Guid.Empty

                    let movimiento : MovimientoInventario =
                        { Id = movId
                          Fecha = ahora
                          Responsable = EmpleadoId responsableId
                          Tipo = Salida (Venta clienteRef)
                          OrdenOrigen = Some dId
                          Lineas = []
                          Observaciones = req.Observaciones }

                    let! txRes = confirmarDespachoFisicoTx dId movimiento None
                    match txRes with
                    | Error msg ->
                        let statusCode = if msg.ToLowerInvariant().Contains("reserva") then 409 else 500
                        return Error (msg, statusCode)
                    | Ok lineasDto ->
                        let (MovimientoId mGuid) = movId
                        return Ok {
                            DespachoId = dGuid.ToString()
                            CodigoDespacho = CodigoDespacho.valor despacho.Codigo
                            MovimientoId = mGuid.ToString()
                            Fecha = ahora.ToString("yyyy-MM-dd HH:mm")
                            LineasDespachadas = lineasDto
                            Mensaje = "Despacho físico ejecutado y stock actualizado exitosamente."
                        }
        }

    /// Anulación de orden de venta con liberación de reservas (Fase 4 / RN04)
    let anularVenta
        (obtenerVenta: OrdenId -> Async<OrdenVenta option>)
        (anularVentaTx: OrdenId -> string -> EmpleadoId -> Async<Result<unit, string>>)
        (nombreProductoResolver: ProductoId -> string)
        (nombreClienteResolver: ClienteId -> string)
        (anuladoPor: Guid)
        (ventaIdRaw: string)
        (motivo: string)
        : Async<Result<OrdenVentaDto, string * int>> =
        async {
            match Guid.TryParse(ventaIdRaw) with
            | false, _ -> return Error ("Identificador de orden de venta inválido", 400)
            | true, vGuid ->
                let vId = OrdenId vGuid
                let! vOpt = obtenerVenta vId
                match vOpt with
                | None -> return Error ("Orden de venta no encontrada", 404)
                | Some venta ->
                    if String.IsNullOrWhiteSpace(motivo) then
                        return Error ("El motivo de anulación es obligatorio", 400)
                    else
                        match venta.Estado with
                        | EstadoVenta.Despachada ->
                            return Error ("No se puede anular una orden de venta que ya ha sido despachada", 409)
                        | EstadoVenta.Anulada _ ->
                            return Error ("La orden de venta ya se encuentra anulada", 409)
                        | EstadoVenta.Confirmada ->
                            let! txRes = anularVentaTx vId (motivo.Trim()) (EmpleadoId anuladoPor)
                            match txRes with
                            | Error msg -> return Error (msg, 500)
                            | Ok () ->
                                let! ventaRecargadaOpt = obtenerVenta vId
                                match ventaRecargadaOpt with
                                | None -> return Error ("Error al recargar venta anulada", 500)
                                | Some ventaAnulada ->
                                    return Ok (toVentaDto nombreProductoResolver nombreClienteResolver ventaAnulada)
        }
