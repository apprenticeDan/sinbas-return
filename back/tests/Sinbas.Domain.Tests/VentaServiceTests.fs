module VentaServiceTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

// ── Helpers de construcción de fixtures ──

let private mkProducto (prodId: ProductoId) (activo: bool) : Producto =
    let nc = NombreCientifico.reconstruir "Swietenia" "macrophylla" None
    let pres = unwrap (Presentacion.reconstruir "Bolsa" 1m Kilogramo)
    { Base =
        { Id = prodId
          Presentacion = pres
          Trazabilidad = Trazabilidad.PorLote
          Activo = activo
          Observaciones = None }
      Categoria = CategoriaProducto.Semilla (nc, [])
      PrecioOficial = Some { Valor = 100m; Moneda = "BOB"; ModificadoPor = None; FechaActualizacion = None }
      EstadoComercial = if activo then EstadoComercial.ActivoParaVenta else EstadoComercial.Inactivo }

let private mkLote (loteId: LoteId) (prodId: ProductoId) (actual: decimal) (reservada: decimal) (unidad: UnidadMedida) : Lote =
    let cod = unwrap (CodigoLote.desdeString "SWIETMAC-02608-01")
    { Id = loteId
      Codigo = cod
      ProductoId = prodId
      Procedencia = None
      CantidadInicial = cantReconst actual unidad
      CantidadActual = cantReconst actual unidad
      CantidadReservada = cantReconst reservada unidad
      FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
      Ubicacion = None
      Estado = EstadoLote.Activo
      Observaciones = None }

let private mkProforma (pId: ProformaId) (cliId: ClienteId) (respId: UsuarioId) (lineas: LineaCotizada list) : Proforma =
    { Id = pId
      Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
      CreadoEn = DateTime.UtcNow
      ResponsableId = respId
      ClienteId = Some cliId
      ClienteNombreLibre = None
      Estado = EstadoProforma.Vigente
      FechaVencimiento = None
      Moneda = "BOB"
      Lineas = lineas
      Total = lineas |> List.sumBy Cotizacion.subtotal
      Leyenda = LeyendaNoReserva
      OrdenVentaId = None
      AnulacionMotivo = None
      AnuladoPor = None
      AnuladoEn = None
      Observaciones = None }

let private mkCliente (cId: ClienteId) : Cliente =
    { Id = cId
      Tipo = TipoCliente.Juridica (RazonSocial.reconstruir "Cliente Test", NIT.reconstruir "123456789", None)
      Telefono = None
      Email = None
      Direccion = None
      Estado = EstadoCliente.Activo }

let private actorTest : Actor =
    { UsuarioId = UsuarioId (Guid.NewGuid())
      EmpleadoId = EmpleadoId (Guid.NewGuid())
      Roles = Set.ofList [ NombreRol.Comercial; NombreRol.Almacen; NombreRol.Gerencia ] }

let private mockConsultarIdempotencia _ = async { return Ok ClaimOutcome.Nuevo }

// ── Tests ──

[<Fact>]
let ``confirmarVenta convierte 1 kg a 1000 g al reservar en lote en gramos`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable capturado : (LoteId * Cantidad) list = []
    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (reservas: (LoteId * Cantidad) list) =
        async {
            capturado <- reservas
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok _ ->
        Assert.Single(capturado) |> ignore
        let (_, c) = capturado.Head
        Assert.Equal(Gramo, c.Unidad)
        Assert.Equal(1000m, c.Valor)

[<Fact>]
let ``confirmarVenta falla 409 si stock disponible insuficiente tras reserva parcial`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    // Lote con 5000 g físicos, 3000 g ya reservados -> disponible 2000 g (2 kg)
    let lote = mkLote loteId prodId 5000m 3000m Gramo
    // Proforma pide 3 kg -> 3000 g > 2000 g disponibles
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 3m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable llamadoTx = false
    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            llamadoTx <- true
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 409"
    | Error (_, code) ->
        Assert.Equal(409, code)
        Assert.False(llamadoTx, "confirmarVentaTx NO debe llamarse si el FIFO falla")

