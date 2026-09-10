module Sinbas.Domain.Tests.StockTests

open System
open Xunit
open Sinbas.Domain

let private parseCodigo s =
    match CodigoLote.desdeString s with
    | Ok c -> c
    | Error e -> failwithf "%A" e

let private crearLote prodId codigoStr estado cantInicialGramos fechaIngreso =
    { Id = LoteId (Identidad.nuevo ())
      Codigo = parseCodigo codigoStr
      ProductoId = prodId
      Procedencia = None
      CantidadInicial = { Valor = cantInicialGramos; Unidad = Gramo }
      CantidadActual = { Valor = cantInicialGramos; Unidad = Gramo }
      FechaIngreso = fechaIngreso
      Ubicacion = None
      Estado = estado
      Observaciones = None }

let private crearMovEntrada loteId cantGramos fecha responsableId =
    { Id = MovimientoId (Identidad.nuevo ())
      Fecha = fecha
      Responsable = responsableId
      Tipo = Entrada (Recoleccion "Campaña Origen")
      OrdenOrigen = None
      Lineas = [ { Referencia = loteId; Cantidad = { Valor = cantGramos; Unidad = Gramo } } ]
      Observaciones = None }

let private crearMovSalida loteId cantGramos fecha responsableId motivo =
    { Id = MovimientoId (Identidad.nuevo ())
      Fecha = fecha
      Responsable = responsableId
      Tipo = Salida motivo
      OrdenOrigen = None
      Lineas = [ { Referencia = loteId; Cantidad = { Valor = cantGramos; Unidad = Gramo } } ]
      Observaciones = None }

// ─────────────────────────────────────────────────────────────
// MF-05-01: Proyección Pura de Stock Disponible (Fold)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``MF-05-01: Proyección pura calcula stock acumulado por lote y producto con fold`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let empId = EmpleadoId (Identidad.nuevo ())
    let ahora = DateTime.UtcNow

    let lote1 = crearLote prodId "SWIETMAC-02608-01" Activo 5000m (DateOnly(2026, 8, 1))
    let lote2 = crearLote prodId "SWIETMAC-02608-02" Activo 3000m (DateOnly(2026, 8, 5))

    let movs =
        [ crearMovEntrada lote1.Id 5000m (ahora.AddDays(-10.0)) empId
          crearMovEntrada lote2.Id 3000m (ahora.AddDays(-8.0)) empId
          crearMovSalida lote1.Id 1500m (ahora.AddDays(-5.0)) empId (UsoInterno "Laboratorio")
          crearMovSalida lote2.Id 500m (ahora.AddDays(-2.0)) empId (UsoInterno "Vivero") ]

    let stockL1 = Stock.cantidadLote lote1.Id movs
    let stockL2 = Stock.cantidadLote lote2.Id movs
    let stockTotal = Stock.stockProducto prodId [ lote1; lote2 ] movs
    let stockDisponible = Stock.disponibleParaVenta prodId [ lote1; lote2 ] movs

    Assert.Equal(3500m, stockL1)
    Assert.Equal(2500m, stockL2)
    Assert.Equal(6000m, stockTotal)
    Assert.Equal(6000m, stockDisponible)

