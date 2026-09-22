namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module LabEndpoints =

    let mapEndpoints (app: WebApplication) =

        // 1. POST /api/lotes/{id}/analisis — Registrar análisis de laboratorio (RF06 / CU-05 / F-LAB-02)
        app.MapPost("/api/lotes/{id}/analisis", Func<string, RegistrarAnalisisRequest, Threading.Tasks.Task<IResult>>(fun id req ->
            async {
                let! res =
                    LabService.registrarAnalisis
                        LoteRepository.obtenerPorId
                        LoteRepository.actualizarStockYEstado
                        LabRepository.insertar
                        id
                        req

                match res with
                | Ok dto -> return Results.Created(sprintf "/api/lotes/%s/analisis/%s" id dto.Id, dto)
                | Error msg ->
                    if msg.Contains("no se encontró", StringComparison.OrdinalIgnoreCase) then
                        return Results.NotFound({| error = msg |})
                    else
                        return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireLaboratorio")
            .WithName("RegistrarAnalisisLaboratorio")
            .WithTags("Laboratorio")
        |> ignore

        // 2. GET /api/lotes/{id}/analisis — Listar historial de análisis de un lote (F-LAB-02)
        app.MapGet("/api/lotes/{id}/analisis", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let! res =
                    LabService.listarAnalisisPorLote
                        LabRepository.listarPorLoteId
                        id

                match res with
                | Ok dtos -> return Results.Ok(dtos)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarAnalisisLote")
            .WithTags("Laboratorio")
        |> ignore

        // 3. GET /api/lotes/{id}/etiqueta — Generar y descargar etiqueta PDF oficial (RF07 / RN01 / F-LAB-03)
        app.MapGet("/api/lotes/{id}/etiqueta", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let! res =
                    LabService.generarEtiquetaPdf
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LabRepository.obtenerUltimoPorLoteId
                        PdfEtiquetaService.generarPdfEtiqueta
                        id

                match res with
                | Ok (pdfBytes, fileName) ->
                    return Results.File(pdfBytes, "application/pdf", fileName)
                | Error msg ->
                    if msg.StartsWith("ERR_SIN_ANALISIS_LABORATORIO") then
                        return Results.UnprocessableEntity({| error = msg |})
                    elif msg.Contains("no se encontró", StringComparison.OrdinalIgnoreCase) then
                        return Results.NotFound({| error = msg |})
                    else
                        return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("GenerarEtiquetaLote")
            .WithTags("Laboratorio")
        |> ignore

        // 4. GET /api/inventario/despacho/candidatos/{productoId} — Lotes candidatos FIFO para despacho (F3 / RN03)
        app.MapGet("/api/inventario/despacho/candidatos/{productoId}", Func<string, Threading.Tasks.Task<IResult>>(fun productoId ->
            async {
                let! res =
                    InventoryService.listarLotesCandidatosDespacho
                        CatalogRepository.buscarPorId
                        LoteRepository.listar
                        LabRepository.obtenerUltimoPorLoteId
                        productoId

                match res with
                | Ok dtos -> return Results.Ok(dtos)
                | Error msg ->
                    if msg.Contains("no se encontró", StringComparison.OrdinalIgnoreCase) then
                        return Results.NotFound({| error = msg |})
                    else
                        return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarCandidatosDespacho")
            .WithTags("Inventario")
        |> ignore
