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
    /// - RNF05: Convierte proforma y genera OrdenVenta + OrdenDespacho en una sola transacción atómica.
    let confirmarVenta
        (obtenerProforma: ProformaId -> Async<Proforma option>)
        (obtenerCliente: ClienteId -> Async<Cliente option>)
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (confirmarVentaTx: OrdenVenta -> OrdenDespacho -> Async<Result<unit, string>>)
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

                    // 2. Validar estado de la proforma (RN14)
                    match proforma.Estado with
                    | EstadoProforma.Anulada ->
                        return Error ("No se puede confirmar una proforma que ha sido anulada.", 409)
                    | EstadoProforma.Convertida ->
                        return Error ("La proforma ya fue convertida a orden de venta previamente.", 409)
                    | EstadoProforma.Vencida ->
                        return Error ("La proforma se encuentra vencida y no puede convertirse a venta.", 409)
                    | EstadoProforma.Vigente ->

                    // Verificar proyección de fecha de vencimiento a hoy
                    match proforma.FechaVencimiento with
                    | Some fv when hoy > fv ->
                        return Error (sprintf "La proforma venció el %s y no puede convertirse a venta." (fv.ToString("yyyy-MM-dd")), 409)
                    | _ ->

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

                    // 4. Re-validación de stock disponible en tiempo real para todas las líneas (RN04 / RN05)
                    let! validacionesStock =
                        proforma.Lineas
                        |> List.map (fun linea ->
                            async {
                                let! lotes = listarLotes (Some linea.ProductoId) (Some Activo)
                                let stockDispBase = Stock.disponibleParaVenta linea.ProductoId lotes
                                let cantidadReqBase = Cantidad.aUnidadBase linea.Cantidad
                                if cantidadReqBase > stockDispBase then
                                    let nomProd = nombreProductoResolver linea.ProductoId
                                    let uBase = UnidadMedida.etiqueta (UnidadMedida.unidadBase linea.Cantidad.Unidad)
                                    let msg = sprintf "%s (disponible: %.2f %s, requerido: %.2f %s)" nomProd (float stockDispBase) uBase (float cantidadReqBase) uBase
                                    return Some msg
                                else
                                    return None
                            })
                        |> Async.Parallel

                    let deficits = validacionesStock |> Array.choose id |> Array.toList
                    if not (List.isEmpty deficits) then
                        let detalle = String.concat "; " deficits
                        return Error (sprintf "Stock insuficiente para confirmar la venta. Productos con déficit: %s" detalle, 409)
                    else

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

                    // 6. Ejecutar transacción atómica de persistencia (RNF05)
                    let! txRes = confirmarVentaTx ordenVenta ordenDespacho
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

                    // Calcular sugerencias FIFO para cada línea
                    let! sugerencias =
                        despacho.Lineas
                        |> List.map (fun linea ->
                            async {
                                let prodId = linea.Referencia
                                let! lotesActivos = listarLotes (Some prodId) (Some Activo)
                                let fifoRes = Fifo.resolverFIFO prodId linea.Cantidad lotesActivos
                                let sugeridos =
                                    match fifoRes with
                                    | Error _ -> []
                                    | Ok lineasMov ->
                                        lineasMov
                                        |> List.map (fun lm ->
                                            let (LoteId lid) = lm.Referencia
                                            let codLote =
                                                lotesActivos
                                                |> List.tryFind (fun l -> l.Id = lm.Referencia)
                                                |> Option.map (fun l -> CodigoLote.valor l.Codigo)
                                                |> Option.defaultValue "Lote"
                                            { LoteId = lid.ToString()
                                              CodigoLote = codLote
                                              Cantidad = lm.Cantidad.Valor
                                              Unidad = desmapearUnidad lm.Cantidad.Unidad })

                                return { ProductoId = (let (ProductoId pid) = prodId in pid.ToString())
                                         NombreProducto = nombreProductoResolver prodId
                                         CantidadRequerida = linea.Cantidad.Valor
                                         Unidad = desmapearUnidad linea.Cantidad.Unidad
                                         LotesSugeridos = sugeridos }
                            })
                        |> Async.Parallel

                    return Ok (dto, Array.toList sugerencias)
        }

    /// RF-08 / CU-11: Confirmar despacho físico en Almacén
    /// Resuelve los lotes mediante FIFO estricto sobre lotes en estado Activo (RN12).
    /// Ejecuta la deducción física de inventario, registra el movimiento de salida (Venta)
    /// y marca tanto la orden de despacho como la orden de venta como Despachadas.
    let confirmarDespachoFisico
        (obtenerDespacho: OrdenId -> Async<OrdenDespacho option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (obtenerCliente: ClienteId -> Async<Cliente option>)
        (confirmarDespachoFisicoTx: OrdenId -> MovimientoInventario -> (LoteId * decimal) list -> string option -> Async<Result<unit, string>>)
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

                    // Resolver lotes por FIFO para cada línea
                    let! resolucionesPorLinea =
                        despacho.Lineas
                        |> List.map (fun linea ->
                            async {
                                let prodId = linea.Referencia
                                let! lotesActivos = listarLotes (Some prodId) (Some Activo)
                                let fifoRes = Fifo.resolverFIFO prodId linea.Cantidad lotesActivos
                                match fifoRes with
                                | Error err -> return Error (sprintf "Stock insuficiente para el producto %A: %s" prodId (errorToString err))
                                | Ok lineasMov ->
                                    // Calcular saldos restantes de los lotes
                                    let saldosActualizados =
                                        lineasMov
                                        |> List.choose (fun lm ->
                                            lotesActivos
                                            |> List.tryFind (fun l -> l.Id = lm.Referencia)
                                            |> Option.map (fun l ->
                                                let cantDeducidaBase = Cantidad.aUnidadBase lm.Cantidad
                                                let saldoActualBase = Cantidad.aUnidadBase l.CantidadActual
                                                let nuevoSaldoBase = max 0m (saldoActualBase - cantDeducidaBase)
                                                let esMayor = l.CantidadActual.Unidad = Kilogramo || l.CantidadActual.Unidad = Litro
                                                let nuevoSaldoEnUnidad = if esMayor then nuevoSaldoBase / 1000m else nuevoSaldoBase
                                                (l.Id, nuevoSaldoEnUnidad)))
                                    return Ok (lineasMov, saldosActualizados, lotesActivos)
                            })
                        |> Async.Parallel

                    let errores = resolucionesPorLinea |> Array.choose (fun r -> match r with Error e -> Some e | _ -> None)
                    if errores.Length > 0 then
                        return Error (errores.[0], 409)
                    else

                    let todosMovLineas =
                        resolucionesPorLinea
                        |> Array.choose (fun r -> match r with Ok (lm, _, _) -> Some lm | _ -> None)
                        |> List.concat

                    let todosSaldos =
                        resolucionesPorLinea
                        |> Array.choose (fun r -> match r with Ok (_, saldos, _) -> Some saldos | _ -> None)
                        |> List.concat

                    let todosLotes =
                        resolucionesPorLinea
                        |> Array.choose (fun r -> match r with Ok (_, _, lotes) -> Some lotes | _ -> None)
                        |> List.concat

                    // Construir MovimientoInventario
                    let movId = MovimientoId (Identidad.nuevo ())
                    let ahora = DateTime.UtcNow

                    let clienteRef =
                        match despacho.ClienteId with
                        | Some cid -> cid
                        | None -> ClienteId Guid.Empty

                    let! clienteNombreOpt =
                        async {
                            match despacho.ClienteId with
                            | Some cid ->
                                let! cOpt = obtenerCliente cid
                                return cOpt |> Option.map Cliente.nombreVisible
                            | None -> return None
                        }

                    let movimiento : MovimientoInventario =
                        { Id = movId
                          Fecha = ahora
                          Responsable = EmpleadoId responsableId
                          Tipo = Salida (Venta clienteRef)
                          OrdenOrigen = Some dId
                          Lineas = todosMovLineas
                          Observaciones = req.Observaciones }

                    // Ejecutar transacción atómica
                    let! txRes = confirmarDespachoFisicoTx dId movimiento todosSaldos clienteNombreOpt
                    match txRes with
                    | Error msg -> return Error (sprintf "Error al confirmar despacho físico: %s" msg, 500)
                    | Ok () ->

                    let lineasDto =
                        todosMovLineas
                        |> List.map (fun lm ->
                            let (LoteId lid) = lm.Referencia
                            let codLote =
                                todosLotes
                                |> List.tryFind (fun l -> l.Id = lm.Referencia)
                                |> Option.map (fun l -> CodigoLote.valor l.Codigo)
                                |> Option.defaultValue (lid.ToString().Substring(0, 8))
                            { LoteId = lid.ToString()
                              CodigoLote = codLote
                              Cantidad = lm.Cantidad.Valor
                              Unidad = desmapearUnidad lm.Cantidad.Unidad })

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