[<Fact>]
let ``MF-05-01: RN12 Exclusión estricta de lotes en estado Rechazado del stock disponible`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let empId = EmpleadoId (Identidad.nuevo ())
    let ahora = DateTime.UtcNow

    let loteAprobado = crearLote prodId "SWIETMAC-02608-01" Activo 4000m (DateOnly(2026, 8, 1))
    let loteRechazado = crearLote prodId "SWIETMAC-02608-02" Rechazado 4000m (DateOnly(2026, 8, 2))
    let loteBloqueado = crearLote prodId "SWIETMAC-02608-03" Bloqueado 2000m (DateOnly(2026, 8, 3))

    let movs =
        [ crearMovEntrada loteAprobado.Id 4000m (ahora.AddDays(-5.0)) empId
          crearMovEntrada loteRechazado.Id 4000m (ahora.AddDays(-4.0)) empId
          crearMovEntrada loteBloqueado.Id 2000m (ahora.AddDays(-3.0)) empId ]

    let lotes = [ loteAprobado; loteRechazado; loteBloqueado ]
    let stockTotal = Stock.stockProducto prodId lotes movs
    let stockVenta = Stock.disponibleParaVenta prodId lotes movs
    let lotesDisponibles = Stock.lotesDisponibles prodId lotes movs

    // El stock total activo incluye solo lotes activos; disponible para venta excluye Rechazado y Bloqueado
    Assert.Equal(4000m, stockTotal)
    Assert.Equal(4000m, stockVenta)
    Assert.Single(lotesDisponibles) |> ignore
    let (loteDisp, cantDisp) = List.head lotesDisponibles
    Assert.Equal(loteAprobado.Id, loteDisp.Id)
    Assert.Equal(4000m, cantDisp)

// ─────────────────────────────────────────────────────────────
// MF-05-02: Kardex Digital e Historial de Movimientos
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``MF-05-02: Kardex calcula el saldo resultante cronológico paso a paso tras cada movimiento`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let empId = EmpleadoId (Identidad.nuevo ())
    let baseFecha = DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)

    let lote1 = crearLote prodId "SWIETMAC-02608-01" Activo 1000m (DateOnly(2026, 8, 1))

    // Movimientos en desorden temporal para verificar que se ordenen cronológicamente
    let mov1 = crearMovEntrada lote1.Id 1000m (baseFecha.AddDays(0.0)) empId
    let mov2 = crearMovSalida lote1.Id 200m (baseFecha.AddDays(2.0)) empId (UsoInterno "Pruebas")
    let mov3 = crearMovEntrada lote1.Id 500m (baseFecha.AddDays(1.0)) empId
    let mov4 = crearMovSalida lote1.Id 400m (baseFecha.AddDays(3.0)) empId (Venta (ClienteId (Identidad.nuevo ())))

    let movs = [ mov2; mov4; mov1; mov3 ] // Desordenados
    let kardex = Stock.calcularKardexProducto prodId [ lote1 ] movs

    Assert.Equal(4, kardex.Length)
    // 1. Entrada 1000 -> saldo: 1000
    Assert.Equal(1000m, kardex.[0].SaldoResultanteGramos)
    // 2. Entrada 500 -> saldo: 1500
    Assert.Equal(1500m, kardex.[1].SaldoResultanteGramos)
    // 3. Salida 200 -> saldo: 1300
    Assert.Equal(1300m, kardex.[2].SaldoResultanteGramos)
    // 4. Salida 400 -> saldo: 900
    Assert.Equal(900m, kardex.[3].SaldoResultanteGramos)

// ─────────────────────────────────────────────────────────────
// MF-05-03: Indicador Informativo de Stock Mínimo
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``MF-05-03: Evaluación de alerta categoriza SinStock, BajoStock y StockNormal`` () =
    let umbralMinimo = 5000m // 5 kg

    let alertaCero = Stock.evaluarAlertaStock 0m umbralMinimo
    let alertaNegativa = Stock.evaluarAlertaStock -10m umbralMinimo
    let alertaBajoIgual = Stock.evaluarAlertaStock 5000m umbralMinimo
    let alertaBajoMenor = Stock.evaluarAlertaStock 3000m umbralMinimo
    let alertaNormal = Stock.evaluarAlertaStock 5001m umbralMinimo

    Assert.Equal(Stock.SinStock, alertaCero)
    Assert.Equal(Stock.SinStock, alertaNegativa)
    Assert.Equal(Stock.BajoStock umbralMinimo, alertaBajoIgual)
    Assert.Equal(Stock.BajoStock umbralMinimo, alertaBajoMenor)
    Assert.Equal(Stock.StockNormal, alertaNormal)
