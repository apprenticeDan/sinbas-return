namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure

module LoteEndpoints =

    let mapEndpoints (app: WebApplication) =

        // 1. GET /api/lotes - Listar lotes de almacén
        app.MapGet("/api/lotes", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let pidFilter =
                    if ctx.Request.Query.ContainsKey("productoId") then
                        let v = ctx.Request.Query.["productoId"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let estFilter =
                    if ctx.Request.Query.ContainsKey("estado") then
                        let v = ctx.Request.Query.["estado"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let! dtos =
                    LoteService.listarLotes
                        LoteRepository.listar
                        CatalogRepository.listarTodos
                        pidFilter
                        estFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarLotes")
            .WithTags("Lotes")
        |> ignore

        // 2. POST /api/lotes - Registrar nuevo lote de semilla / ingreso a almacén
        app.MapPost("/api/lotes", Func<CrearLoteRequest, Threading.Tasks.Task<IResult>>(fun req ->
            async {
                let! res =
                    LoteService.registrarIngresoLote
                        CatalogRepository.buscarPorId
                        LoteRepository.insertar
                        (fun () -> async { return 1 })
                        req

                match res with
                | Ok dto -> return Results.Created(sprintf "/api/lotes/%s" dto.Id, dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("CrearLote")
            .WithTags("Lotes")
        |> ignore

        // 3. PUT /api/lotes/{id}/bloquear - Bloquear lote por calidad u observaciones
        app.MapPut("/api/lotes/{id}/bloquear", Func<string, BloquearLoteRequest, Threading.Tasks.Task<IResult>>(fun id req ->
            async {
                let! res =
                    LoteService.bloquearLote
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LoteRepository.actualizarStockYEstado
                        id
                        req.Motivo

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("BloquearLote")
            .WithTags("Lotes")
        |> ignore

        // 4. GET /api/lotes/{id} - Consultar ficha técnica completa del lote (RF14 / CU-14 / F-LAB-06)
        app.MapGet("/api/lotes/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let! res =
                    LoteService.consultarFichaTecnicaLote
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LabRepository.listarPorLoteId
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error msg ->
                    if msg.Contains("no se encontró", StringComparison.OrdinalIgnoreCase) then
                        return Results.NotFound({| error = msg |})
                    else
                        return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ConsultarFichaTecnicaLote")
            .WithTags("Lotes")
        |> ignore

