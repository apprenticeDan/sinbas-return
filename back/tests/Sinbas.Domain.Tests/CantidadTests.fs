module CantidadTests

open System
open Xunit
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// REGRESIÓN CRÍTICA: Cantidad debe rechazar valores negativos o cero
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Crear Cantidad con valor negativo retorna error CantidadInvalida`` () =
    let resultado = Cantidad.crear -5.0m Kilogramo
    match resultado with
    | Error (CantidadInvalida msg) ->
        Assert.Contains("mayor a cero", msg)
    | Ok cant ->
        failwithf "Se esperaba error CantidadInvalida pero se obtuvo Ok con valor: %A" cant
    | Error err ->
        failwithf "Se esperaba CantidadInvalida pero se obtuvo otro error: %A" err

[<Fact>]
let ``Crear Cantidad con valor cero retorna error CantidadInvalida`` () =
    let resultado = Cantidad.crear 0.0m Kilogramo
    match resultado with
    | Error (CantidadInvalida msg) ->
        Assert.Contains("mayor a cero", msg)
    | Ok cant ->
        failwithf "Se esperaba error CantidadInvalida al crear cantidad con cero, pero se obtuvo Ok: %A" cant
    | Error err ->
        failwithf "Se esperaba CantidadInvalida pero se obtuvo otro error: %A" err

[<Theory>]
[<InlineData(-100.0)>]
[<InlineData(-1.0)>]
[<InlineData(-0.0001)>]
[<InlineData(0.0)>]
let ``Crear Cantidad rechaza multiples valores no positivos en diversas unidades`` (valor: double) =
    let valorDecimal = decimal valor
    let unidades = [ Gramo; Kilogramo; Mililitro; Litro; UnidadDiscreta ]
    for u in unidades do
        match Cantidad.crear valorDecimal u with
        | Error (CantidadInvalida _) -> ()
        | Ok _ -> failwithf "Debería rechazar valor %M para unidad %A" valorDecimal u
        | Error otro -> failwithf "Se esperaba CantidadInvalida para %M %A, pero se obtuvo: %A" valorDecimal u otro

[<Fact>]
let ``Crear Cantidad con valor positivo retorna Ok con campos asignados`` () =
    match Cantidad.crear 10.5m Kilogramo with
    | Ok c ->
        Assert.Equal(10.5m, c.Valor)
        Assert.Equal(Kilogramo, c.Unidad)
    | Error err ->
        failwithf "Fallo inesperado al crear Cantidad positiva: %A" err

// ─────────────────────────────────────────────────────────────
// Invariantes de Lote y FIFO ante cantidades negativas
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Lote.crear con cantidad inicial negativa o cero retorna error CantidadInvalida`` () =
    let loteId = LoteId (Guid.Parse("01917f3a-0004-7000-8000-000000000001"))
    let prodId = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    // Intento con cantidad cero
    let cantCero = { Valor = 0m; Unidad = Kilogramo }
    match Lote.crear loteId codigo prodId (Some "Origen") cantCero (DateOnly(2026, 8, 15)) None None with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Lote.crear con cantidad inicial cero debio fallar, obtuvo: %A" res

    // Intento con cantidad negativa
    let cantNegativa = { Valor = -10m; Unidad = Kilogramo }
    match Lote.crear loteId codigo prodId (Some "Origen") cantNegativa (DateOnly(2026, 8, 15)) None None with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Lote.crear con cantidad inicial negativa debio fallar, obtuvo: %A" res

