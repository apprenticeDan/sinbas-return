module Sinbas.Application.ReporteService

open System
open Sinbas.Domain

let stockCsv
    (consultarStockConsolidado: unit -> Async<StockConsolidadoProductoDto list>)
    : Async<string> =
    async {
        let! productos = consultarStockConsolidado ()
        let headers =
            [ "Producto"; "Categoria"; "Unidad"
              "Stock Total"; "Stock Disp Venta"; "Alerta"
              "Lote"; "Procedencia"; "Fecha Ingreso"; "Estado Lote"
              "Stock Lote" ]
        let rows =
            [ for p in productos do
                if p.Lotes.IsEmpty then
                    yield [ p.NombreProducto; p.Categoria; p.UnidadMedida
                            Csv.fmtDecimal p.StockTotalDisplay
                            Csv.fmtDecimal p.StockDisponibleVentaDisplay
                            p.Alerta
                            ""; ""; ""; ""; "" ]
                else
                    for l in p.Lotes do
                        yield [ p.NombreProducto; p.Categoria; p.UnidadMedida
                                Csv.fmtDecimal p.StockTotalDisplay
                                Csv.fmtDecimal p.StockDisponibleVentaDisplay
                                p.Alerta
                                l.Codigo; l.Procedencia; l.FechaIngreso
                                l.Estado; Csv.fmtDecimal l.StockDisplay ] ]
        return Csv.toCsv headers rows }

let kardexCsv
    (consultarKardex: string option -> string option -> Async<KardexMovimientoDto list>)
    (desde: DateOnly option) (hasta: DateOnly option)
    : Async<string> =
    async {
        let! movimientos = consultarKardex None None
        let inRange (f: string) =
            if String.IsNullOrEmpty f then true
            else
                match DateTime.TryParse f with
                | false, _ -> true
                | true, dt ->
                    let d = DateOnly.FromDateTime dt
                    (match desde with Some x -> d >= x | None -> true)
                    && (match hasta with Some x -> d <= x | None -> true)
        let filtrados = movimientos |> List.filter (fun m -> inRange m.Fecha)
        let headers =
            [ "Fecha"; "Tipo"; "Motivo"; "Producto"; "Lote"
              "Cantidad"; "Unidad"; "Saldo Resultante"
              "Responsable ID"; "Observaciones" ]
        let rows =
            [ for m in filtrados ->
                [ m.Fecha; m.Tipo; m.Motivo; m.NombreProducto; m.CodigoLote
                  Csv.fmtDecimal m.Cantidad; m.Unidad
                  Csv.fmtDecimal m.SaldoResultanteDisplay
                  m.ResponsableId; m.Observaciones ] ]
        return Csv.toCsv headers rows }

let lotesCsv
    (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
    (listarProductos: unit -> Async<Producto list>)
    (obtenerUltimoAnalisis: LoteId -> Async<AnalisisLaboratorio option>)
    : Async<string> =
    async {
        let! lotes = listarLotes None None
        let! productos = listarProductos ()
        let prodMap = productos |> List.map (fun p -> (p.Base.Id, p)) |> Map.ofList
        let headers =
            [ "Codigo Lote"; "Producto"; "Procedencia"
              "Fecha Ingreso"; "Estado"
              "Cantidad Inicial"; "Cantidad Actual"; "Unidad"
              "Ultimo Analisis"; "Dictamen"
              "Germinacion %"; "Pureza %"; "Humedad %"; "Viabilidad %" ]
        let rows = ResizeArray()
        for l in lotes do
            let! analisis = obtenerUltimoAnalisis l.Id
            let prodNombre =
                match Map.tryFind l.ProductoId prodMap with
                | Some p -> Producto.nombreVisible p
                | None -> ""
            let (ultimo, dict, germ, pur, hum, via) =
                match analisis with
                | Some a ->
                    ( a.FechaAnalisis.ToString("yyyy-MM-dd")
                    , DictamenCalidad.aTexto a.Dictamen
                    , Csv.fmtDecimal (PorcentajeCalidad.valor a.Germinacion)
                    , Csv.fmtDecimal (PorcentajeCalidad.valor a.Pureza)
                    , Csv.fmtDecimal (PorcentajeCalidad.valor a.Humedad)
                    , Csv.fmtDecimal (PorcentajeCalidad.valor a.Viabilidad) )
                | None -> ("", "", "", "", "", "")
            rows.Add(
                [ CodigoLote.valor l.Codigo
                  prodNombre
                  (defaultArg l.Procedencia "")
                  l.FechaIngreso.ToString("yyyy-MM-dd")
                  (EstadoLote.aTexto l.Estado)
                  Csv.fmtDecimal l.CantidadInicial.Valor
                  Csv.fmtDecimal l.CantidadActual.Valor
                  (UnidadMedida.aTexto l.CantidadActual.Unidad)
                  ultimo; dict; germ; pur; hum; via ])
        return Csv.toCsv headers (rows |> List.ofSeq) }
