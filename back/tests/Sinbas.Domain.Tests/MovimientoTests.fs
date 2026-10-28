module MovimientoTests

open Xunit
open Sinbas.Domain

[<Fact>]
let ``MotivoIngreso.desdeTexto reconoce Ajuste`` () =
    match MotivoIngreso.desdeTexto "Ajuste" None None with
    | Ok MotivoIngreso.Ajuste -> ()
    | other -> failwithf "esperaba Ok Ajuste, recibí %A" other

[<Fact>]
let ``MotivoIngreso.aTexto Ajuste`` () =
    Assert.Equal("Ajuste", MotivoIngreso.aTexto MotivoIngreso.Ajuste)

[<Fact>]
let ``MotivoEgreso.desdeTexto reconoce Ajuste`` () =
    match MotivoEgreso.desdeTexto "Ajuste" None None None None None with
    | Ok MotivoEgreso.Ajuste -> ()
    | other -> failwithf "esperaba Ok Ajuste, recibí %A" other

[<Fact>]
let ``MotivoEgreso.aTexto Ajuste`` () =
    Assert.Equal("Ajuste", MotivoEgreso.aTexto MotivoEgreso.Ajuste)

[<Fact>]
let ``Round-trip Ajuste ingreso`` () =
    match MotivoIngreso.desdeTexto "Ajuste" None None with
    | Ok m -> Assert.Equal("Ajuste", MotivoIngreso.aTexto m)
    | Error e -> failwithf "esperaba Ok, recibí %A" e

[<Fact>]
let ``Round-trip Ajuste egreso`` () =
    match MotivoEgreso.desdeTexto "Ajuste" None None None None None with
    | Ok m -> Assert.Equal("Ajuste", MotivoEgreso.aTexto m)
    | Error e -> failwithf "esperaba Ok, recibí %A" e