[<Fact>]
let ``Lote.descontarStock con cantidad negativa o cero retorna error CantidadInvalida`` () =
    let loteId = LoteId (Guid.Parse("01917f3a-0004-7000-8000-000000000001"))
    let prodId = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    let cantInicial = { Valor = 50m; Unidad = Kilogramo }
    let lote =
        match Lote.crear loteId codigo prodId None cantInicial (DateOnly(2026, 8, 15)) None None with
        | Ok l -> l
        | Error e -> failwithf "Error creando lote: %A" e

    // Descontar negativo no debe incrementar stock ni retornar Ok
    let cantNegativa = { Valor = -5m; Unidad = Kilogramo }
    match Lote.descontarStock cantNegativa lote with
    | Error (CantidadInvalida _) -> ()
    | Ok l -> failwithf "descontarStock con cantidad negativa debio fallar pero retorno Ok con stock: %A" l.CantidadActual
    | Error err -> failwithf "Se esperaba CantidadInvalida pero se obtuvo: %A" err

    // Descontar cero tampoco es una operación válida de egreso
    let cantCero = { Valor = 0m; Unidad = Kilogramo }
    match Lote.descontarStock cantCero lote with
    | Error (CantidadInvalida _) -> ()
    | Ok l -> failwithf "descontarStock con cero debio fallar pero retorno Ok: %A" l.CantidadActual
    | Error err -> failwithf "Se esperaba CantidadInvalida pero se obtuvo: %A" err

[<Fact>]
let ``Fifo.resolverFIFO con cantidad requerida negativa o cero retorna error CantidadInvalida`` () =
    let loteId = LoteId (Guid.Parse("01917f3a-0004-7000-8000-000000000001"))
    let prodId = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    let lote =
        match Lote.crear loteId codigo prodId None { Valor = 50m; Unidad = Gramo } (DateOnly(2026, 8, 15)) None None with
        | Ok l -> l
        | Error e -> failwithf "Error creando lote: %A" e

    let reqNegativa = { Valor = -10m; Unidad = Gramo }
    match Fifo.resolverFIFO prodId reqNegativa [lote] with
    | Error (CantidadInvalida _) -> ()
    | Ok lineas -> failwithf "resolverFIFO con cantidad negativa debio fallar, retorno Ok: %A" lineas
    | Error err -> failwithf "Se esperaba CantidadInvalida pero retorno: %A" err

    let reqCero = { Valor = 0m; Unidad = Gramo }
    match Fifo.resolverFIFO prodId reqCero [lote] with
    | Error (CantidadInvalida _) -> ()
    | Ok lineas -> failwithf "resolverFIFO con cantidad cero debio fallar, retorno Ok: %A" lineas
    | Error err -> failwithf "Se esperaba CantidadInvalida pero retorno: %A" err

[<Fact>]
let ``Cantidad.sumar con sumandos negativos o cero retorna error CantidadInvalida`` () =
    let cValida = { Valor = 10m; Unidad = Kilogramo }
    let cNegativa = { Valor = -5m; Unidad = Kilogramo }
    let cCero = { Valor = 0m; Unidad = Kilogramo }

    match Cantidad.sumar cValida cNegativa with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "sumar con sumando negativo debio fallar, retorno: %A" res

    match Cantidad.sumar cNegativa cValida with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "sumar con primer sumando negativo debio fallar, retorno: %A" res

    match Cantidad.sumar cValida cCero with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "sumar con sumando cero debio fallar, retorno: %A" res

[<Fact>]
let ``Cantidad.sumar con cantidades positivas compatibles suma y preserva unidad del primer termino`` () =
    let c1 = { Valor = 2m; Unidad = Kilogramo }
    let c2 = { Valor = 500m; Unidad = Gramo }

    match Cantidad.sumar c1 c2 with
    | Ok res ->
        Assert.Equal(2.5m, res.Valor)
        Assert.Equal(Kilogramo, res.Unidad)
    | Error err -> failwithf "sumar valido fallo: %A" err

[<Fact>]
let ``Cantidad.reconstruir instancia Cantidad directamente para persistencia`` () =
    let c = Cantidad.reconstruir 42.5m Gramo
    Assert.Equal(42.5m, c.Valor)
    Assert.Equal(Gramo, c.Unidad)