[<Fact>]
let ``confirmarVenta falla 422 si producto fue desactivado despues de proforma`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    // Producto ya no apto para venta
    let prod = { mkProducto prodId false with
                    EstadoComercial = EstadoComercial.Inactivo }
    let lote = mkLote (LoteId (Guid.NewGuid())) prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 422"
    | Error (_, code) -> Assert.Equal(422, code)

[<Fact>]
let ``despacharReserva falla si no hay reserva suficiente (falla dura)`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let lote = mkLote (LoteId (Guid.NewGuid())) prodId 5000m 500m Gramo
    // Intentar despachar 2 kg cuando solo hay 0.5 kg reservados
    match Lote.despacharReserva (cantReconst 2m Kilogramo) lote with
    | Ok _ -> failwith "Debió fallar con reserva insuficiente"
    | Error (OperacionInvalida msg) -> Assert.Contains("Reserva insuficiente", msg)
    | Error e -> failwith (sprintf "Error inesperado: %A" e)

[<Fact>]
let ``confirmarDespachoFisico descuenta reserva y stock fisico enviando Cantidad consolidada al tx`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let despachoId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = Guid.NewGuid()

    let ordenDespacho : OrdenDespacho =
        { Id = despachoId
          Codigo = CodigoDespacho.reconstruir "DSP-202610-0001"
          Origen = OrigenDespacho.DeVenta (OrdenId (Guid.NewGuid()))
          ClienteId = Some clienteId
          Lineas = [ { Referencia = prodId; Cantidad = cantReconst 2m Kilogramo } ]
          Estado = EstadoDespacho.Pendiente
          CreadoEn = DateTime.UtcNow }

    let mockLinea : LineaMovimientoDto =
        { LoteId = (let (LoteId g) = loteId in g.ToString())
          CodigoLote = "SWIETMAC-02608-01"
          Cantidad = 2000m
          Unidad = "Gramo" }

    let mockDespachoTx (_: OrdenId) (_: MovimientoInventario) (_: string option) =
        async { return Ok [ mockLinea ] }

    let res =
        VentaService.confirmarDespachoFisico
            (fun _ -> async { return Some ordenDespacho })
            mockDespachoTx
            actorTest
            (let (OrdenId g) = despachoId in g.ToString())
            { Observaciones = Some "Despacho test" }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Single(dto.LineasDespachadas) |> ignore
        let linea = dto.LineasDespachadas.Head
        Assert.Equal("SWIETMAC-02608-01", linea.CodigoLote)
        Assert.Equal("Gramo", linea.Unidad)
        Assert.Equal(2000m, linea.Cantidad)

[<Fact>]
let ``confirmarVenta OBS-04: proforma con FechaVencimiento en el pasado no bloquea y retorna Ok`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let ayer = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
    let proforma =
        { mkProforma proformaId clienteId responsableId
            [ { ProductoId = prodId
                Cantidad = cantReconst 1m Kilogramo
                PrecioUnitario = 100m } ]
          with FechaVencimiento = Some ayer
               Estado = EstadoProforma.Vigente }
    let cliente = mkCliente clienteId

    let mutable llamadoTx = false
    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            llamadoTx <- true
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, code) -> failwithf "No debió fallar por fecha de vencimiento: %s (code=%d)" msg code
    | Ok ventaConfirmada ->
        Assert.True(llamadoTx, "confirmarVentaTx debió ejecutarse")
        Assert.Equal("OV-", ventaConfirmada.Venta.Codigo.Substring(0, 3))

// ── Tests Fase 4: Reserva de stock, despacho y anulación ──

