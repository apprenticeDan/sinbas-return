module ProformaTests

open System
open Xunit
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F7: Elaboración de Proformas y Cotizaciones (RF-07 | CU-10)
// ─────────────────────────────────────────────────────────────

let private prodId1 = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
let private prodId2 = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000002"))
let private respId = UsuarioId (Guid.NewGuid())
let private clienteId = ClienteId (Guid.NewGuid())

let private crearCantidad valor unidad =
    match Cantidad.crear valor unidad with
    | Ok c -> c
    | Error e -> failwithf "Cantidad error: %A" e

[<Fact>]
let ``// RF-07 | RN13 - Calculo de subtotal y total aplica redondeo bancario`` () =
    let c1 = crearCantidad 2.5m Kilogramo
    let l1 : LineaCotizada = { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 150.00m }
    Assert.Equal(375.00m, Cotizacion.subtotal l1)

    // Redondeo AwayFromZero: 0.125 * 3 = 0.375 -> 0.38
    let c2 = crearCantidad 0.125m Kilogramo
    let l2 : LineaCotizada = { ProductoId = prodId2; Cantidad = c2; PrecioUnitario = 3.00m }
    Assert.Equal(0.38m, Cotizacion.subtotal l2)

    let total = Cotizacion.total [ l1; l2 ]
    Assert.Equal(375.38m, total)

[<Fact>]
let ``// RF-07 | CU-10 - Crear proforma valida asigna estado Vigente, leyenda legal y calcula total`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 5.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 120.00m } ]
    let fecha = DateOnly(2026, 10, 7)

    match Proforma.crear pid respId (Some clienteId) (Some "Vivero El Pinar") lineas fecha None (Some "BOB") (Some "Cotización inicial") with
    | Ok p ->
        Assert.Equal(pid, p.Id)
        Assert.Equal(EstadoProforma.Vigente, p.Estado)
        Assert.Equal(600.00m, p.Total)
        Assert.Equal("Disponibilidad sujeta a cambios - La proforma no reserva stock", p.Leyenda)
        Assert.Equal(Some "Vivero El Pinar", p.ClienteNombreLibre)
        Assert.Equal(Some (DateOnly(2026, 10, 14)), p.FechaVencimiento) // 7 días por defecto
        Assert.Equal("BOB", p.Moneda)
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF-07 | CU-10 - Crear proforma con lista de lineas vacia retorna ValorRequerido`` () =
    let pid = ProformaId (Guid.NewGuid())
    let fecha = DateOnly(2026, 10, 7)

    match Proforma.crear pid respId None None [] fecha None None None with
    | Error (ValorRequerido msg) ->
        Assert.Contains("al menos una línea", msg)
    | res ->
        failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// RF-07 | CU-10 - Crear proforma con productos duplicados retorna OperacionInvalida`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let c2 = crearCantidad 2.0m Kilogramo
    let lineas =
        [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m }
          { ProductoId = prodId1; Cantidad = c2; PrecioUnitario = 100m } ]
    let fecha = DateOnly(2026, 10, 7)

    match Proforma.crear pid respId None None lineas fecha None None None with
    | Error (OperacionInvalida msg) ->
        Assert.Contains("duplicados", msg)
    | res ->
        failwithf "Se esperaba Error OperacionInvalida, se obtuvo: %A" res

[<Fact>]
let ``// RF-07 | CU-10 - Crear proforma con fecha vencimiento anterior a emision retorna FechaInvalida`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m } ]
    let fecha = DateOnly(2026, 10, 7)
    let fechaVenc = DateOnly(2026, 10, 5) // anterior

    match Proforma.crear pid respId None None lineas fecha (Some fechaVenc) None None with
    | Error (FechaInvalida msg) ->
        Assert.Contains("vencimiento no puede ser anterior", msg)
    | res ->
        failwithf "Se esperaba Error FechaInvalida, se obtuvo: %A" res

