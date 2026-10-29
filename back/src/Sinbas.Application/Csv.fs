module Sinbas.Application.Csv

open System
open System.Globalization
open System.Text

/// Escapa un campo: si contiene ';', '"', '\n' o '\r', lo envuelve en
/// comillas dobles y duplica las comillas internas.
let escape (s: string) : string =
    if String.IsNullOrEmpty s then ""
    else
        let needsQuoting = s.IndexOfAny([| ';'; '"'; '\n'; '\r' |]) >= 0
        if not needsQuoting then s
        else "\"" + s.Replace("\"", "\"\"") + "\""

/// Formato invariante (punto decimal), hasta 4 decimales si aplica.
let fmtDecimal (d: decimal) : string =
    d.ToString("0.00################", CultureInfo.InvariantCulture)

let fmtDate (d: DateOnly) : string = d.ToString("yyyy-MM-dd")
let fmtDateTime (d: DateTime) : string = d.ToString("yyyy-MM-dd HH:mm")

let row (cells: string list) : string =
    cells |> List.map escape |> String.concat ";"

let toCsv (headers: string list) (rows: string list list) : string =
    let sb = StringBuilder()
    sb.Append(row headers).Append("\r\n") |> ignore
    for r in rows do sb.Append(row r).Append("\r\n") |> ignore
    sb.ToString()