[<Fact>]
let ``confirmarVenta inserts reservas with preserved unit`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable capturado : (LoteId * Cantidad) list = []
    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (reservas: (LoteId * Cantidad) list) =
        async {
            capturado <- reservas
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok _ ->
        Assert.Single(capturado) |> ignore
        let (lid, c) = capturado.Head
        Assert.Equal(loteId, lid)
        Assert.Equal(Gramo, c.Unidad)
        Assert.Equal(1000m, c.Valor)

[<Fact>]
let ``confirmarDespachoFisico fails 409 if no active reservations`` () =
    let despachoId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = Guid.NewGuid()
    let ordenDespacho : OrdenDespacho =
        { Id = despachoId
          Codigo = CodigoDespacho.reconstruir "DSP-202610-0002"
          Origen = OrigenDespacho.DeVenta (OrdenId (Guid.NewGuid()))
          ClienteId = Some clienteId
          Lineas = []
          Estado = EstadoDespacho.Pendiente
          CreadoEn = DateTime.UtcNow }

    let mockDespachoTx (_: OrdenId) (_: MovimientoInventario) (_: string option) =
        async { return Error "No hay reservas activas para la orden" }

    let res =
        VentaService.confirmarDespachoFisico
            (fun _ -> async { return Some ordenDespacho })
            mockDespachoTx
            actorTest
            (let (OrdenId g) = despachoId in g.ToString())
            { Observaciones = None }
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 409"
    | Error (msg, code) ->
        Assert.Equal(409, code)
        Assert.Contains("reservas", msg)

[<Fact>]
let ``confirmarDespachoFisico returns lines from repo`` () =
    let despachoId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = Guid.NewGuid()
    let ordenDespacho : OrdenDespacho =
        { Id = despachoId
          Codigo = CodigoDespacho.reconstruir "DSP-202610-0003"
          Origen = OrigenDespacho.DeVenta (OrdenId (Guid.NewGuid()))
          ClienteId = Some clienteId
          Lineas = []
          Estado = EstadoDespacho.Pendiente
          CreadoEn = DateTime.UtcNow }

    let line1 : LineaMovimientoDto =
        { LoteId = Guid.NewGuid().ToString()
          CodigoLote = "LOTE-001"
          Cantidad = 500m
          Unidad = "Gramo" }
    let line2 : LineaMovimientoDto =
        { LoteId = Guid.NewGuid().ToString()
          CodigoLote = "LOTE-002"
          Cantidad = 500m
          Unidad = "Gramo" }

    let mockDespachoTx (_: OrdenId) (_: MovimientoInventario) (_: string option) =
        async { return Ok [ line1; line2 ] }

    let res =
        VentaService.confirmarDespachoFisico
            (fun _ -> async { return Some ordenDespacho })
            mockDespachoTx
            actorTest
            (let (OrdenId g) = despachoId in g.ToString())
            { Observaciones = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Equal(2, dto.LineasDespachadas.Length)

[<Fact>]
let ``anularVenta ok on Confirmada state`` () =
    let ventaId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let prodId = ProductoId (Guid.NewGuid())
    let responsableId = EmpleadoId (Guid.NewGuid())
    let ordenVenta : OrdenVenta =
        { Id = ventaId
          Codigo = unwrap (CodigoVenta.crear "OV-202610-0001")
          ProformaOrigenId = ProformaId (Guid.NewGuid())
          ClienteId = clienteId
          Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
          ResponsableId = responsableId
          Lineas = [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
          Total = 100m
          Moneda = "BOB"
          Estado = EstadoVenta.Confirmada
          CreadoEn = DateTime.UtcNow }

    let mutable ventaAnuladaEstado : OrdenVenta =
        { ordenVenta with
            Estado =
                EstadoVenta.Anulada
                    { Fecha = DateTime.UtcNow
                      Motivo = "Error en pedido"
                      AnuladoPor = responsableId } }

    let mutable txLlamado = false
    let mockTx (_: OrdenId) (_: string) (_: EmpleadoId) =
        async { txLlamado <- true; return Ok () }

    let actor = { actorTest with EmpleadoId = responsableId }

    let res =
        VentaService.anularVenta
            (fun _ -> async { if txLlamado then return Some ventaAnuladaEstado else return Some ordenVenta })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actor
            (let (OrdenId g) = ventaId in g.ToString())
            "Error en pedido"
        |> Async.RunSynchronously

    match res with
    | Error (msg, code) -> failwithf "Fallo inesperado: %s (code=%d)" msg code
    | Ok dto ->
        Assert.True(txLlamado)
        Assert.Equal("Anulada", dto.Estado)

[<Fact>]
let ``anularVenta fails 409 on Despachada state`` () =
    let ventaId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let prodId = ProductoId (Guid.NewGuid())
    let responsableId = EmpleadoId (Guid.NewGuid())
    let ordenVenta : OrdenVenta =
        { Id = ventaId
          Codigo = unwrap (CodigoVenta.crear "OV-202610-0002")
          ProformaOrigenId = ProformaId (Guid.NewGuid())
          ClienteId = clienteId
          Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
          ResponsableId = responsableId
          Lineas = [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
          Total = 100m
          Moneda = "BOB"
          Estado = EstadoVenta.Despachada
          CreadoEn = DateTime.UtcNow }

    let mutable txLlamado = false
    let mockTx (_: OrdenId) (_: string) (_: EmpleadoId) =
        async { txLlamado <- true; return Ok () }

    let res =
        VentaService.anularVenta
            (fun _ -> async { return Some ordenVenta })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            (let (OrdenId g) = ventaId in g.ToString())
            "Cancelación cliente"
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 409 en venta Despachada"
    | Error (msg, code) ->
        Assert.False(txLlamado, "El mock no debió ser llamado")
        Assert.Equal(409, code)
        Assert.Contains("despachada", msg.ToLowerInvariant())

[<Fact>]
let ``anularVenta fails 400 on empty motivo`` () =
    let ventaId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let prodId = ProductoId (Guid.NewGuid())
    let responsableId = EmpleadoId (Guid.NewGuid())
    let ordenVenta : OrdenVenta =
        { Id = ventaId
          Codigo = unwrap (CodigoVenta.crear "OV-202610-0003")
          ProformaOrigenId = ProformaId (Guid.NewGuid())
          ClienteId = clienteId
          Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
          ResponsableId = responsableId
          Lineas = [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
          Total = 100m
          Moneda = "BOB"
          Estado = EstadoVenta.Confirmada
          CreadoEn = DateTime.UtcNow }

    let mutable txLlamado = false
    let mockTx (_: OrdenId) (_: string) (_: EmpleadoId) =
        async { txLlamado <- true; return Ok () }

    let res =
        VentaService.anularVenta
            (fun _ -> async { return Some ordenVenta })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            (let (OrdenId g) = ventaId in g.ToString())
            "   "
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 400 por motivo vacío"
    | Error (msg, code) ->
        Assert.False(txLlamado, "El mock no debió ser llamado")
        Assert.Equal(400, code)
        Assert.Contains("motivo", msg.ToLowerInvariant())

[<Fact>]
let ``anularVenta fails 403 when Comercial actor is not the issuer`` () =
    let ventaId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let prodId = ProductoId (Guid.NewGuid())
    let emisorId = EmpleadoId (Guid.NewGuid())
    let otroComercialId = EmpleadoId (Guid.NewGuid())
    let ordenVenta : OrdenVenta =
        { Id = ventaId
          Codigo = unwrap (CodigoVenta.crear "OV-202610-0004")
          ProformaOrigenId = ProformaId (Guid.NewGuid())
          ClienteId = clienteId
          Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
          ResponsableId = emisorId
          Lineas = [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
          Total = 100m
          Moneda = "BOB"
          Estado = EstadoVenta.Confirmada
          CreadoEn = DateTime.UtcNow }

    let mutable txLlamado = false
    let mockTx (_: OrdenId) (_: string) (_: EmpleadoId) =
        async { txLlamado <- true; return Ok () }

    let actorComercialAjeno =
        { UsuarioId = UsuarioId (Guid.NewGuid())
          EmpleadoId = otroComercialId
          Roles = Set.singleton NombreRol.Comercial }

    let res =
        VentaService.anularVenta
            (fun _ -> async { return Some ordenVenta })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorComercialAjeno
            (let (OrdenId g) = ventaId in g.ToString())
            "Cancelación cliente ajeno"
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió fallar con 403 para comercial ajeno"
    | Error (msg, code) ->
        Assert.False(txLlamado, "El mock no debió ser llamado")
        Assert.Equal(403, code)
        Assert.Contains("responsable", msg.ToLowerInvariant())

[<Fact>]
let ``anularVenta ok when Gerencia actor is not the issuer`` () =
    let ventaId = OrdenId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let prodId = ProductoId (Guid.NewGuid())
    let emisorId = EmpleadoId (Guid.NewGuid())
    let gerenteId = EmpleadoId (Guid.NewGuid())
    let ordenVenta : OrdenVenta =
        { Id = ventaId
          Codigo = unwrap (CodigoVenta.crear "OV-202610-0005")
          ProformaOrigenId = ProformaId (Guid.NewGuid())
          ClienteId = clienteId
          Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
          ResponsableId = emisorId
          Lineas = [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
          Total = 100m
          Moneda = "BOB"
          Estado = EstadoVenta.Confirmada
          CreadoEn = DateTime.UtcNow }

    let mutable ventaAnuladaEstado : OrdenVenta =
        { ordenVenta with
            Estado =
                EstadoVenta.Anulada
                    { Fecha = DateTime.UtcNow
                      Motivo = "Intervención gerencial"
                      AnuladoPor = gerenteId } }

    let mutable txLlamado = false
    let mockTx (_: OrdenId) (_: string) (_: EmpleadoId) =
        async { txLlamado <- true; return Ok () }

    let actorGerente =
        { UsuarioId = UsuarioId (Guid.NewGuid())
          EmpleadoId = gerenteId
          Roles = Set.singleton NombreRol.Gerencia }

    let res =
        VentaService.anularVenta
            (fun _ -> async { if txLlamado then return Some ventaAnuladaEstado else return Some ordenVenta })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorGerente
            (let (OrdenId g) = ventaId in g.ToString())
            "Intervención gerencial"
        |> Async.RunSynchronously

    match res with
    | Error (msg, code) -> failwithf "Fallo inesperado: %s (code=%d)" msg code
    | Ok dto ->
        Assert.True(txLlamado)
        Assert.Equal("Anulada", dto.Estado)

[<Fact>]
let ``confirmarVenta usa placeholders en base y compone DTO con codigos reales devueltos por tx`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable codigoVentaBaseRecibido = ""
    let mutable codigoDespachoBaseRecibido = ""

    let mockTx (_: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (ov: OrdenVenta) (od: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            codigoVentaBaseRecibido <- CodigoVenta.valor ov.Codigo
            codigoDespachoBaseRecibido <- CodigoDespacho.valor od.Codigo
            let cvReal = CodigoVenta.reconstruir "OV-202611-0042"
            let cdReal = CodigoDespacho.reconstruir "DSP-202611-0042"
            return Ok (cvReal, cdReal)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto Test")
            (fun _ -> "Cliente Test")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, code) -> failwithf "Fallo inesperado: %s (code=%d)" msg code
    | Ok dto ->
        Assert.Equal("OV-PENDING", codigoVentaBaseRecibido)
        Assert.Equal("DSP-PENDING", codigoDespachoBaseRecibido)
        Assert.Equal("OV-202611-0042", dto.Venta.Codigo)
        Assert.Equal("DSP-202611-0042", dto.Despacho.Codigo)

[<Fact>]
let ``confirmarVenta sin header idempotencia -> invoca tx con None`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable idempotenciaRecibida : (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option =
        Some ({ UsuarioId = UsuarioId Guid.Empty; Endpoint = ""; Clave = ""; RequestHash = ""; TtlHours = 0 }, (fun _ _ -> ""))

    let mockTx (idem: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            idempotenciaRecibida <- idem
            let cv = CodigoVenta.reconstruir "OV-202611-0001"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0001"
            return Ok (cv, cd)
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            actorTest
            None
            "POST /api/proformas/test/confirmar-venta"
            "hash"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok _ ->
        Assert.True(idempotenciaRecibida.IsNone, "La tx debió recibir None para idempotencia cuando no hay clave")

[<Fact>]
let ``confirmarVenta con header -> invoca tx con Some claim y serializer produce JSON valido`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let loteId = LoteId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())
    let prod = mkProducto prodId true
    let lote = mkLote loteId prodId 5000m 0m Gramo
    let proforma = mkProforma proformaId clienteId responsableId
                        [ { ProductoId = prodId
                            Cantidad = cantReconst 1m Kilogramo
                            PrecioUnitario = 100m } ]
    let cliente = mkCliente clienteId

    let mutable claimRecibido : IdempotenciaClaim option = None
    let mutable jsonGenerado : string option = None

    let mockTx (idem: (IdempotenciaClaim * (CodigoVenta -> CodigoDespacho -> string)) option) (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async {
            match idem with
            | None -> ()
            | Some (claim, serializer) ->
                claimRecibido <- Some claim
                let cv = CodigoVenta.reconstruir "OV-202611-0042"
                let cd = CodigoDespacho.reconstruir "DSP-202611-0042"
                jsonGenerado <- Some (serializer cv cd)
            let cv = CodigoVenta.reconstruir "OV-202611-0042"
            let cd = CodigoDespacho.reconstruir "DSP-202611-0042"
            return Ok (cv, cd)
        }

    let clavePrueba = "key-test-uuid"
    let endpointPrueba = "POST /api/proformas/p-123/confirmar-venta"
    let hashPrueba = "hash-sha256-abc"

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockConsultarIdempotencia
            mockTx
            (fun _ -> "Producto Test")
            (fun _ -> "Cliente Test")
            actorTest
            (Some clavePrueba)
            endpointPrueba
            hashPrueba
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok _ ->
        Assert.True(claimRecibido.IsSome, "El claim debió recibirse en la tx")
        let claim = claimRecibido.Value
        Assert.Equal(clavePrueba, claim.Clave)
        Assert.Equal(endpointPrueba, claim.Endpoint)
        Assert.Equal(hashPrueba, claim.RequestHash)
        Assert.Equal(actorTest.UsuarioId, claim.UsuarioId)
        Assert.Equal(24, claim.TtlHours)

        Assert.True(jsonGenerado.IsSome, "El serializer debió generar JSON")
        let json = jsonGenerado.Value
        Assert.Contains("OV-202611-0042", json)
        Assert.Contains("DSP-202611-0042", json)
        let dto = System.Text.Json.JsonSerializer.Deserialize<VentaConfirmadaDto>(json)
        Assert.Equal("OV-202611-0042", dto.Venta.Codigo)
        Assert.Equal("DSP-202611-0042", dto.Despacho.Codigo)

[<Fact>]
let ``confirmarVenta con replay detectado -> retorna 200 con cuerpo previo sin invocar tx ni validar proforma`` () =
    let proformaId = ProformaId (Guid.NewGuid())
    let mutable txInvocada = false
    let mockTx _ _ _ _ =
        async {
            txInvocada <- true
            return Ok (CodigoVenta.reconstruir "OV-0", CodigoDespacho.reconstruir "DSP-0")
        }

    let cuerpoCacheado = "{\"Venta\":{\"Codigo\":\"OV-CACHE\"}}"
    let mockConsultar (c: IdempotenciaClaim) =
        async {
            return Ok (ClaimOutcome.Replay (cuerpoCacheado, 201))
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return failwith "No debió cargar proforma en replay" })
            (fun _ -> async { return None })
            (fun _ -> async { return None })
            (fun _ _ -> async { return [] })
            mockConsultar
            mockTx
            (fun _ -> "")
            (fun _ -> "")
            actorTest
            (Some "key-replay-1")
            "POST /api/proformas/p/confirmar-venta"
            "hash-1"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió retornar Error con prefijo REPLAY"
    | Error (msg, code) ->
        Assert.Equal(200, code)
        Assert.Equal("REPLAY:" + cuerpoCacheado, msg)
        Assert.False(txInvocada, "La tx no debió invocarse en replay")

[<Fact>]
let ``confirmarVenta con hash mismatch -> retorna 422 sin invocar tx`` () =
    let proformaId = ProformaId (Guid.NewGuid())
    let mutable txInvocada = false
    let mockTx _ _ _ _ =
        async {
            txInvocada <- true
            return Ok (CodigoVenta.reconstruir "OV-0", CodigoDespacho.reconstruir "DSP-0")
        }

    let mockConsultar (c: IdempotenciaClaim) =
        async {
            return Ok ClaimOutcome.HashMismatch
        }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return failwith "No debió cargar proforma en mismatch" })
            (fun _ -> async { return None })
            (fun _ -> async { return None })
            (fun _ _ -> async { return [] })
            mockConsultar
            mockTx
            (fun _ -> "")
            (fun _ -> "")
            actorTest
            (Some "key-mismatch-1")
            "POST /api/proformas/p/confirmar-venta"
            "hash-2"
            (let (ProformaId g) = proformaId in g.ToString())
            { ClienteId = None }
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Debió retornar Error 422"
    | Error (msg, code) ->
        Assert.Equal(422, code)
        Assert.Equal("Idempotency-Key reused with different body", msg)
        Assert.False(txInvocada, "La tx no debió invocarse en mismatch")
