namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// DTOs de Inventario (F4 / F5 / F8 / F9)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type LineaMovimientoDto =
    { LoteId: string
      CodigoLote: string
      Cantidad: decimal
      Unidad: string }

[<CLIMutable>]
type MovimientoDto =
    { Id: string
      Fecha: string
      ResponsableId: string
      Tipo: string
      Motivo: string
      ContraparteNombre: string
      Departamento: string
      Solicitante: string
      Observaciones: string
      Lineas: LineaMovimientoDto list }

[<CLIMutable>]
type RegistrarIngresoRequest =
    { ProductoId: string
      LoteId: string
      Descripcion: string
      Categoria: string
      TipoIngreso: string
      Cantidad: decimal
      Unidad: string
      Procedencia: string
      Observaciones: string
      Fecha: string }

[<CLIMutable>]
type MetadataMovimiento =
    { ContraparteNombre: string option
      Departamento: string option
      Solicitante: string option }

[<CLIMutable>]
type RegistrarEgresoRequest =
    { /// UUID del producto a egresar. Si está vacío se resuelve por descripción.
      ProductoId: string
      /// Descripción o nombre visible del producto (búsqueda de respaldo).
      Descripcion: string
      /// Tipo de egreso: Venta | Merma | UsoLabor | UsoVivero | Intercambio
      TipoEgreso: string
      /// Cantidad a egresar (en la unidad declarada).
      Cantidad: decimal
      Unidad: string
      /// Para Venta: nombre del cliente (extensible a ClienteId con F6).
      ContraparteNombre: string
      /// Para UsoInterno: departamento solicitante (extensible con organigrama F9).
      Departamento: string
      /// Para UsoInterno: nombre del solicitante interno.
      Solicitante: string
      Observaciones: string
      Fecha: string }

[<CLIMutable>]
type StockLoteDto =
    { LoteId: string
      Codigo: string
      ProductoId: string
      NombreProducto: string
      FechaIngreso: string
      Estado: string
      StockGramos: decimal
      StockDisplay: decimal
      Unidad: string }

[<CLIMutable>]
type StockProductoDto =
    { ProductoId: string
      NombreProducto: string
      StockTotalGramos: decimal
      StockDisponibleVentaGramos: decimal
      Lotes: StockLoteDto list }

// ─────────────────────────────────────────────────────────────
// Servicio de Aplicación: InventoryService
// ─────────────────────────────────────────────────────────────

