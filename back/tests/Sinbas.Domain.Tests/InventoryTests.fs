module Sinbas.Domain.Tests.InventoryTests

open System
open Xunit
open Sinbas.Domain

[<Fact>]
let ``Stock de un lote acumula entradas y salidas correctamente`` () =
    let loteId = LoteId (Identidad.nuevo ())
    let productoId = ProductoId (Identidad.nuevo ())
    let empleadoId = EmpleadoId (Identidad.nuevo ())

    let mov1 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-2.0)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña 2026")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = unwrap (Cantidad.reconstruir 1000m Gramo) } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-1.0)
          Responsable = empleadoId
          Tipo = Entrada (Compra (ProveedorId (Identidad.nuevo ())))
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = unwrap (Cantidad.reconstruir 500m Gramo) } ]
          Observaciones = None }

    let mov3 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow
          Responsable = empleadoId
          Tipo = Salida (UsoInterno "Pruebas de viabilidad")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = unwrap (Cantidad.reconstruir 300m Gramo) } ]
          Observaciones = None }

    let movimientos = [ mov1; mov2; mov3 ]
    let stock = Stock.cantidadLote loteId movimientos

    Assert.Equal(1200m, stock)

[<Fact>]
let ``Stock disponible para venta excluye lotes bloqueados`` () =
    let productoId = ProductoId (Identidad.nuevo ())
    let empleadoId = EmpleadoId (Identidad.nuevo ())
    let parseCodigo s = match CodigoLote.desdeString s with Ok c -> c | Error e -> failwithf "%A" e
    let codigo1 = parseCodigo "SWIETMAC-02608-01"
    let codigo2 = parseCodigo "SWIETMAC-02608-02"

    let loteActivo : Lote =
        { Id = LoteId (Identidad.nuevo ())
          Codigo = codigo1
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = unwrap (Cantidad.reconstruir 5000m Gramo)
          CantidadActual  = unwrap (Cantidad.reconstruir 5000m Gramo)
          FechaIngreso = DateOnly(2026, 8, 1)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let loteBloqueado : Lote =
        { Id = LoteId (Identidad.nuevo ())
          Codigo = codigo2
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = unwrap (Cantidad.reconstruir 3000m Gramo)
          CantidadActual  = unwrap (Cantidad.reconstruir 3000m Gramo)
          FechaIngreso = DateOnly(2026, 8, 5)
          Ubicacion = None
          Estado = Bloqueado
          Observaciones = Some "Pendiente de verificación" }

    let mov1 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-5.0)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña Don Mario")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteActivo.Id; Cantidad = unwrap (Cantidad.reconstruir 5000m Gramo) } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-3.0)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña Don Mario")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteBloqueado.Id; Cantidad = unwrap (Cantidad.reconstruir 3000m Gramo) } ]
          Observaciones = None }

    let lotes = [ loteActivo; loteBloqueado ]
    let movimientos = [ mov1; mov2 ]

    let stockTotal = Stock.stockProducto productoId lotes
    let stockVenta = Stock.disponibleParaVenta productoId lotes
    let stockLoteBloqueado = Stock.cantidadLote loteBloqueado.Id movimientos

    Assert.Equal(5000m, stockTotal)
    Assert.Equal(5000m, stockVenta)
    Assert.Equal(3000m, stockLoteBloqueado)

