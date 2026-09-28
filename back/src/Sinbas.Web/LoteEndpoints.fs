namespace Sinbas.Web

open System
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Domain
open Sinbas.Application
open Sinbas.Infrastructure

module LoteEndpoints =

    let mapEndpoints (app: WebApplication) =

        let resolverEmpleadoId (ctx: HttpContext) : Async<Guid> =
            async {
                let defaultEmpleado = Guid.Parse("01917f3a-0001-7000-8000-000000000001")
                let empClaim = ctx.User.FindFirst("empleado_id")
                if not (isNull empClaim) then
                    match Guid.TryParse(empClaim.Value) with
                    | true, g -> return g
                    | false, _ -> return defaultEmpleado
                else
                    let subClaim =
                        let c1 = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
                        if isNull c1 then ctx.User.FindFirst(JwtRegisteredClaimNames.Sub) else c1
                    if not (isNull subClaim) then
                        match Guid.TryParse(subClaim.Value) with
                        | true, uGuid ->
                            let! uRes = AuthRepository.buscarUsuarioPorId (UsuarioId uGuid)
                            match uRes with
                            | Ok u ->
                                let (EmpleadoId eId) = Usuario.empleadoId u
                                return eId
                            | Error _ -> return defaultEmpleado
                        | false, _ -> return defaultEmpleado
                    else
                        return defaultEmpleado
            }

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

        // 5. PUT /api/lotes/{id}/liberar-cuarentena - Levantar cuarentena de lote tras revisión gerencial
        app.MapPut("/api/lotes/{id}/liberar-cuarentena", Func<HttpContext, string, LiberarCuarentenaRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! empGuid = resolverEmpleadoId ctx
                let! res =
                    LoteService.liberarCuarentena
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LoteRepository.actualizarStockYEstado
                        id
                        (EmpleadoId empGuid)
                        req.Justificacion

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("LiberarCuarentenaLote")
            .WithTags("Lotes")
        |> ignore

        // 6. PUT /api/lotes/{id}/rechazar - Rechazar definitivamente lote en cuarentena (Gerencia)
        app.MapPut("/api/lotes/{id}/rechazar", Func<HttpContext, string, RechazarLoteRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! empGuid = resolverEmpleadoId ctx
                let! res =
                    LoteService.rechazarLote
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LoteRepository.actualizarStockYEstado
                        id
                        (EmpleadoId empGuid)
                        req.Motivo

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("RechazarLote")
            .WithTags("Lotes")
        |> ignore

        // 7. PUT /api/lotes/{id}/solicitar-analisis - Solicitar nuevo análisis a laboratorio para lote en cuarentena
        app.MapPut("/api/lotes/{id}/solicitar-analisis", Func<HttpContext, string, SolicitarNuevoAnalisisRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! empGuid = resolverEmpleadoId ctx
                let! res =
                    LoteService.solicitarNuevoAnalisis
                        LoteRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LoteRepository.actualizarStockYEstado
                        id
                        (EmpleadoId empGuid)
                        req.Instruccion

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("SolicitarAnalisisLote")
            .WithTags("Lotes")
        |> ignore

