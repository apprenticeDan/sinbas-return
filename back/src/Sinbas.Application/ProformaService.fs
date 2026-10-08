namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F7: Elaboración de Proformas y Cotizaciones
// DTOs y Servicio de Aplicación
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type LineaCotizadaDto =
    { ProductoId: string
      NombreProducto: string
      Cantidad: decimal
      Unidad: string
      PrecioUnitario: decimal
      Subtotal: decimal }

[<CLIMutable>]
type ProformaDto =
    { Id: string
      Fecha: string
      CreadoEn: string
      ResponsableId: string
      ClienteId: string option
      ClienteNombre: string
      Estado: string
      EstadoProyectado: string
      FechaVencimiento: string option
      Moneda: string
      Lineas: LineaCotizadaDto list
      Total: decimal
      Leyenda: string
      OrdenVentaId: string option
      AnulacionMotivo: string option
      AnuladoPor: string option
      AnuladoEn: string option
      Observaciones: string option }

[<CLIMutable>]
type LineaCotizadaRequest =
    { ProductoId: string
      Cantidad: decimal
      Unidad: string }

[<CLIMutable>]
type CrearProformaRequest =
    { ClienteId: string option
      ClienteNombreLibre: string option
      Lineas: LineaCotizadaRequest list
      FechaVencimiento: string option
      Moneda: string option
      Observaciones: string option }

[<CLIMutable>]
type AnularProformaRequest =
    { Motivo: string }

// ─────────────────────────────────────────────────────────────
// Tipo guardado para persistencia (sin estado proyectado)
// ─────────────────────────────────────────────────────────────

type ProformaGuardada = Proforma

// ─────────────────────────────────────────────────────────────
// Servicio de Aplicación: ProformaService
// ─────────────────────────────────────────────────────────────