[<Fact>]
let ``Fifo resuelve asignación de lotes en orden cronológico`` () =
    let productoId = ProductoId (Identidad.nuevo ())
    let empleadoId = EmpleadoId (Identidad.nuevo ())
    let loteViejoId = LoteId (Identidad.nuevo ())
    let loteNuevoId = LoteId (Identidad.nuevo ())

    let parseCodigo s = match CodigoLote.desdeString s with Ok c -> c | Error e -> failwithf "%A" e
    let loteViejo : Lote =
        { Id = loteViejoId
          Codigo = parseCodigo "SWIETMAC-02607-01"
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = unwrap (Cantidad.reconstruir 2000m Gramo)
          CantidadActual  = unwrap (Cantidad.reconstruir 2000m Gramo)
          FechaIngreso = DateOnly(2026, 7, 10)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let loteNuevo : Lote =
        { Id = loteNuevoId
          Codigo = parseCodigo "SWIETMAC-02608-01"
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = unwrap (Cantidad.reconstruir 5000m Gramo)
          CantidadActual  = unwrap (Cantidad.reconstruir 5000m Gramo)
          FechaIngreso = DateOnly(2026, 8, 15)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let mov1 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime(2026, 7, 10)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña 1")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteViejoId; Cantidad = unwrap (Cantidad.reconstruir 2000m Gramo) } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime(2026, 8, 15)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña 2")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteNuevoId; Cantidad = unwrap (Cantidad.reconstruir 5000m Gramo) } ]
          Observaciones = None }

    let lotes = [ loteNuevo; loteViejo ] // Enviados en cualquier orden
    let movimientos = [ mov1; mov2 ]

    // Pedir 3000 gramos: debe tomar 2000g del lote viejo y 1000g del lote nuevo
    let requerida = unwrap (Cantidad.reconstruir 3000m Gramo)
    let resFifo = Fifo.resolverFIFO productoId requerida lotes

    match resFifo with
    | Error err -> Assert.True(false, sprintf "Fallo FIFO: %A" err)
    | Ok lineasAsignadas ->
        Assert.Equal(2, lineasAsignadas.Length)
        Assert.Equal(loteViejoId, lineasAsignadas.[0].Referencia)
        Assert.Equal(2000m, Cantidad.valor lineasAsignadas.[0].Cantidad)
        Assert.Equal(loteNuevoId, lineasAsignadas.[1].Referencia)
        Assert.Equal(1000m, Cantidad.valor lineasAsignadas.[1].Cantidad)

[<Fact>]
let ``Fifo retorna error cuando la cantidad requerida supera el stock disponible`` () =
    let productoId = ProductoId (Identidad.nuevo ())
    let empleadoId = EmpleadoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let parseCodigo s = match CodigoLote.desdeString s with Ok c -> c | Error e -> failwithf "%A" e

    let lote : Lote =
        { Id = loteId
          Codigo = parseCodigo "SWIETMAC-02607-01"
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = unwrap (Cantidad.reconstruir 1000m Gramo)
          CantidadActual  = unwrap (Cantidad.reconstruir 1000m Gramo)
          FechaIngreso = DateOnly(2026, 7, 10)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let requerida = unwrap (Cantidad.reconstruir 2000m Gramo)
    let resFifo = Fifo.resolverFIFO productoId requerida [ lote ]

    match resFifo with
    | Error (StockInsuficiente _) -> Assert.True(true)
    | other -> Assert.True(false, sprintf "Se esperaba StockInsuficiente pero se obtuvo: %A" other)

[<Theory>]
[<InlineData("Invalido")>]
[<InlineData("")>]
[<InlineData("Transferencia")>]
[<InlineData(null)>]
let ``TipoMovimiento.resolver rechaza tipos desconocidos y nunca fabrica Entrada`` (tipoInvalido: string) =
    let res = TipoMovimiento.resolver tipoInvalido "Compra" (Some (Guid.NewGuid())) None None None None
    match res with
    | Error (ValorRequerido _) -> () // Correcto: no inventa Entrada
    | Ok tipo -> failwithf "FALLO DE SEGURIDAD: Se fabricó el movimiento %A para tipo inválido '%s'" tipo tipoInvalido
    | Error err -> failwithf "Retornó error no esperado: %A" err

[<Fact>]
let ``TipoMovimiento.resolver rechaza Compra sin ProveedorId y nunca inventa UUID quemado`` () =
    let res = TipoMovimiento.resolver "Entrada" "Compra" None None None None None
    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("contraparte_ref", msg)
    | Ok tipo -> failwithf "FALLO DE SEGURIDAD: Se fabricó UUID para Compra sin proveedor: %A" tipo
    | Error err -> failwithf "Retornó error no esperado: %A" err

[<Fact>]
let ``TipoMovimiento.resolver rechaza Venta sin ClienteId y nunca inventa UUID quemado`` () =
    let res = TipoMovimiento.resolver "Salida" "Venta" None None None None None
    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("contraparte_ref", msg)
    | Ok tipo -> failwithf "FALLO DE SEGURIDAD: Se fabricó UUID para Venta sin cliente: %A" tipo
    | Error err -> failwithf "Retornó error no esperado: %A" err

[<Fact>]
let ``TipoMovimiento.resolver resuelve entradas y salidas validas correctamente`` () =
    let provId = Guid.NewGuid()
    let cliId = Guid.NewGuid()

    let entradaRes = TipoMovimiento.resolver "Entrada" "Compra" (Some provId) None None None None
    Assert.Equal(Ok (Entrada (Compra (ProveedorId provId))), entradaRes)

    let salidaRes = TipoMovimiento.resolver "Salida" "Venta" (Some cliId) None None None None
    Assert.Equal(Ok (Salida (Venta (ClienteId cliId))), salidaRes)


