module Sinbas.Domain.Tests.IntegridadTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

type DummyRow =
    { Id: Guid
      CantidadStr: string
      Valor: decimal }

[<Fact>]
let ``LecturaColeccion.particionar separa correctamente registros validos de inconsistencias`` () =
    let rows =
        [ { Id = Guid.NewGuid(); CantidadStr = "10"; Valor = 10m }
          { Id = Guid.NewGuid(); CantidadStr = "-5"; Valor = -5m } // Corrupto
          { Id = Guid.NewGuid(); CantidadStr = "20"; Valor = 20m }
          { Id = Guid.NewGuid(); CantidadStr = "-99"; Valor = -99m } // Corrupto
          { Id = Guid.NewGuid(); CantidadStr = "30"; Valor = 30m } ]

    let mapper (r: DummyRow) : Result<Cantidad, ErrorIntegridad> =
        match Cantidad.reconstruir r.Valor Gramo with
        | Ok c -> Ok c
        | Error err ->
            Error { Entidad = "dummy"
                    RegistroId = string r.Id
                    Campo = "valor"
                    ValorCrudo = Some (string r.Valor)
                    ErrorDominio = err }

    let coleccion = LecturaColeccion.particionar mapper rows

    Assert.Equal(3, coleccion.Validos.Length)
    Assert.Equal(2, coleccion.Inconsistencias.Length)
    Assert.True(LecturaColeccion.tieneInconsistencias coleccion)
    Assert.Equal(5, LecturaColeccion.totalFilas coleccion)

    // Verificar metadatos de la primera inconsistencia
    let err1 = coleccion.Inconsistencias.[0]
    Assert.Equal("dummy", err1.Entidad)
    Assert.Equal("valor", err1.Campo)
    Assert.Equal(Some "-5", err1.ValorCrudo)
    match err1.ErrorDominio with
    | CantidadInvalida msg -> Assert.Contains("-5", msg)
    | other -> failwithf "Se esperaba CantidadInvalida, pero se obtuvo %A" other

[<Fact>]
let ``LecturaColeccion con filas 100% validas no reporta inconsistencias`` () =
    let rows =
        [ { Id = Guid.NewGuid(); CantidadStr = "10"; Valor = 10m }
          { Id = Guid.NewGuid(); CantidadStr = "20"; Valor = 20m } ]

    let mapper (r: DummyRow) : Result<Cantidad, ErrorIntegridad> =
        match Cantidad.reconstruir r.Valor Gramo with
        | Ok c -> Ok c
        | Error err ->
            Error { Entidad = "dummy"
                    RegistroId = string r.Id
                    Campo = "valor"
                    ValorCrudo = Some (string r.Valor)
                    ErrorDominio = err }

    let coleccion = LecturaColeccion.particionar mapper rows

    Assert.Equal(2, coleccion.Validos.Length)
    Assert.Empty(coleccion.Inconsistencias)
    Assert.False(LecturaColeccion.tieneInconsistencias coleccion)