[<Fact>]
let ``// RF-07 | RN04 - Proyectar estado determina vencimiento segun fecha actual sin mutacion`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m } ]
    let fecha = DateOnly(2026, 10, 1)
    let vencimiento = DateOnly(2026, 10, 8)

    let p =
        match Proforma.crear pid respId None None lineas fecha (Some vencimiento) None None with
        | Ok prof -> prof
        | Error e -> failwithf "%A" e

    // Antes o en la fecha de vencimiento: Vigente
    Assert.Equal(EstadoProforma.Vigente, Proforma.proyectarEstado (DateOnly(2026, 10, 7)) p)
    Assert.Equal(EstadoProforma.Vigente, Proforma.proyectarEstado (DateOnly(2026, 10, 8)) p)

    // Después de la fecha de vencimiento: Vencida
    Assert.Equal(EstadoProforma.Vencida, Proforma.proyectarEstado (DateOnly(2026, 10, 9)) p)

[<Fact>]
let ``// RF-07 | CU-10 - Anular proforma vigente registra motivo y responsable`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m } ]
    let p =
        match Proforma.crear pid respId None None lineas (DateOnly(2026, 10, 7)) None None None with
        | Ok prof -> prof
        | Error e -> failwithf "%A" e

    match Proforma.anular "Cliente desistió de la compra" respId p with
    | Ok anulada ->
        Assert.Equal(EstadoProforma.Anulada, anulada.Estado)
        Assert.Equal(Some "Cliente desistió de la compra", anulada.AnulacionMotivo)
        Assert.Equal(Some respId, anulada.AnuladoPor)
        Assert.True(anulada.AnuladoEn.IsSome)
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF-07 | CU-10 - Anular proforma sin motivo retorna ValorRequerido`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m } ]
    let p =
        match Proforma.crear pid respId None None lineas (DateOnly(2026, 10, 7)) None None None with
        | Ok prof -> prof
        | Error e -> failwithf "%A" e

    match Proforma.anular "   " respId p with
    | Error (ValorRequerido msg) ->
        Assert.Contains("motivo", msg)
    | res ->
        failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// RF-07 | CU-10 - Anular proforma ya anulada retorna OperacionInvalida`` () =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 1.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 100m } ]
    let p =
        match Proforma.crear pid respId None None lineas (DateOnly(2026, 10, 7)) None None None with
        | Ok prof -> prof
        | Error e -> failwithf "%A" e

    let anulada =
        match Proforma.anular "Motivo 1" respId p with
        | Ok a -> a
        | Error e -> failwithf "%A" e

    match Proforma.anular "Motivo 2" respId anulada with
    | Error (OperacionInvalida msg) ->
        Assert.Contains("ya fue anulada", msg)
    | res ->
        failwithf "Se esperaba Error OperacionInvalida, se obtuvo: %A" res

[<Fact>]
let ``// RF-07 | CU-10 - EstadoProforma parsing y formato textual`` () =
    Assert.Equal("Vigente", EstadoProforma.aTexto EstadoProforma.Vigente)
    Assert.Equal("Vencida", EstadoProforma.aTexto EstadoProforma.Vencida)
    Assert.Equal("Convertida", EstadoProforma.aTexto EstadoProforma.Convertida)
    Assert.Equal("Anulada", EstadoProforma.aTexto EstadoProforma.Anulada)
    Assert.Equal(Ok EstadoProforma.Vigente, EstadoProforma.desdeTexto "vigente")
    Assert.Equal(Ok EstadoProforma.Vencida, EstadoProforma.desdeTexto "Vencida")
    Assert.Equal(Ok EstadoProforma.Convertida, EstadoProforma.desdeTexto "convertida")
    Assert.Equal(Ok EstadoProforma.Anulada, EstadoProforma.desdeTexto "anulada")
    match EstadoProforma.desdeTexto "desconocido" with
    | Error (ValorRequerido _) -> ()
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res
