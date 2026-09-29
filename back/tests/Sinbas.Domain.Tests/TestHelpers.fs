namespace global

[<AutoOpen>]
module TestHelpers =
    open System
    open Sinbas.Domain

    let unwrap = function
        | Ok x -> x
        | Error e -> failwithf "Test fixture unwrapping failed: %A" e

    let cantReconst (valor: decimal) (unidad: UnidadMedida) : Cantidad =
        match Cantidad.reconstruir valor unidad with
        | Ok c -> c
        | Error e -> failwithf "Test fixture Cantidad.reconstruir failed: %A" e
