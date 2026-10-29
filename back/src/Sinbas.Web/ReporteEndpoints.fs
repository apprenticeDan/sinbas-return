module Sinbas.Web.ReporteEndpoints

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open System
open System.Text
open Sinbas.Application
open Sinbas.Infrastructure

let private bom = "\uFEFF"

let private csvResult (filenameBase: string) (body: string) : IResult =
    let fecha = DateTime.UtcNow.ToString("yyyy-MM-dd")
    let bytes = Encoding.UTF8.GetBytes(bom + body)
    Results.File(
        bytes,
        contentType = "text/csv; charset=utf-8",
        fileDownloadName = sprintf "%s-%s.csv" filenameBase fecha)

let mapEndpoints (app: WebApplication) =
    // GET /api/reportes/stock.csv
    app.MapGet("/api/reportes/stock.csv", Func<Threading.Tasks.Task<IResult>>(fun () ->
        async {
            let! csv =
                ReporteService.stockCsv (fun () ->
                    async {
                        let! prodsCol = CatalogRepository.listarColeccion ()
                        let! lotesCol = LoteRepository.listarColeccion None None
                        let! movsCol  = InventoryRepository.listarTodosMovimientosDominioColeccion ()
                        return! InventoryService.consultarStockConsolidado
                                    (fun () -> async { return prodsCol.Validos })
                                    (fun p e -> async {
                                        return lotesCol.Validos
                                        |> List.filter (fun l ->
                                            (match p with None -> true | Some pid -> l.ProductoId = pid)
                                            && (match e with None -> true | Some est -> l.Estado = est)) })
                                    (fun () -> async { return movsCol.Validos })
                                    None
                    })
            return csvResult "stock" csv
        } |> Async.StartAsTask ))
        .RequireAuthorization("RequireInventarioLectura")
        .WithName("ReporteStockCsv").WithTags("Reportes") |> ignore

    // GET /api/reportes/kardex.csv?desde=YYYY-MM-DD&hasta=YYYY-MM-DD
    app.MapGet("/api/reportes/kardex.csv", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
        async {
            let parseDate (key: string) =
                if ctx.Request.Query.ContainsKey key then
                    match DateOnly.TryParse(ctx.Request.Query.[key].ToString()) with
                    | true, d -> Some d
                    | false, _ -> None
                else None
            let desde = parseDate "desde"
            let hasta = parseDate "hasta"
            let! csv =
                ReporteService.kardexCsv
                    (fun _ _ ->
                        async {
                            let! prodsCol = CatalogRepository.listarColeccion ()
                            let! lotesCol = LoteRepository.listarColeccion None None
                            let! movsCol  = InventoryRepository.listarTodosMovimientosDominioColeccion ()
                            return! InventoryService.consultarKardex
                                        (fun () -> async { return prodsCol.Validos })
                                        (fun p e -> async {
                                            return lotesCol.Validos
                                            |> List.filter (fun l ->
                                                (match p with None -> true | Some pid -> l.ProductoId = pid)
                                                && (match e with None -> true | Some est -> l.Estado = est)) })
                                        (fun () -> async { return movsCol.Validos })
                                        None None
                        })
                    desde hasta
            return csvResult "kardex" csv
        } |> Async.StartAsTask ))
        .RequireAuthorization("RequireInventarioLectura")
        .WithName("ReporteKardexCsv").WithTags("Reportes") |> ignore

    // GET /api/reportes/lotes.csv
    app.MapGet("/api/reportes/lotes.csv", Func<Threading.Tasks.Task<IResult>>(fun () ->
        async {
            let! csv =
                ReporteService.lotesCsv
                    LoteRepository.listar
                    CatalogRepository.listarTodos
                    LabRepository.obtenerUltimoPorLoteId
            return csvResult "lotes" csv
        } |> Async.StartAsTask ))
        .RequireAuthorization("RequireInventarioLectura")
        .WithName("ReporteLotesCsv").WithTags("Reportes") |> ignore