module ProformaService =

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

    let private mapearUnidad (uStr: string) : UnidadMedida =
        match (if isNull uStr then "" else uStr.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Gramo
        | "kilogramo" | "kg" -> Kilogramo
        | "mililitro" | "ml" -> Mililitro
        | "litro" | "l" -> Litro
        | _ -> UnidadDiscreta

    let toDto (nombreProductoResolver: ProductoId -> string) (p: Proforma) : ProformaDto =
        let (ProformaId pid) = p.Id
        let (UsuarioId uid) = p.ResponsableId
        let hoy = DateOnly.FromDateTime(DateTime.UtcNow)
        let estadoProyectado = Proforma.proyectarEstado hoy p

        let clienteNombre =
            match p.ClienteNombreLibre with
            | Some n -> n
            | None -> defaultArg (p.ClienteId |> Option.map (fun (ClienteId cid) -> cid.ToString())) "Cliente anónimo"

        let lineasDto =
            p.Lineas |> List.map (fun l ->
                let (ProductoId lpid) = l.ProductoId
                { ProductoId = lpid.ToString()
                  NombreProducto = nombreProductoResolver l.ProductoId
                  Cantidad = l.Cantidad.Valor
                  Unidad = UnidadMedida.aTexto l.Cantidad.Unidad
                  PrecioUnitario = l.PrecioUnitario
                  Subtotal = Cotizacion.subtotal l })

        { Id = pid.ToString()
          Fecha = p.Fecha.ToString("yyyy-MM-dd")
          CreadoEn = p.CreadoEn.ToString("yyyy-MM-ddTHH:mm:ssZ")
          ResponsableId = uid.ToString()
          ClienteId = p.ClienteId |> Option.map (fun (ClienteId cid) -> cid.ToString())
          ClienteNombre = clienteNombre
          Estado = EstadoProforma.aTexto p.Estado
          EstadoProyectado = EstadoProforma.aTexto estadoProyectado
          FechaVencimiento = p.FechaVencimiento |> Option.map (fun fv -> fv.ToString("yyyy-MM-dd"))
          Moneda = p.Moneda
          Lineas = lineasDto
          Total = p.Total
          Leyenda = p.Leyenda
          OrdenVentaId = p.OrdenVentaId |> Option.map (fun (OrdenId oid) -> oid.ToString())
          AnulacionMotivo = p.AnulacionMotivo
          AnuladoPor = p.AnuladoPor |> Option.map (fun (UsuarioId uid) -> uid.ToString())
          AnuladoEn = p.AnuladoEn |> Option.map (fun dt -> dt.ToString("yyyy-MM-ddTHH:mm:ssZ"))
          Observaciones = p.Observaciones }

    /// Elaborar una proforma nueva (RF-07 / CU-10)
    /// Valida: cliente existe (si se envía ID), productos activos con precio (RN16),
    /// congela precios del catálogo (RN13), valida stock disponible (RN04).
    let elaborarProforma
        (obtenerCliente: ClienteId -> Async<Cliente option>)
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (obtenerStockProducto: ProductoId -> Async<decimal>)
        (insertarProforma: Proforma -> Async<Result<unit, string>>)
        (responsableId: Guid)
        (req: CrearProformaRequest)
        : Async<Result<ProformaDto, string * int>> =
        async {
            // 1. Validar que la lista no esté vacía
            if isNull (box req.Lineas) || List.isEmpty req.Lineas then
                return Error ("La proforma debe contener al menos una línea", 400)
            else

            // 2. Resolver cliente si se envía ClienteId
            let! clienteIdValidado =
                async {
                    match req.ClienteId with
                    | Some cidRaw when not (String.IsNullOrWhiteSpace cidRaw) ->
                        match Guid.TryParse(cidRaw) with
                        | false, _ -> return Error (sprintf "ID de cliente inválido: '%s'" cidRaw, 400)
                        | true, guid ->
                            let! cOpt = obtenerCliente (ClienteId guid)
                            match cOpt with
                            | None -> return Error (sprintf "No se encontró el cliente con ID '%s'" cidRaw, 404)
                            | Some c ->
                                if not (Cliente.estaActivo c) then
                                    return Error ("El cliente especificado está inactivo", 400)
                                else
                                    return Ok (Some (ClienteId guid))
                    | _ -> return Ok None
                }

            match clienteIdValidado with
            | Error err -> return Error err
            | Ok clienteId ->

            // Si no hay clienteId y no hay nombre libre, asignar nombre genérico
            let clienteNombreLibre =
                match clienteId with
                | Some _ -> req.ClienteNombreLibre
                | None ->
                    match req.ClienteNombreLibre with
                    | Some n when not (String.IsNullOrWhiteSpace n) -> Some n
                    | _ -> Some "Cliente sin registro"

            // 3. Resolver cada línea: validar producto, congelar precio, verificar stock
            let! lineasResult =
                req.Lineas
                |> List.map (fun lr ->
                    async {
                        match Guid.TryParse(lr.ProductoId) with
                        | false, _ -> return Error (sprintf "ID de producto inválido: '%s'" lr.ProductoId, 400)
                        | true, pGuid ->
                            let prodId = ProductoId pGuid
                            let! optProd = obtenerProducto prodId
                            match optProd with
                            | None ->
                                return Error (sprintf "Producto no encontrado: '%s'" lr.ProductoId, 404)
                            | Some prod ->
                                // RN16: solo productos activos con precio para venta
                                if not (Producto.esAptoParaVenta prod) then
                                    return Error (sprintf "El producto '%s' no está activo para venta o no tiene precio oficial" (Producto.nombreVisible prod), 422)
                                else

                                // RN13: congelar precio oficial
                                let precioOficial =
                                    match prod.PrecioOficial with
                                    | Some po -> po.Valor
                                    | None -> 0m // No debería llegar aquí por esAptoParaVenta

                                let unidad = mapearUnidad lr.Unidad
                                match Cantidad.crear lr.Cantidad unidad with
                                | Error err -> return Error (errorToString err, 400)
                                | Ok cant ->
                                    if lr.Cantidad <= 0m then
                                        return Error ("La cantidad debe ser mayor a cero", 400)
                                    else

                                    // RN04: validar stock disponible en tiempo real
                                    let! stockDisp = obtenerStockProducto prodId
                                    let cantBase = Cantidad.aUnidadBase cant
                                    if cantBase > stockDisp then
                                        return Error (
                                            sprintf "Stock insuficiente para '%s': disponible %.2f, solicitado %.2f (en unidad base)"
                                                (Producto.nombreVisible prod) stockDisp cantBase,
                                            409)
                                    else
                                        let linea : LineaCotizada = { ProductoId = prodId; Cantidad = cant; PrecioUnitario = precioOficial }
                                        return Ok (prodId, Producto.nombreVisible prod, linea)
                    })
                |> Async.Parallel

            let errores = lineasResult |> Array.choose (fun r -> match r with Error e -> Some e | _ -> None)
            if errores.Length > 0 then
                return Error (errores.[0])
            else

            let lineasValidadas = lineasResult |> Array.choose (fun r -> match r with Ok (_, _, lc) -> Some lc | _ -> None) |> Array.toList
            let nombresMap =
                lineasResult
                |> Array.choose (fun r -> match r with Ok (pid, nom, _) -> Some (pid, nom) | _ -> None)
                |> Map.ofArray

            let nombreResolver pid = Map.tryFind pid nombresMap |> Option.defaultValue "?"

            // 4. Construir proforma de dominio
            let fecha = DateOnly.FromDateTime(DateTime.UtcNow)
            let fvOpt =
                match req.FechaVencimiento with
                | Some fvStr ->
                    match DateOnly.TryParse(fvStr) with
                    | true, fv -> Some fv
                    | false, _ -> None
                | None -> None

            let proformaId = ProformaId (Identidad.nuevo ())
            let responsable = UsuarioId responsableId

            match Proforma.crear proformaId responsable clienteId clienteNombreLibre lineasValidadas fecha fvOpt req.Moneda req.Observaciones with
            | Error err -> return Error (errorToString err, 400)
            | Ok proforma ->

            // 5. Persistir
            let! insertRes = insertarProforma proforma
            match insertRes with
            | Error msg -> return Error (msg, 500)
            | Ok () ->

            return Ok (toDto nombreResolver proforma)
        }

    /// Obtener una proforma por ID
    let obtenerProformaPorId
        (obtenerPorId: ProformaId -> Async<Proforma option>)
        (nombreProductoResolver: ProductoId -> string)
        (idRaw: string)
        : Async<Result<ProformaDto, string * int>> =
        async {
            match Guid.TryParse idRaw with
            | false, _ -> return Error ("ID de proforma inválido", 400)
            | true, guid ->
                let! pOpt = obtenerPorId (ProformaId guid)
                match pOpt with
                | None -> return Error ("Proforma no encontrada", 404)
                | Some p -> return Ok (toDto nombreProductoResolver p)
        }

    /// Listar proformas con filtros opcionales
    let listarProformas
        (listarEnRepo: string option -> string option -> Async<Proforma list>)
        (nombreProductoResolver: ProductoId -> string)
        (estadoFilter: string option)
        (clienteIdFilter: string option)
        : Async<ProformaDto list> =
        async {
            let! proformas = listarEnRepo estadoFilter clienteIdFilter
            return proformas |> List.map (toDto nombreProductoResolver)
        }

    /// Anular una proforma
    let anularProforma
        (obtenerPorId: ProformaId -> Async<Proforma option>)
        (actualizarEstado: Proforma -> Async<Result<unit, string>>)
        (nombreProductoResolver: ProductoId -> string)
        (responsableId: Guid)
        (idRaw: string)
        (req: AnularProformaRequest)
        : Async<Result<ProformaDto, string * int>> =
        async {
            match Guid.TryParse idRaw with
            | false, _ -> return Error ("ID de proforma inválido", 400)
            | true, guid ->
                let! pOpt = obtenerPorId (ProformaId guid)
                match pOpt with
                | None -> return Error ("Proforma no encontrada", 404)
                | Some p ->
                    let motivo = if isNull req.Motivo then "" else req.Motivo
                    match Proforma.anular motivo (UsuarioId responsableId) p with
                    | Error err -> return Error (errorToString err, 400)
                    | Ok pAnulada ->
                        let! updateRes = actualizarEstado pAnulada
                        match updateRes with
                        | Error msg -> return Error (msg, 500)
                        | Ok () -> return Ok (toDto nombreProductoResolver pAnulada)
        }
