namespace Sinbas.Web

open System
open System.Security.Claims
open System.IdentityModel.Tokens.Jwt
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Fase 6a: Solicitud de Egreso Interno — Endpoints Minimal API
// ─────────────────────────────────────────────────────────────

module SolicitudEndpoints =

    let private crearNombreProductoResolver () : ProductoId -> string =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<ProductoId, string>()
        fun pid ->
            cache.GetOrAdd(pid, fun _ ->
                let result =
                    CatalogRepository.buscarPorId pid
                    |> Async.RunSynchronously
                result
                |> Option.map Producto.nombreVisible
                |> Option.defaultValue "?")

    let private crearCodigoLoteResolver () : LoteId -> string =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<LoteId, string>()
        fun lid ->
            cache.GetOrAdd(lid, fun _ ->
                let result =
                    LoteRepository.obtenerPorId lid
                    |> Async.RunSynchronously
                result
                |> Option.map (fun l -> CodigoLote.valor l.Codigo)
                |> Option.defaultValue "?")

    let private resolverEmpleadoId (ctx: HttpContext) : Async<Guid> =
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

    let private mapError (msg: string, code: int) : IResult =
        match code with
        | 404 -> Results.NotFound({| error = msg |})
        | 409 -> Results.Conflict({| error = msg |})
        | 422 -> Results.UnprocessableEntity({| error = msg |})
        | 500 -> Results.Problem(detail = msg, statusCode = Nullable 500)
        | _   -> Results.BadRequest({| error = msg |})

    let mapEndpoints (app: WebApplication) =

        // 1. POST /api/solicitudes (RequireAlmacen)
        app.MapPost("/api/solicitudes", Func<HttpContext, CrearSolicitudRequest, Threading.Tasks.Task<IResult>>(fun ctx req ->
            async {
                let! solicitanteId = resolverEmpleadoId ctx
                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let! res =
                    SolicitudService.crear
                        SolicitudRepository.insertarConReservaTx
                        LoteRepository.obtenerPorId
                        LoteRepository.listar
                        nombreProdResolver
                        codLoteResolver
                        solicitanteId
                        req

                match res with
                | Ok dto ->
                    let location = sprintf "/api/solicitudes/%s" dto.Id
                    return Results.Created(location, dto)
                | Error (msg, code) ->
                    return mapError (msg, code)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("CrearSolicitudEgresoInterno")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore

        // 2. GET /api/solicitudes (RequireAlmacen)
        app.MapGet("/api/solicitudes", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let estadoFilter =
                    if ctx.Request.Query.ContainsKey("estado") then
                        let v = ctx.Request.Query.["estado"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let solicitanteFilter =
                    if ctx.Request.Query.ContainsKey("solicitanteId") then
                        let v = ctx.Request.Query.["solicitanteId"].ToString()
                        match Guid.TryParse(v) with
                        | true, g -> Some (EmpleadoId g)
                        | false, _ -> None
                    else None

                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let! dtos =
                    SolicitudService.listar
                        SolicitudRepository.listar
                        nombreProdResolver
                        codLoteResolver
                        estadoFilter
                        solicitanteFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ListarSolicitudesEgresoInterno")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore

        // 3. GET /api/solicitudes/{id} (RequireAlmacen)
        app.MapGet("/api/solicitudes/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let! res =
                    SolicitudService.obtenerPorId
                        SolicitudRepository.obtenerPorId
                        nombreProdResolver
                        codLoteResolver
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, code) -> return mapError (msg, code)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ObtenerSolicitudEgresoInternoPorId")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore

        // 4. PUT /api/solicitudes/{id}/aprobar (RequireGerencia)
        app.MapPut("/api/solicitudes/{id}/aprobar", Func<HttpContext, string, Threading.Tasks.Task<IResult>>(fun ctx id ->
            async {
                let! aprobadorId = resolverEmpleadoId ctx
                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let! res =
                    SolicitudService.aprobar
                        SolicitudRepository.obtenerPorId
                        SolicitudRepository.actualizarEstadoConReservaTx
                        LoteRepository.listar
                        nombreProdResolver
                        codLoteResolver
                        aprobadorId
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, code) -> return mapError (msg, code)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireGerencia")
            .WithName("AprobarSolicitudEgresoInterno")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore

        // 5. PUT /api/solicitudes/{id}/rechazar (RequireGerencia)
        app.MapPut("/api/solicitudes/{id}/rechazar", Func<HttpContext, string, RechazarSolicitudRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! rechazadorId = resolverEmpleadoId ctx
                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let (reqBody: RechazarSolicitudRequest) =
                    if isNull (box req) || String.IsNullOrWhiteSpace req.Motivo then
                        { RechazarSolicitudRequest.Motivo = "" }
                    else
                        req

                let! res =
                    SolicitudService.rechazar
                        SolicitudRepository.obtenerPorId
                        SolicitudRepository.actualizarEstado
                        nombreProdResolver
                        codLoteResolver
                        rechazadorId
                        id
                        reqBody

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, code) -> return mapError (msg, code)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireGerencia")
            .WithName("RechazarSolicitudEgresoInterno")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore

        // 6. POST /api/solicitudes/{id}/confirmar (RequireAlmacen)
        app.MapPost("/api/solicitudes/{id}/confirmar", Func<HttpContext, string, Threading.Tasks.Task<IResult>>(fun ctx id ->
            async {
                let! confirmadorId = resolverEmpleadoId ctx
                let nombreProdResolver = crearNombreProductoResolver ()
                let codLoteResolver = crearCodigoLoteResolver ()

                let! res =
                    SolicitudService.confirmar
                        SolicitudRepository.obtenerPorId
                        SolicitudRepository.confirmarTx
                        nombreProdResolver
                        codLoteResolver
                        confirmadorId
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, code) -> return mapError (msg, code)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ConfirmarSolicitudEgresoInterno")
            .WithTags("SolicitudesEgresoInterno")
        |> ignore
