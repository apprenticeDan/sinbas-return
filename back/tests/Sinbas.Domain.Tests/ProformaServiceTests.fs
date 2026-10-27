module ProformaServiceTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

// ── Helpers (mismo estilo que VentaServiceTests.fs) ──

let private mkProductoConPrecio (prodId: ProductoId) (precio: decimal) (activo: bool) : Producto =
    let nc = NombreCientifico.reconstruir "Swietenia" "macrophylla" None
    let pres = unwrap (Presentacion.reconstruir "Bolsa" 1m Kilogramo)
    { Base =
        { Id = prodId
          Presentacion = pres
          Trazabilidad = Trazabilidad.PorLote
          Activo = activo
          Observaciones = None }
      Categoria = CategoriaProducto.Semilla (nc, [])
      PrecioOficial = Some { Valor = precio; Moneda = "BOB"; ModificadoPor = None; FechaActualizacion = None }
      EstadoComercial = if activo then EstadoComercial.ActivoParaVenta else EstadoComercial.Inactivo }

let private mkProformaVigente
    (pId: ProformaId) (cliId: ClienteId) (respId: UsuarioId)
    (lineas: LineaCotizada list) (estado: EstadoProforma) : Proforma =
    { Id = pId
      Fecha = DateOnly.FromDateTime(DateTime.UtcNow)
      CreadoEn = DateTime.UtcNow
      ResponsableId = respId
      ClienteId = Some cliId
      ClienteNombreLibre = None
      Estado = estado
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

// ── Fase 5: Refresh de Proforma ──

[<Fact>]
let ``Fase5: proforma vigente sin cambios de precio ni stock no tiene advertencias`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())

    let prod = mkProductoConPrecio prodId 100m true
    let proforma =
        mkProformaVigente proformaId clienteId responsableId
            [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
            EstadoProforma.Vigente

    let mockProforma (_: ProformaId) = async { return Some proforma }
    let mockProducto (_: ProductoId) = async { return Some prod }
    let mockStock (_: ProductoId) = async { return 2000m }
    let resolver (_: ProductoId) = "Semilla Test"

    let res =
        ProformaService.obtenerProformaPorId
            mockProforma mockProducto mockStock resolver
            (let (ProformaId g) = proformaId in g.ToString())
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Empty(dto.Advertencias)
        Assert.Equal(Some true, dto.PuedeConfirmarse)
        Assert.Single(dto.Lineas) |> ignore
        let l = dto.Lineas.Head
        Assert.Equal(Some 100m, l.PrecioVigente)
        Assert.Equal(Some true, l.StockSuficiente)
        Assert.Equal(Some 2000m, l.StockDisponibleActual)
        Assert.Equal(Some "Gramo", l.StockUnidad)

[<Fact>]
let ``Fase5: proforma vigente con precio cambiado agrega advertencia de precio`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())

    // Precio pactado 100, precio actual 180
    let prod = mkProductoConPrecio prodId 180m true
    let proforma =
        mkProformaVigente proformaId clienteId responsableId
            [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
            EstadoProforma.Vigente

    let mockProforma (_: ProformaId) = async { return Some proforma }
    let mockProducto (_: ProductoId) = async { return Some prod }
    let mockStock (_: ProductoId) = async { return 5000m }
    let resolver (_: ProductoId) = "Semilla Test"

    let res =
        ProformaService.obtenerProformaPorId
            mockProforma mockProducto mockStock resolver
            (let (ProformaId g) = proformaId in g.ToString())
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Single(dto.Advertencias) |> ignore
        let adv = dto.Advertencias.Head
        Assert.Contains("cambió", adv)
        Assert.Contains("100", adv)
        Assert.Contains("180", adv)
        // Total congelado: sigue siendo el pactado (100), no el vigente
        Assert.Equal(100m, dto.Total)
        // Pero el precioVigente refleja 180
        Assert.Equal(Some 180m, dto.Lineas.Head.PrecioVigente)

[<Fact>]
let ``Fase5: proforma vigente con stock insuficiente marca PuedeConfirmarse false`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())

    let prod = mkProductoConPrecio prodId 100m true
    // Proforma pide 5 kg, pero stock = 3 kg (3000 g)
    let proforma =
        mkProformaVigente proformaId clienteId responsableId
            [ { ProductoId = prodId; Cantidad = cantReconst 5m Kilogramo; PrecioUnitario = 100m } ]
            EstadoProforma.Vigente

    let mockProforma (_: ProformaId) = async { return Some proforma }
    let mockProducto (_: ProductoId) = async { return Some prod }
    let mockStock (_: ProductoId) = async { return 3000m }
    let resolver (_: ProductoId) = "Semilla Test"

    let res =
        ProformaService.obtenerProformaPorId
            mockProforma mockProducto mockStock resolver
            (let (ProformaId g) = proformaId in g.ToString())
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Equal(Some false, dto.PuedeConfirmarse)
        Assert.Contains(dto.Advertencias, fun a -> a.Contains("Stock insuficiente"))
        Assert.Equal(Some false, dto.Lineas.Head.StockSuficiente)

[<Fact>]
let ``Fase5: proforma Convertida no enriquece campos dinamicos`` () =
    let prodId = ProductoId (Guid.NewGuid())
    let proformaId = ProformaId (Guid.NewGuid())
    let clienteId = ClienteId (Guid.NewGuid())
    let responsableId = UsuarioId (Guid.NewGuid())

    let prod = mkProductoConPrecio prodId 999m true
    let proforma =
        mkProformaVigente proformaId clienteId responsableId
            [ { ProductoId = prodId; Cantidad = cantReconst 1m Kilogramo; PrecioUnitario = 100m } ]
            EstadoProforma.Convertida

    // Verificar que obtenerProducto NO se llame
    let mutable productoLlamado = false
    let mutable stockLlamado = false
    let mockProforma (_: ProformaId) = async { return Some proforma }
    let mockProducto (_: ProductoId) = async { productoLlamado <- true; return Some prod }
    let mockStock (_: ProductoId) = async { stockLlamado <- true; return 5000m }
    let resolver (_: ProductoId) = "Semilla Test"

    let res =
        ProformaService.obtenerProformaPorId
            mockProforma mockProducto mockStock resolver
            (let (ProformaId g) = proformaId in g.ToString())
        |> Async.RunSynchronously

    match res with
    | Error (msg, _) -> failwith msg
    | Ok dto ->
        Assert.Null(dto.PuedeConfirmarse)
        Assert.Empty(dto.Advertencias)
        Assert.Null(dto.Lineas.Head.PrecioVigente)
        Assert.Null(dto.Lineas.Head.StockDisponibleActual)
        Assert.Null(dto.Lineas.Head.StockSuficiente)
        Assert.Null(dto.Lineas.Head.StockUnidad)
        Assert.False(productoLlamado, "obtenerProducto no debió llamarse")
        Assert.False(stockLlamado, "obtenerStockProducto no debió llamarse")
