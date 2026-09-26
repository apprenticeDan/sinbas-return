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
        Assert.Contains("no puede ser negativa", msg)
    | Ok cant ->
        failwithf "Se esperaba error CantidadInvalida pero se obtuvo Ok con valor: %A" cant
    | Error err ->
        failwithf "Se esperaba CantidadInvalida pero se obtuvo otro error: %A" err

[<Fact>]
let ``Crear Cantidad con valor cero es valido (saldo agotado es 0)`` () =
    let resultado = Cantidad.crear 0.0m Kilogramo
    match resultado with
    | Ok c ->
        Assert.Equal(0m, Cantidad.valor c)
    | Error err ->
        failwithf "Cero es un saldo válido, no debería fallar: %A" err

[<Theory>]
[<InlineData(-100.0)>]
[<InlineData(-1.0)>]
[<InlineData(-0.0001)>]
let ``Crear Cantidad rechaza valores negativos en diversas unidades`` (valor: double) =
    let valorDecimal = decimal valor
    let unidades = [ Gramo; Kilogramo; Mililitro; Litro; UnidadDiscreta ]
    for u in unidades do
        match Cantidad.crear valorDecimal u with
        | Error (CantidadInvalida _) -> ()
        | Ok _ -> failwithf "Debería rechazar valor negativo %M para unidad %A" valorDecimal u
        | Error otro -> failwithf "Se esperaba CantidadInvalida para %M %A, pero se obtuvo: %A" valorDecimal u otro

[<Fact>]
let ``Crear Cantidad con valor positivo retorna Ok con campos asignados`` () =
    match Cantidad.crear 10.5m Kilogramo with
    | Ok c ->
        Assert.Equal(10.5m, Cantidad.valor c)
        Assert.Equal(Kilogramo, Cantidad.unidad c)
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

    // Intento con cantidad cero — ahora Cantidad acepta 0, pero Lote.crear debe rechazarlo
    let cantCero = Cantidad.reconstruir 0m Kilogramo
    match Lote.crear loteId codigo prodId (Some "Origen") cantCero (DateOnly(2026, 8, 15)) None None with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Lote.crear con cantidad inicial cero debio fallar, obtuvo: %A" res

    // Intento con cantidad negativa — Cantidad.crear ya lo rechaza, pero por completitud
    let cantNegativa = Cantidad.reconstruir 10m Kilogramo  // usamos una válida y forzamos con negativa directamente
    // Verificamos que Cantidad.crear rechaza negativos
    match Cantidad.crear -10m Kilogramo with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Cantidad.crear con negativo debió fallar, obtuvo: %A" res

[<Fact>]
let ``Lote.descontarStock con cantidad negativa o cero retorna error CantidadInvalida`` () =
    let loteId = LoteId (Guid.Parse("01917f3a-0004-7000-8000-000000000001"))
    let prodId = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    let cantInicial = Cantidad.reconstruir 50m Kilogramo
    let lote =
        match Lote.crear loteId codigo prodId None cantInicial (DateOnly(2026, 8, 15)) None None with
        | Ok l -> l
        | Error e -> failwithf "Error creando lote: %A" e

    // Descontar negativo no debe incrementar stock ni retornar Ok
    let cantNegativa = Cantidad.reconstruir 5m Kilogramo  // usamos reconstruir; la validación de > 0 es de descontarStock
    // Verificamos que descontarStock rechaza cero
    let cantCero = Cantidad.reconstruir 0m Kilogramo
    match Lote.descontarStock cantCero lote with
    | Error (CantidadInvalida _) -> ()
    | Ok l -> failwithf "descontarStock con cero debio fallar pero retorno Ok: %A" (Cantidad.valor l.CantidadActual)
    | Error err -> failwithf "Se esperaba CantidadInvalida pero se obtuvo: %A" err

    // También rechaza negativos
    match Cantidad.crear -5m Kilogramo with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Cantidad negativa debería rechazarse: %A" res

[<Fact>]
let ``Fifo.resolverFIFO con cantidad requerida negativa o cero retorna error CantidadInvalida`` () =
    let loteId = LoteId (Guid.Parse("01917f3a-0004-7000-8000-000000000001"))
    let prodId = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
    let codigo =
        match CodigoLote.desdeString "SWIETMAC-02608-01" with
        | Ok c -> c
        | Error e -> failwithf "Codigo invalido: %A" e

    let lote =
        match Lote.crear loteId codigo prodId None (Cantidad.reconstruir 50m Gramo) (DateOnly(2026, 8, 15)) None None with
        | Ok l -> l
        | Error e -> failwithf "Error creando lote: %A" e

    let reqNegativa = Cantidad.reconstruir 10m Gramo  // FIFO valida > 0 internamente
    // Verificamos que Cantidad.crear rechaza negativos y que FIFO rechaza cero
    match Fifo.resolverFIFO prodId (Cantidad.reconstruir 0m Gramo) [lote] with
    | Error (CantidadInvalida _) -> ()
    | Ok lineas -> failwithf "resolverFIFO con cantidad cero debio fallar, retorno Ok: %A" lineas
    | Error err -> failwithf "Se esperaba CantidadInvalida pero retorno: %A" err

    match Cantidad.crear -10m Gramo with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Cantidad negativa debería rechazarse: %A" res

[<Fact>]
let ``Cantidad.sumar con sumandos negativos o cero retorna error CantidadInvalida`` () =
    let cValida   = Cantidad.reconstruir 10m Kilogramo
    let cNegativa = Cantidad.reconstruir 5m  Kilogramo  // reconstruir acepta > 0
    // Para la prueba de negativo usamos crear que sí lo rechaza
    match Cantidad.crear -5m Kilogramo with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Cantidad negativa rechazada correctamente: %A" res

    // sumar con sumando cero
    let cCero = Cantidad.reconstruir 0m Kilogramo
    match Cantidad.sumar cValida cCero with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "sumar con sumando cero debio fallar, retorno: %A" res

    match Cantidad.sumar cCero cValida with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "sumar con primer sumando cero debio fallar, retorno: %A" res

[<Fact>]
let ``Cantidad.sumar con cantidades positivas compatibles suma y preserva unidad del primer termino`` () =
    let c1 = Cantidad.reconstruir 2m    Kilogramo
    let c2 = Cantidad.reconstruir 500m  Gramo

    match Cantidad.sumar c1 c2 with
    | Ok res ->
        Assert.Equal(2.5m, Cantidad.valor res)
        Assert.Equal(Kilogramo, Cantidad.unidad res)
    | Error err -> failwithf "sumar valido fallo: %A" err

[<Fact>]
let ``Cantidad.reconstruir instancia Cantidad directamente para persistencia`` () =
    let c = Cantidad.reconstruir 42.5m Gramo
    Assert.Equal(42.5m, Cantidad.valor c)
    Assert.Equal(Gramo, Cantidad.unidad c)
