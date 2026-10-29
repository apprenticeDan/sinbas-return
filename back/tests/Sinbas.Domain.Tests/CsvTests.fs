module CsvTests

open Xunit
open Sinbas.Application

[<Fact>]
let ``escape no toca campos simples`` () =
    let input = "Handroanthus"
    let result = Csv.escape input
    Assert.Equal("Handroanthus", result)

[<Fact>]
let ``escape quotea si contiene punto y coma`` () =
    let input = "Cedrela; odorata"
    let result = Csv.escape input
    Assert.Equal("\"Cedrela; odorata\"", result)

[<Fact>]
let ``escape duplica comillas internas`` () =
    let input = "Lote \"Especial\""
    let result = Csv.escape input
    Assert.Equal("\"Lote \"\"Especial\"\"\"", result)

[<Fact>]
let ``toCsv con headers y 2 filas usa CRLF y delimitador punto y coma`` () =
    let headers = [ "Producto"; "Cantidad" ]
    let rows =
        [ [ "Cedrela"; "10.50" ]
          [ "Swietenia"; "25.00" ] ]
    let csv = Csv.toCsv headers rows
    let expected = "Producto;Cantidad\r\nCedrela;10.50\r\nSwietenia;25.00\r\n"
    Assert.Equal(expected, csv)

[<Fact>]
let ``toCsv vacio solo genera headers con CRLF`` () =
    let headers = [ "ColA"; "ColB" ]
    let rows: string list list = []
    let csv = Csv.toCsv headers rows
    let expected = "ColA;ColB\r\n"
    Assert.Equal(expected, csv)
