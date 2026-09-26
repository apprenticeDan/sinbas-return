module LoteTests

open System
open Xunit
open Sinbas.Domain

let prodGuid = Guid.Parse("01917f3a-0003-7000-8000-000000000001")
let prodId = ProductoId prodGuid

let sampleLoteGuid = Guid.Parse("01917f3a-0004-7000-8000-000000000001")
let loteId = LoteId sampleLoteGuid

let crearLoteValido () =
    let codigo =
        match CodigoLote.generar "Swietenia" "macrophylla" (DateOnly(2026, 8, 15)) 1 with
        | Ok c -> c
        | Error e -> failwithf "Error generando código de lote: %A" e

    let cantInicial = Cantidad.reconstruir 50m Kilogramo

    match Lote.crear loteId codigo prodId (Some "Bosque Chiquitano") cantInicial (DateOnly(2026, 8, 15)) (Some "Almacén Central") (Some "Observación inicial") with
    | Ok l -> l
    | Error e -> failwithf "Error creando lote: %A" e

[<Fact>]
let ``Creacion de Lote valido inicia en estado Activo con CantidadInicial y CantidadActual identicas`` () =
    let lote = crearLoteValido ()
    Assert.Equal(Activo, lote.Estado)
    Assert.True(Lote.estaActivo lote)
    Assert.Equal(50m, Cantidad.valor lote.CantidadInicial)
    Assert.Equal(50m, Cantidad.valor lote.CantidadActual)
    Assert.Equal(Kilogramo, Cantidad.unidad lote.CantidadInicial)
    Assert.Equal(Some "Bosque Chiquitano", lote.Procedencia)

[<Fact>]
let ``Descuento parcial de stock preserva estado Activo`` () =
    let lote = crearLoteValido ()
    let aDescontar = Cantidad.reconstruir 10m Kilogramo

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteActualizado ->
        Assert.Equal(Activo, loteActualizado.Estado)
        Assert.Equal(40000m, Cantidad.valor loteActualizado.CantidadActual) // 40 kg en gramos
        Assert.Equal(Gramo, Cantidad.unidad loteActualizado.CantidadActual)

[<Fact>]
let ``Descuento total de stock cambia automaticamente estado a Agotado`` () =
    let lote = crearLoteValido ()
    let aDescontar = Cantidad.reconstruir 50m Kilogramo

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteAgotado ->
        Assert.Equal(Agotado, loteAgotado.Estado)
        Assert.Equal(0m, Cantidad.valor loteAgotado.CantidadActual)
        Assert.False(Lote.estaActivo loteAgotado)

[<Fact>]
let ``Intento de descontar mas stock del disponible retorna error StockInsuficiente`` () =
    let lote = crearLoteValido ()
    let aDescontar = Cantidad.reconstruir 60m Kilogramo

    match Lote.descontarStock aDescontar lote with
    | Error (StockInsuficiente msg) -> Assert.Contains("insuficiente", msg)
    | res -> failwithf "Debería haber fallado por stock insuficiente: %A" res

[<Fact>]
let ``Lote.actualizarSaldo actualiza saldo y transiciona bidireccionalmente entre Activo y Agotado`` () =
    let lote = crearLoteValido ()
    let agotado = Lote.actualizarSaldo 0m lote
    Assert.Equal(Agotado, agotado.Estado)
    Assert.Equal(0m, Cantidad.valor agotado.CantidadActual)

    let reactivado = Lote.actualizarSaldo 1500m agotado
    Assert.Equal(Activo, reactivado.Estado)
    Assert.Equal(1500m, Cantidad.valor reactivado.CantidadActual)

[<Fact>]
let ``Bloqueo de lote acumula observaciones de justificacion inmutablemente`` () =
    let lote = crearLoteValido ()
    let bloqueado = Lote.bloquear "Alerta de plaga por muestra de laboratorio" lote

    Assert.Equal(Bloqueado, bloqueado.Estado)
    Assert.False(Lote.estaActivo bloqueado)
    Assert.Contains("[BLOQUEADO]: Alerta de plaga", bloqueado.Observaciones.Value)
