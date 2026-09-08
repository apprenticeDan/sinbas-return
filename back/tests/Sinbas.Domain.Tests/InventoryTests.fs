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
          Lineas = [ { Referencia = loteId; Cantidad = { Valor = 1000m; Unidad = Gramo } } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-1.0)
          Responsable = empleadoId
          Tipo = Entrada (Compra (ProveedorId (Identidad.nuevo ())))
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = { Valor = 500m; Unidad = Gramo } } ]
          Observaciones = None }

    let mov3 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow
          Responsable = empleadoId
          Tipo = Salida (UsoInterno "Pruebas de viabilidad")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = { Valor = 300m; Unidad = Gramo } } ]
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
          CantidadInicial = { Valor = 5000m; Unidad = Gramo }
          CantidadActual = { Valor = 5000m; Unidad = Gramo }
          FechaIngreso = DateOnly(2026, 8, 1)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let loteBloqueado : Lote =
        { Id = LoteId (Identidad.nuevo ())
          Codigo = codigo2
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = { Valor = 3000m; Unidad = Gramo }
          CantidadActual = { Valor = 3000m; Unidad = Gramo }
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
          Lineas = [ { Referencia = loteActivo.Id; Cantidad = { Valor = 5000m; Unidad = Gramo } } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime.UtcNow.AddDays(-3.0)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña Don Mario")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteBloqueado.Id; Cantidad = { Valor = 3000m; Unidad = Gramo } } ]
          Observaciones = None }

    let lotes = [ loteActivo; loteBloqueado ]
    let movimientos = [ mov1; mov2 ]

    let stockTotal = Stock.stockProducto productoId lotes movimientos
    let stockVenta = Stock.disponibleParaVenta productoId lotes movimientos
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
          CantidadInicial = { Valor = 2000m; Unidad = Gramo }
          CantidadActual = { Valor = 2000m; Unidad = Gramo }
          FechaIngreso = DateOnly(2026, 7, 10)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let loteNuevo : Lote =
        { Id = loteNuevoId
          Codigo = parseCodigo "SWIETMAC-02608-01"
          ProductoId = productoId
          Procedencia = None
          CantidadInicial = { Valor = 5000m; Unidad = Gramo }
          CantidadActual = { Valor = 5000m; Unidad = Gramo }
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
          Lineas = [ { Referencia = loteViejoId; Cantidad = { Valor = 2000m; Unidad = Gramo } } ]
          Observaciones = None }

    let mov2 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime(2026, 8, 15)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña 2")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteNuevoId; Cantidad = { Valor = 5000m; Unidad = Gramo } } ]
          Observaciones = None }

    let lotes = [ loteNuevo; loteViejo ] // Enviados en cualquier orden
    let movimientos = [ mov1; mov2 ]

    // Pedir 3000 gramos: debe tomar 2000g del lote viejo y 1000g del lote nuevo
    let requerida = { Valor = 3000m; Unidad = Gramo }
    let resFifo = Fifo.resolverFIFO productoId requerida lotes movimientos

    match resFifo with
    | Error err -> Assert.True(false, sprintf "Fallo FIFO: %A" err)
    | Ok lineasAsignadas ->
        Assert.Equal(2, lineasAsignadas.Length)
        Assert.Equal(loteViejoId, lineasAsignadas.[0].Referencia)
        Assert.Equal(2000m, lineasAsignadas.[0].Cantidad.Valor)
        Assert.Equal(loteNuevoId, lineasAsignadas.[1].Referencia)
        Assert.Equal(1000m, lineasAsignadas.[1].Cantidad.Valor)

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
          CantidadInicial = { Valor = 1000m; Unidad = Gramo }
          CantidadActual = { Valor = 1000m; Unidad = Gramo }
          FechaIngreso = DateOnly(2026, 7, 10)
          Ubicacion = None
          Estado = Activo
          Observaciones = None }

    let mov1 : MovimientoInventario =
        { Id = MovimientoId (Identidad.nuevo ())
          Fecha = DateTime(2026, 7, 10)
          Responsable = empleadoId
          Tipo = Entrada (Recoleccion "Campaña 1")
          OrdenOrigen = None
          Lineas = [ { Referencia = loteId; Cantidad = { Valor = 1000m; Unidad = Gramo } } ]
          Observaciones = None }

    let requerida = { Valor = 2000m; Unidad = Gramo }
    let resFifo = Fifo.resolverFIFO productoId requerida [ lote ] [ mov1 ]

    match resFifo with
    | Error (StockInsuficiente _) -> Assert.True(true)
    | other -> Assert.True(false, sprintf "Se esperaba StockInsuficiente pero se obtuvo: %A" other)

