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
    let mockTx (_: OrdenVenta) (_: OrdenDespacho) (reservas: (LoteId * Cantidad) list) =
        async { capturado <- reservas; return Ok () }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            (let (UsuarioId g) = responsableId in g)
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
    let mockTx (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async { llamadoTx <- true; return Ok () }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            (let (UsuarioId g) = responsableId in g)
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

    let mockTx (_: OrdenVenta) (_: OrdenDespacho) (_: (LoteId * Cantidad) list) =
        async { return Ok () }

    let res =
        VentaService.confirmarVenta
            (fun _ -> async { return Some proforma })
            (fun _ -> async { return Some cliente })
            (fun _ -> async { return Some prod })
            (fun _ _ -> async { return [ lote ] })
            mockTx
            (fun _ -> "Producto")
            (fun _ -> "Cliente")
            (let (UsuarioId g) = responsableId in g)
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
    let lote = mkLote loteId prodId 10000m 2000m Gramo
    let cliente = mkCliente clienteId

    let ordenDespacho : OrdenDespacho =
        { Id = despachoId
          Codigo = CodigoDespacho.reconstruir "DSP-202610-0001"
          Origen = OrigenDespacho.DeVenta (OrdenId (Guid.NewGuid()))
          ClienteId = Some clienteId
          Lineas = [ { Referencia = prodId; Cantidad = cantReconst 2m Kilogramo } ]
          Estado = EstadoDespacho.Pendiente
          CreadoEn = DateTime.UtcNow }

    let mutable capturadoDeducciones : (LoteId * Cantidad) list = []
    let mockDespachoTx (_: OrdenId) (_: MovimientoInventario) (deducciones: (LoteId * Cantidad) list) (_: string option) =
        async { capturadoDeducciones <- deducciones; return Ok () }

    let res =
        VentaService.confirmarDespachoFisico
            (fun _ -> async { return Some ordenDespacho })
            (fun _ _ -> async { return [ lote ] })
            (fun _ -> async { return Some cliente })
            mockDespachoTx
            responsableId
            (let (OrdenId g) = despachoId in g.ToString())
            { Observaciones = Some "Despacho test" }
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Single(capturadoDeducciones) |> ignore
        let (lid, c) = capturadoDeducciones.Head
        Assert.Equal(loteId, lid)
        Assert.Equal(Gramo, c.Unidad)
        Assert.Equal(2000m, c.Valor)