module InventoryService =

    let private mapearUnidad (uStr: string) : Unidad =
        match uStr with
        | "Gramo" -> Gramo
        | "Kilogramo" -> Kilogramo
        | "Unidad_" -> Unidad_
        | _ -> Kilogramo

    let private desmapearUnidad (u: Unidad) : string =
        match u with
        | Gramo -> "Gramo"
        | Kilogramo -> "Kilogramo"
        | Unidad_ -> "Unidad_"
        | Bolsa _ -> "Bolsa"

    // ─────────────────────────────────────────────────────────
    // EXTENSIBILITY: Búsqueda y Validación de Clientes (F6)
    // Cuando F6 esté activa, la contraparte para egresos por Venta
    // resolverá el ClienteId mediante búsqueda multivariable
    // (nombre, apellido, NIT, teléfono, email).
    // Actualmente se preserva como texto libre en ContraparteNombre.
    // ─────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────
    // EXTENSIBILITY: Egresos para Uso Interno (F9)
    // Se registran depto y solicitante (cliente interno).
    // Próximas iteraciones validarán estos campos contra
    // centros de costos u organigrama de la institución.
    // ─────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────
    // EXTENSIBILITY: Validación de Calidad / Laboratorio (F3)
    // Actualmente un lote Activo se considera apto sin requerir
    // dictamen previo. Con F3 se introducirá estado PendienteLab.
    // ─────────────────────────────────────────────────────────

    let registrarIngreso
        (obtenerProductoPorId: ProductoId -> Async<Producto option>)
        (listarProductos: unit -> Async<Producto list>)
        (obtenerLotePorId: LoteId -> Async<Lote option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (insertarLote: Lote -> Async<unit>)
        (actualizarLoteStock: Lote -> Async<unit>)
        (insertarMovimiento: MovimientoInventario -> string option -> string option -> string option -> Async<unit>)
        (responsableId: Guid)
        (req: RegistrarIngresoRequest)
        : Async<Result<MovimientoDto, string>> =
        async {
            // 1. Validar cantidad
            if req.Cantidad <= 0m then
                return Error "La cantidad de ingreso debe ser mayor a cero"
            else
                let unidad = mapearUnidad req.Unidad
                let cantRes = Cantidad.crear req.Cantidad unidad

                match cantRes with
                | Error err -> return Error (sprintf "Cantidad inválida: %A" err)
                | Ok cantidadIngreso ->
                    // 2. Resolver el Producto
                    let! optProducto =
                        async {
                            match Guid.TryParse(req.ProductoId) with
                            | true, pGuid -> return! obtenerProductoPorId (ProductoId pGuid)
                            | false, _ ->
                                // Búsqueda por descripción / nombre si no viene UUID exacto
                                if not (String.IsNullOrWhiteSpace req.Descripcion) then
                                    let! todos = listarProductos ()
                                    let encontrado =
                                        todos
                                        |> List.tryFind (fun p ->
                                            String.Equals(Producto.nombreVisible p, req.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)
                                            || match p.Categoria with
                                               | Semilla(nc, _) -> String.Equals(nc.Epiteto, req.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)
                                               | _ -> false)
                                    return encontrado
                                else
                                    return None
                        }

                    match optProducto with
                    | None ->
                        return Error (sprintf "No se encontró el producto especificado '%s' en el catálogo" (defaultArg (Option.ofObj req.Descripcion) req.ProductoId))
                    | Some producto ->
                        let prodId = producto.Base.Id
                        let fechaMov =
                            match DateTime.TryParse(req.Fecha) with
                            | true, dt -> dt
                            | false, _ -> DateTime.UtcNow
                        let fechaIngresoDateOnly = DateOnly.FromDateTime(fechaMov)

                        // 3. Resolver o crear el Lote asociado
                        let! optLoteFinal =
                            async {
                                match Guid.TryParse(req.LoteId) with
                                | true, lGuid ->
                                    let! lOpt = obtenerLotePorId (LoteId lGuid)
                                    match lOpt with
                                    | Some l ->
                                        // Aumentar stock del lote existente
                                        let nuevaCant = { Valor = l.CantidadActual.Valor + req.Cantidad; Unidad = l.CantidadActual.Unidad }
                                        let loteActualizado = { l with CantidadActual = nuevaCant; Estado = Activo }
                                        do! actualizarLoteStock loteActualizado
                                        return Ok loteActualizado
                                    | None ->
                                        return Error "El lote especificado no existe"
                                | false, _ ->
                                    // Buscar si ya existe un lote activo para este producto
                                    let! lotesActivos = listarLotes (Some prodId) (Some Activo)
                                    match lotesActivos |> List.tryHead with
                                    | Some loteExistente ->
                                        let nuevaCant = { Valor = loteExistente.CantidadActual.Valor + req.Cantidad; Unidad = loteExistente.CantidadActual.Unidad }
                                        let loteActualizado = { loteExistente with CantidadActual = nuevaCant }
                                        do! actualizarLoteStock loteActualizado
                                        return Ok loteActualizado
                                    | None ->
                                        // Generar nuevo lote automáticamente
                                        let (genero, epiteto) =
                                            match producto.Categoria with
                                            | Semilla(nc, _) -> (nc.Genero, nc.Epiteto)
                                            | Plantin(nc, _, _) -> (nc.Genero, nc.Epiteto)
                                            | Insumo(n, _, _) -> (n, "INS")
                                            | Otro(n, _) -> (n, "OTR")

                                        let! lotesTotal = listarLotes (Some prodId) None
                                        let secuencia = min 99 (lotesTotal.Length + 1)
                                        let resCodigo = CodigoLote.generar genero epiteto fechaIngresoDateOnly (max 1 secuencia)

                                        match resCodigo with
                                        | Error err -> return Error (sprintf "Error generando código de lote: %A" err)
                                        | Ok codigo ->
                                            let nuevoLoteId = LoteId (Identidad.nuevo ())
                                            let procOpt = if String.IsNullOrWhiteSpace req.Procedencia then None else Some req.Procedencia
                                            let obsOpt = if String.IsNullOrWhiteSpace req.Observaciones then None else Some req.Observaciones

                                            match Lote.crear nuevoLoteId codigo prodId procOpt cantidadIngreso fechaIngresoDateOnly None obsOpt with
                                            | Error err -> return Error (sprintf "Error creando lote de dominio: %A" err)
                                            | Ok nuevoLote ->
                                                do! insertarLote nuevoLote
                                                return Ok nuevoLote
                            }

                        match optLoteFinal with
                        | Error err -> return Error err
                        | Ok lote ->
                            // 4. Crear MovimientoInventario de tipo Entrada
                            let motivoIngreso =
                                match req.TipoIngreso with
                                | "Compra" ->
                                    Compra (ProveedorId (Guid.Parse("01917f3a-0005-7000-8000-000000000001")))
                                | "Devolucion" ->
                                    Devolucion (ClienteId (Guid.Parse("01917f3a-0006-7000-8000-000000000001")))
                                | "Intercambio" | "Trueque" ->
                                    TruequeEntrada (TruequeId (Guid.Parse("01917f3a-0007-7000-8000-000000000001")))
                                | "Recoleccion" | _ ->
                                    let campana = if String.IsNullOrWhiteSpace req.Procedencia then "Ingreso Almacén" else req.Procedencia
                                    Recoleccion campana

                            let nuevoMovId = MovimientoId (Identidad.nuevo ())
                            let linea = { Referencia = lote.Id; Cantidad = cantidadIngreso }
                            let obsMovOpt = if String.IsNullOrWhiteSpace req.Observaciones then None else Some req.Observaciones

                            let movimientoDominio : MovimientoInventario =
                                { Id = nuevoMovId
                                  Fecha = fechaMov
                                  Responsable = EmpleadoId responsableId
                                  Tipo = Entrada motivoIngreso
                                  OrdenOrigen = None
                                  Lineas = [ linea ]
                                  Observaciones = obsMovOpt }

                            let contraparteNombre = if String.IsNullOrWhiteSpace req.Procedencia then None else Some req.Procedencia
                            do! insertarMovimiento movimientoDominio contraparteNombre None None

                            let (MovimientoId mGuid) = nuevoMovId
                            let (LoteId lGuid) = lote.Id
                            let dto : MovimientoDto =
                                { Id = mGuid.ToString()
                                  Fecha = fechaMov.ToString("yyyy-MM-dd HH:mm")
                                  ResponsableId = responsableId.ToString()
                                  Tipo = "Entrada"
                                  Motivo = req.TipoIngreso
                                  ContraparteNombre = defaultArg contraparteNombre ""
                                  Departamento = ""
                                  Solicitante = ""
                                  Observaciones = defaultArg obsMovOpt ""
                                  Lineas = [
                                      { LoteId = lGuid.ToString()
                                        CodigoLote = CodigoLote.valor lote.Codigo
                                        Cantidad = req.Cantidad
                                        Unidad = desmapearUnidad cantidadIngreso.Unidad }
                                  ] }

                            return Ok dto
        }

    let listarMovimientos
        (listarMovsRepo: string option -> Async<(MovimientoInventario * MetadataMovimiento) list>)
        (obtenerLote: LoteId -> Async<Lote option>)
        (tipoFilter: string option)
        : Async<MovimientoDto list> =
        async {
            let! rows = listarMovsRepo tipoFilter
            let! dtos =
                rows
                |> List.map (fun (mov, meta) ->
                    async {
                        let (MovimientoId mId) = mov.Id
                        let (EmpleadoId eId) = mov.Responsable
                        let tipoStr =
                            match mov.Tipo with
                            | Entrada _ -> "Entrada"
                            | Salida _ -> "Salida"
                        let motivoStr =
                            match mov.Tipo with
                            | Entrada (Compra _) -> "Compra"
                            | Entrada (Recoleccion c) -> sprintf "Recolección (%s)" c
                            | Entrada (DonacionRecibida d) -> sprintf "Donación (%s)" d
                            | Entrada (Devolucion _) -> "Devolución"
                            | Entrada (TruequeEntrada _) -> "Trueque"
                            | Salida (Venta _) -> "Venta"
                            | Salida (MuestraLab _) -> "Muestra Lab"
                            | Salida (Merma c) -> sprintf "Merma (%s)" c
                            | Salida (UsoInterno u) -> sprintf "Uso Interno (%s)" u
                            | Salida (DonacionEnviada d) -> sprintf "Donación (%s)" d
                            | Salida (TruequeSalida _) -> "Trueque"

                        let! lineasDtos =
                            mov.Lineas
                            |> List.map (fun l ->
                                async {
                                    let (LoteId lId) = l.Referencia
                                    let! optLote = obtenerLote l.Referencia
                                    let codLote = optLote |> Option.map (fun lot -> CodigoLote.valor lot.Codigo) |> Option.defaultValue "Lote Desconocido"
                                    return { LoteId = lId.ToString()
                                             CodigoLote = codLote
                                             Cantidad = l.Cantidad.Valor
                                             Unidad = desmapearUnidad l.Cantidad.Unidad }
                                })
                            |> Async.Parallel

                        return { Id = mId.ToString()
                                 Fecha = mov.Fecha.ToString("yyyy-MM-dd HH:mm")
                                 ResponsableId = eId.ToString()
                                 Tipo = tipoStr
                                 Motivo = motivoStr
                                 ContraparteNombre = defaultArg meta.ContraparteNombre ""
                                 Departamento = defaultArg meta.Departamento ""
                                 Solicitante = defaultArg meta.Solicitante ""
                                 Observaciones = defaultArg mov.Observaciones ""
                                 Lineas = Array.toList lineasDtos }
                    })
                |> Async.Parallel

            return Array.toList dtos
        }

    let consultarStockProducto
        (obtenerProducto: ProductoId -> Async<Producto option>)
        (listarLotesPorProducto: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (listarTodosMovimientos: unit -> Async<MovimientoInventario list>)
        (productoIdStr: string)
        : Async<Result<StockProductoDto, string>> =
        async {
            match Guid.TryParse(productoIdStr) with
            | false, _ -> return Error "UUID de producto inválido"
            | true, pGuid ->
                let prodId = ProductoId pGuid
                let! optProd = obtenerProducto prodId
                match optProd with
                | None -> return Error "Producto no encontrado"
                | Some prod ->
                    let! lotes = listarLotesPorProducto (Some prodId) None
                    let! movimientos = listarTodosMovimientos ()

                    let stockTotalGramos = Stock.stockProducto prodId lotes movimientos
                    let stockVentaGramos = Stock.disponibleParaVenta prodId lotes movimientos

                    let lotesDtos =
                        lotes
                        |> List.map (fun l ->
                            let (LoteId lId) = l.Id
                            let (ProductoId pId) = l.ProductoId
                            let stockG = Stock.cantidadLote l.Id movimientos
                            let estadoStr =
                                match l.Estado with
                                | Activo -> "Activo"
                                | Agotado -> "Agotado"
                                | Bloqueado -> "Bloqueado"
                                | Archivado -> "Archivado"

                            { LoteId = lId.ToString()
                              Codigo = CodigoLote.valor l.Codigo
                              ProductoId = pId.ToString()
                              NombreProducto = Producto.nombreVisible prod
                              FechaIngreso = l.FechaIngreso.ToString("yyyy-MM-dd")
                              Estado = estadoStr
                              StockGramos = stockG
                              StockDisplay = if l.CantidadActual.Unidad = Kilogramo then stockG / 1000m else stockG
                              Unidad = desmapearUnidad l.CantidadActual.Unidad })

                    return Ok { ProductoId = pGuid.ToString()
                                NombreProducto = Producto.nombreVisible prod
                                StockTotalGramos = stockTotalGramos
                                StockDisponibleVentaGramos = stockVentaGramos
                                Lotes = lotesDtos }
        }

    // ─────────────────────────────────────────────────────────
    // registrarEgreso — Feature F8 / F9 (MF-08-03 / MF-09-01)
    //
    // Implementa la salida de stock usando la política FIFO del domain.
    // Fifo.resolverFIFO ordena los lotes por FechaIngreso y deduce la
    // cantidad solicitada de los más antiguos primero.
    //
    // EXTENSIBILITY (F6 — Clientes):
    //   ContraparteNombre se guarda como texto libre.  Cuando F6 esté activa,
    //   se resolverá a ClienteId mediante búsqueda multivariable.
    //
    // EXTENSIBILITY (F9 — Uso Interno):
    //   Departamento y Solicitante se persisten en columnas dedicadas de
    //   movimiento_inventario para futura integración con organigrama.
    // ─────────────────────────────────────────────────────────
    let registrarEgreso
        (obtenerProductoPorId: ProductoId -> Async<Producto option>)
        (listarProductos: unit -> Async<Producto list>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (actualizarLoteStock: Lote -> Async<unit>)
        (insertarMovimiento: MovimientoInventario -> string option -> string option -> string option -> Async<unit>)
        (listarTodosMovimientos: unit -> Async<MovimientoInventario list>)
        (responsableId: Guid)
        (req: RegistrarEgresoRequest)
        : Async<Result<MovimientoDto, string>> =
        async {
            // 1. Validar cantidad
            if req.Cantidad <= 0m then
                return Error "La cantidad de egreso debe ser mayor a cero"
            else
                let unidad = mapearUnidad req.Unidad
                match Cantidad.crear req.Cantidad unidad with
                | Error err -> return Error (sprintf "Cantidad inválida: %A" err)
                | Ok cantidadEgreso ->

                    // 2. Resolver Producto
                    let! optProducto =
                        async {
                            match Guid.TryParse(req.ProductoId) with
                            | true, pGuid -> return! obtenerProductoPorId (ProductoId pGuid)
                            | false, _ ->
                                if not (String.IsNullOrWhiteSpace req.Descripcion) then
                                    let! todos = listarProductos ()
                                    return todos |> List.tryFind (fun p ->
                                        String.Equals(Producto.nombreVisible p, req.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)
                                        || match p.Categoria with
                                           | Semilla(nc, _) -> String.Equals(nc.Epiteto, req.Descripcion.Trim(), StringComparison.OrdinalIgnoreCase)
                                           | _ -> false)
                                else
                                    return None
                        }

                    match optProducto with
                    | None -> return Error (sprintf "No se encontró el producto '%s' en el catálogo" req.Descripcion)
                    | Some producto ->
                        let prodId = producto.Base.Id

                        // 3. Cargar lotes activos y movimientos para FIFO
                        let! lotesActivos = listarLotes (Some prodId) (Some Activo)
                        let! todosMovimientos = listarTodosMovimientos ()

                        // 4. Resolver FIFO — domain determina qué lotes y cuánto de cada uno
                        match Fifo.resolverFIFO prodId cantidadEgreso lotesActivos todosMovimientos with
                        | Error fifoErr ->
                            return Error (sprintf "Stock insuficiente para el egreso: %A" fifoErr)
                        | Ok resolucionesFifo ->

                            // 5. Actualizar stock de cada lote afectado
                            for linea in resolucionesFifo do
                                match lotesActivos |> List.tryFind (fun (l: Sinbas.Domain.Lote) -> l.Id = linea.Referencia) with
                                | Some lote ->
                                    let stockActual = Stock.cantidadLote lote.Id todosMovimientos
                                    let cantDeducidaGramos = Cantidad.enGramos linea.Cantidad
                                    let stockNuevo = max 0m (stockActual - cantDeducidaGramos)
                                    let nuevaCant : Cantidad = { Valor = stockNuevo; Unidad = lote.CantidadActual.Unidad }
                                    let nuevoEstado = if nuevaCant.Valor <= 0m then Agotado else Activo
                                    let loteActualizado : Sinbas.Domain.Lote = { lote with CantidadActual = nuevaCant; Estado = nuevoEstado }
                                    do! actualizarLoteStock loteActualizado
                                | None -> ()

                            // 6. Construir tipo de movimiento de dominio
                            let fechaMov =
                                match DateTime.TryParse(req.Fecha) with
                                | true, dt -> dt
                                | false, _ -> DateTime.UtcNow

                            let motivoEgreso =
                                match req.TipoEgreso with
                                | "Venta" ->
                                    // EXTENSIBILITY(F6): resolver ClienteId real aquí
                                    Venta (ClienteId (Guid.Parse("01917f3a-0006-7000-8000-000000000001")))
                                | "Merma" ->
                                    let causa = if String.IsNullOrWhiteSpace req.Observaciones then "Merma operativa" else req.Observaciones
                                    Merma causa
                                | "UsoLabor" | "MuestraLab" ->
                                    MuestraLab (LaboratorioId (Guid.Parse("01917f3a-0008-7000-8000-000000000001")))
                                | "UsoVivero" | "UsoInterno" ->
                                    // EXTENSIBILITY(F9): Departamento y Solicitante se persistirán en columnas dedicadas
                                    let partes = [
                                        if not (String.IsNullOrWhiteSpace req.Departamento) then sprintf "[Depto: %s]" req.Departamento
                                        if not (String.IsNullOrWhiteSpace req.Solicitante) then sprintf "[Solicitante: %s]" req.Solicitante
                                        if not (String.IsNullOrWhiteSpace req.Observaciones) then req.Observaciones
                                    ]
                                    let desc = if List.isEmpty partes then "Uso Interno" else String.concat " " partes
                                    UsoInterno desc
                                | "Intercambio" | "Trueque" ->
                                    TruequeSalida (TruequeId (Guid.Parse("01917f3a-0007-7000-8000-000000000001")))
                                | _ ->
                                    UsoInterno (if String.IsNullOrWhiteSpace req.TipoEgreso then "Salida General" else req.TipoEgreso)

                            let nuevoMovId = MovimientoId (Identidad.nuevo ())
                            let obsMovOpt = if String.IsNullOrWhiteSpace req.Observaciones then None else Some req.Observaciones

                            // Cada resolución FIFO es directamente una línea de movimiento
                            let lineas : LineaMovimiento list = resolucionesFifo

                            let movimientoDominio : MovimientoInventario =
                                { Id = nuevoMovId
                                  Fecha = fechaMov
                                  Responsable = EmpleadoId responsableId
                                  Tipo = Salida motivoEgreso
                                  OrdenOrigen = None
                                  Lineas = lineas
                                  Observaciones = obsMovOpt }

                            let contraparteOpt =
                                if String.IsNullOrWhiteSpace req.ContraparteNombre then None
                                else Some req.ContraparteNombre
                            let deptoOpt =
                                if String.IsNullOrWhiteSpace req.Departamento then None
                                else Some req.Departamento
                            let solicitanteOpt =
                                if String.IsNullOrWhiteSpace req.Solicitante then None
                                else Some req.Solicitante

                            do! insertarMovimiento movimientoDominio contraparteOpt deptoOpt solicitanteOpt

                            // 7. Construir DTO de respuesta
                            let (MovimientoId mGuid) = nuevoMovId
                            let lineasDto =
                                resolucionesFifo |> List.map (fun linea ->
                                    let (LoteId lGuid) = linea.Referencia
                                    let codigo =
                                        lotesActivos
                                        |> List.tryFind (fun (l: Sinbas.Domain.Lote) -> l.Id = linea.Referencia)
                                        |> Option.map (fun l -> CodigoLote.valor l.Codigo)
                                        |> Option.defaultValue (lGuid.ToString().Substring(0, 8))
                                    { LoteId = lGuid.ToString()
                                      CodigoLote = codigo
                                      Cantidad = linea.Cantidad.Valor
                                      Unidad = desmapearUnidad linea.Cantidad.Unidad } : LineaMovimientoDto)

                            let dto : MovimientoDto =
                                { Id = mGuid.ToString()
                                  Fecha = fechaMov.ToString("yyyy-MM-dd HH:mm")
                                  ResponsableId = responsableId.ToString()
                                  Tipo = "Salida"
                                  Motivo = req.TipoEgreso
                                  ContraparteNombre = defaultArg contraparteOpt ""
                                  Departamento = defaultArg deptoOpt ""
                                  Solicitante = defaultArg solicitanteOpt ""
                                  Observaciones = defaultArg obsMovOpt ""
                                  Lineas = lineasDto }

                            return Ok dto
        }
