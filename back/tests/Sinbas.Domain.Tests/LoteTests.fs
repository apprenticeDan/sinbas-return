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

    let cantInicial = { Valor = 50m; Unidad = Kilogramo }

    match Lote.crear loteId codigo prodId (Some "Bosque Chiquitano") cantInicial (DateOnly(2026, 8, 15)) (Some "Almacén Central") (Some "Observación inicial") with
    | Ok l -> l
    | Error e -> failwithf "Error creando lote: %A" e

[<Fact>]
let ``Creacion de Lote valido inicia en estado Activo con CantidadInicial y CantidadActual identicas`` () =
    let lote = crearLoteValido ()
    Assert.Equal(Activo, lote.Estado)
    Assert.True(Lote.estaActivo lote)
    Assert.Equal(50m, lote.CantidadInicial.Valor)
    Assert.Equal(50m, lote.CantidadActual.Valor)
    Assert.Equal(Kilogramo, lote.CantidadInicial.Unidad)
    Assert.Equal(Some "Bosque Chiquitano", lote.Procedencia)

[<Fact>]
let ``Descuento parcial de stock preserva estado Activo`` () =
    let lote = crearLoteValido ()
    let aDescontar = { Valor = 10m; Unidad = Kilogramo }

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteActualizado ->
        Assert.Equal(Activo, loteActualizado.Estado)
        Assert.Equal(40000m, loteActualizado.CantidadActual.Valor) // 40 kg en gramos
        Assert.Equal(Gramo, loteActualizado.CantidadActual.Unidad)

[<Fact>]
let ``Descuento total de stock cambia automaticamente estado a Agotado`` () =
    let lote = crearLoteValido ()
    let aDescontar = { Valor = 50m; Unidad = Kilogramo }

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteAgotado ->
        Assert.Equal(Agotado, loteAgotado.Estado)
        Assert.Equal(0m, loteAgotado.CantidadActual.Valor)
        Assert.False(Lote.estaActivo loteAgotado)

[<Fact>]
let ``Intento de descontar mas stock del disponible retorna error StockInsuficiente`` () =
    let lote = crearLoteValido ()
    let aDescontar = { Valor = 60m; Unidad = Kilogramo }

    match Lote.descontarStock aDescontar lote with
    | Error (StockInsuficiente msg) -> Assert.Contains("insuficiente", msg)
    | res -> failwithf "Debería haber fallado por stock insuficiente: %A" res

[<Fact>]
let ``Bloqueo de lote acumula observaciones de justificacion inmutablemente`` () =
    let lote = crearLoteValido ()
    let bloqueado = Lote.bloquear "Alerta de plaga por muestra de laboratorio" lote

    Assert.Equal(Bloqueado, bloqueado.Estado)
    Assert.False(Lote.estaActivo bloqueado)
    Assert.Contains("[BLOQUEADO]: Alerta de plaga", bloqueado.Observaciones.Value)
