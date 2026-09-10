namespace Sinbas.Web

open System
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

module InventoryEndpoints =

    let mapEndpoints (app: WebApplication) =

        // ─────────────────────────────────────────────────────────
        // 1. POST /api/inventario/ingreso
        // Registra recepción física e ingreso de producto/lote a almacén (F4 / MF-04-01).
        // TODO: RBAC - Restringir a RequireAlmacen / RequireGerencia antes de F08
        // ─────────────────────────────────────────────────────────
        app.MapPost("/api/inventario/ingreso", Func<RegistrarIngresoRequest, HttpContext, Threading.Tasks.Task<IResult>>(fun req ctx ->
            async {
                let defaultEmpleado = Guid.Parse("01917f3a-0001-7000-8000-000000000001")
                let responsableId =
                    let subClaim =
                        let c1 = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
                        if isNull c1 then ctx.User.FindFirst(JwtRegisteredClaimNames.Sub) else c1
                    if not (isNull subClaim) then
                        match Guid.TryParse(subClaim.Value) with
                        | true, g -> g
                        | false, _ -> defaultEmpleado
                    else defaultEmpleado

                let! res =
                    InventoryService.registrarIngreso
                        CatalogRepository.buscarPorId
                        CatalogRepository.listarTodos
                        LoteRepository.obtenerPorId
                        LoteRepository.listar
                        LoteRepository.insertar
                        LoteRepository.actualizarStockYEstado
                        InventoryRepository.insertarMovimiento
                        responsableId
                        req

                match res with
                | Ok dto -> return Results.Created(sprintf "/api/inventario/movimientos/%s" dto.Id, dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("RegistrarIngreso")
            .WithTags("Inventario")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 2. GET /api/inventario/movimientos
        // Historial cronológico de movimientos de almacén (F4 / F5 / F8).
        // Soporta filtro opcional ?tipo=Entrada | ?tipo=Salida
        // TODO: RBAC - Restringir a RequireAlmacen / RequireGerencia / RequireComercial antes de F08
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/inventario/movimientos", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let tipoFilter =
                    if ctx.Request.Query.ContainsKey("tipo") then
                        let v = ctx.Request.Query.["tipo"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let! dtos =
                    InventoryService.listarMovimientos
                        InventoryRepository.listarMovimientos
                        LoteRepository.obtenerPorId
                        tipoFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarMovimientos")
            .WithTags("Inventario")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // ─────────────────────────────────────────────────────────
        // 3. GET /api/inventario/stock
        // Consulta de existencias consolidadas por producto con alertas y desglose de lotes (F5 / MF-05-01 / MF-05-03).
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/inventario/stock", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let umbralMinimo =
                    if ctx.Request.Query.ContainsKey("umbralMinimo") then
                        match Decimal.TryParse(ctx.Request.Query.["umbralMinimo"].ToString()) with
                        | true, v -> Some v
                        | false, _ -> None
                    else None

                let! dtos =
                    InventoryService.consultarStockConsolidado
                        CatalogRepository.listarTodos
                        LoteRepository.listar
                        InventoryRepository.listarTodosMovimientosDominio
                        umbralMinimo

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ConsultarStockConsolidado")
            .WithTags("Inventario")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 4. GET /api/inventario/stock/{productoId}
        // Consulta de existencias de un producto específico y desglose de lotes (F5 / MF-05-01).
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/inventario/stock/{productoId}", Func<string, Threading.Tasks.Task<IResult>>(fun productoId ->
            async {
                let! res =
                    InventoryService.consultarStockProducto
                        CatalogRepository.buscarPorId
                        LoteRepository.listar
                        InventoryRepository.listarTodosMovimientosDominio
                        productoId

                match res with
                | Ok stockDto -> return Results.Ok(stockDto)
                | Error msg -> return Results.NotFound({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ConsultarStockProducto")
            .WithTags("Inventario")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 5. GET /api/inventario/kardex
        // Kardex digital cronológico con saldo acumulado paso a paso (F5 / MF-05-02).
        // Soporta filtros opcionales ?productoId=&loteId=
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/inventario/kardex", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let prodIdFilter =
                    if ctx.Request.Query.ContainsKey("productoId") then
                        let v = ctx.Request.Query.["productoId"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let loteIdFilter =
                    if ctx.Request.Query.ContainsKey("loteId") then
                        let v = ctx.Request.Query.["loteId"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let! dtos =
                    InventoryService.consultarKardex
                        CatalogRepository.listarTodos
                        LoteRepository.listar
                        InventoryRepository.listarTodosMovimientosDominio
                        prodIdFilter
                        loteIdFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ConsultarKardexDigital")
            .WithTags("Inventario")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 4. POST /api/inventario/egreso
        // Registra la salida de stock con resolución FIFO (F8 / MF-08-03).
        // Soporta: Venta, Merma, UsoLabor (MuestraLab), UsoVivero (UsoInterno),
        //          Intercambio (TruequeSalida).
        // TODO: RBAC - Restringir a RequireAlmacen / RequireGerencia / RequireComercial
        // ─────────────────────────────────────────────────────────
        app.MapPost("/api/inventario/egreso", Func<RegistrarEgresoRequest, HttpContext, Threading.Tasks.Task<IResult>>(fun req ctx ->
            async {
                let defaultEmpleado = Guid.Parse("01917f3a-0001-7000-8000-000000000001")
                let responsableId =
                    let subClaim =
                        let c1 = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
                        if isNull c1 then ctx.User.FindFirst(JwtRegisteredClaimNames.Sub) else c1
                    if not (isNull subClaim) then
                        match Guid.TryParse(subClaim.Value) with
                        | true, g -> g
                        | false, _ -> defaultEmpleado
                    else defaultEmpleado

                let! res =
                    InventoryService.registrarEgreso
                        CatalogRepository.buscarPorId
                        CatalogRepository.listarTodos
                        LoteRepository.listar
                        LoteRepository.actualizarStockYEstado
                        InventoryRepository.insertarMovimiento
                        InventoryRepository.listarTodosMovimientosDominio
                        responsableId
                        req

                match res with
                | Ok dto -> return Results.Created(sprintf "/api/inventario/movimientos/%s" dto.Id, dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("RegistrarEgreso")
            .WithTags("Inventario")
        |> ignore
