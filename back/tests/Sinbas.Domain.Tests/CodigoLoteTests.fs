module CodigoLoteTests

open System
open Xunit
open Sinbas.Domain

let private fechaEnero2026 = DateOnly(2026, 1, 15)

// ── Generar: padding ──────────────────────────────────────────

[<Fact>]
let ``generar rellena género corto con _`` () =
    match CodigoLote.generar "Test" "ingreso" fechaEnero2026 1 with
    | Ok c -> Assert.Equal("TEST_INGR-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``generar usa primeros 5 chars de género largo`` () =
    match CodigoLote.generar "Alnus" "acuminata" fechaEnero2026 1 with
    | Ok c -> Assert.Equal("ALNUSACUM-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``generar trunca género mayor a 5 chars`` () =
    match CodigoLote.generar "Handroanthus" "impetiginosus" fechaEnero2026 1 with
    | Ok c -> Assert.Equal("HANDRIMPE-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``generar rellena epíteto corto a 4 chars`` () =
    match CodigoLote.generar "Pinus" "alba" fechaEnero2026 1 with
    | Ok c -> Assert.Equal("PINUSALBA-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``generar rechaza secuencia fuera de rango`` () =
    Assert.True(CodigoLote.generar "Alnus" "acuminata" fechaEnero2026 0 |> Result.isError)
    Assert.True(CodigoLote.generar "Alnus" "acuminata" fechaEnero2026 100 |> Result.isError)

// ── Generar: legacy format ────────────────────────────────────

[<Fact>]
let ``generarLegacy produce formato 17 chars sin padding`` () =
    match CodigoLote.generarLegacy "Test" "ingreso" fechaEnero2026 1 with
    | Ok c -> Assert.Equal("TESTING-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

// ── desdeString: acepta legacy y nuevo ────────────────────────

[<Fact>]
let ``desdeString acepta formato nuevo 18 chars`` () =
    Assert.True(CodigoLote.desdeString "ALNUSACUM-02601-01" |> Result.isOk)

[<Fact>]
let ``desdeString acepta formato legacy 17 chars`` () =
    Assert.True(CodigoLote.desdeString "ALNUSACU-02601-01" |> Result.isOk)

[<Fact>]
let ``desdeString acepta legacy corto 16 chars`` () =
    Assert.True(CodigoLote.desdeString "TESTING-02601-01" |> Result.isOk)

[<Fact>]
let ``desdeString normaliza a uppercase`` () =
    match CodigoLote.desdeString "alnusacum-02601-01" with
    | Ok c -> Assert.Equal("ALNUSACUM-02601-01", CodigoLote.valor c)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``desdeString rechaza código vacío`` () =
    Assert.True(CodigoLote.desdeString "" |> Result.isError)
    Assert.True(CodigoLote.desdeString "   " |> Result.isError)

[<Fact>]
let ``desdeString rechaza formato inválido`` () =
    Assert.True(CodigoLote.desdeString "abc" |> Result.isError)
    Assert.True(CodigoLote.desdeString "TOOSHORT-01" |> Result.isError)
    Assert.True(CodigoLote.desdeString "NO-DATE-HERE" |> Result.isError)
    Assert.True(CodigoLote.desdeString "ALNUSACUM-XXX-01" |> Result.isError)

// ── Round-trip ────────────────────────────────────────────────

[<Fact>]
let ``round-trip generar → desdeString`` () =
    match CodigoLote.generar "Alnus" "acuminata" fechaEnero2026 5 with
    | Error e -> failwithf "esperaba Ok, recibí %A" e
    | Ok c ->
        let raw = CodigoLote.valor c
        match CodigoLote.desdeString raw with
        | Ok c2 -> Assert.Equal(raw, CodigoLote.valor c2)
        | Error e -> failwithf "round-trip falló: %A" e
