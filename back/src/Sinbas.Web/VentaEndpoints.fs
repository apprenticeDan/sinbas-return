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
// Feature F8: Ventas y Despacho Físico — Minimal API Endpoints
// (RF-08 | CU-11 | RN04 / RN05 / RN12 / RN14 / RNF05)
// ─────────────────────────────────────────────────────────────

module VentaEndpoints =

    /// Helper: resolver nombre de producto desde el catálogo
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

    /// Helper: resolver nombre de cliente desde el repositorio
    let private crearNombreClienteResolver () : ClienteId -> string =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<ClienteId, string>()
        fun cid ->
            cache.GetOrAdd(cid, fun _ ->
                let result =
                    ClientRepository.obtenerPorId cid
                    |> Async.RunSynchronously
                result
                |> Option.map Cliente.nombreVisible
                |> Option.defaultValue "?")

    /// Helper: extraer EmpleadoId del claim del JWT
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

    let mapEndpoints (app: WebApplication) =

        // ─────────────────────────────────────────────────────────
        // 1. POST /api/proformas/{id}/confirmar-venta (RequireComercial)
        // Confirma venta, congela stock y emite orden de despacho (RNF05)
        // ─────────────────────────────────────────────────────────
        app.MapPost("/api/proformas/{id}/confirmar-venta", Func<HttpContext, string, ConfirmarVentaRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! responsableId = resolverEmpleadoId ctx
                let nombreProdResolver = crearNombreProductoResolver ()
                let nombreCliResolver = crearNombreClienteResolver ()

                // Soporte de idempotencia
                let idempotencyKey =
                    if ctx.Request.Headers.ContainsKey("Idempotency-Key") then
                        let k = ctx.Request.Headers.["Idempotency-Key"].ToString()
                        if String.IsNullOrWhiteSpace k then None else Some (k.Trim())
                    else None

                let! regPrevio =
                    match idempotencyKey with
                    | Some key -> IdempotenciaRepository.buscar key
                    | None -> async { return None }

                match regPrevio with
                | Some r ->
                    return Results.Content(r.cuerpo_respuesta, "application/json", null, Nullable r.status_code)
                | None ->

                let reqBody = if isNull (box req) then { ClienteId = None } else req

                let! res =
                    VentaService.confirmarVenta
                        ProformaRepository.obtenerPorId
                        ClientRepository.obtenerPorId
                        CatalogRepository.buscarPorId
                        LoteRepository.listar
                        VentaRepository.confirmarVentaTx
                        nombreProdResolver
                        nombreCliResolver
                        responsableId
                        id
                        reqBody

                match res with
                | Ok dto ->
                    let location = sprintf "/api/ventas/%s" dto.Venta.Id
                    let result = Results.Created(location, dto)
                    match idempotencyKey with
                    | Some key ->
                        let subClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
                        let uId = if isNull subClaim then responsableId else (match Guid.TryParse(subClaim.Value) with true, g -> g | _ -> responsableId)
                        let json = System.Text.Json.JsonSerializer.Serialize(dto)
                        let reg : RegistroIdempotenciaRow =
                            { clave = key
                              endpoint = sprintf "POST /api/proformas/%s/confirmar-venta" id
                              usuario_id = uId
                              status_code = 201
                              cuerpo_respuesta = json
                              creado_en = DateTime.UtcNow }
                        do! IdempotenciaRepository.guardar reg
                    | None -> ()
                    return result
                | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                | Error (msg, 409) -> return Results.Conflict({| error = msg |})
                | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("ConfirmarVenta")
            .WithTags("Ventas")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 2. GET /api/ventas (RequireComercial)
        // Listar órdenes de venta con filtros opcionales
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/ventas", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let clienteIdFilter =
                    if ctx.Request.Query.ContainsKey("clienteId") then
                        let v = ctx.Request.Query.["clienteId"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let estadoFilter =
                    if ctx.Request.Query.ContainsKey("estado") then
                        let v = ctx.Request.Query.["estado"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let fechaFilter =
                    if ctx.Request.Query.ContainsKey("fecha") then
                        let v = ctx.Request.Query.["fecha"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let nombreProdResolver = crearNombreProductoResolver ()
                let nombreCliResolver = crearNombreClienteResolver ()

                let! dtos =
                    VentaService.listarVentas
                        VentaRepository.listarVentas
                        nombreProdResolver
                        nombreCliResolver
                        clienteIdFilter
                        estadoFilter
                        fechaFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("ListarVentas")
            .WithTags("Ventas")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 3. GET /api/ventas/{id} (RequireComercial)
        // Detalle completo de una orden de venta
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/ventas/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let nombreProdResolver = crearNombreProductoResolver ()
                let nombreCliResolver = crearNombreClienteResolver ()

                let! res =
                    VentaService.obtenerVentaPorId
                        VentaRepository.obtenerVentaPorId
                        nombreProdResolver
                        nombreCliResolver
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("ObtenerVentaPorId")
            .WithTags("Ventas")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 4. GET /api/despachos/pendientes (RequireAlmacen)
        // Lista de órdenes de despacho pendientes para Almacén
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/despachos/pendientes", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let nombreProdResolver = crearNombreProductoResolver ()
                let nombreCliResolver = crearNombreClienteResolver ()
                let codVentaResolver vid =
                    let v = VentaRepository.obtenerVentaPorId vid |> Async.RunSynchronously
                    v |> Option.map (fun ven -> CodigoVenta.valor ven.Codigo)

                let! dtos =
                    VentaService.listarDespachos
                        VentaRepository.listarDespachos
                        nombreProdResolver
                        nombreCliResolver
                        codVentaResolver
                        (Some "Pendiente")

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ListarDespachosPendientes")
            .WithTags("Despachos")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 5. GET /api/despachos/{id} (RequireAlmacen)
        // Detalle de orden con sugerencias FIFO de lotes activos
        // ─────────────────────────────────────────────────────────
        app.MapGet("/api/despachos/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let nombreProdResolver = crearNombreProductoResolver ()
                let nombreCliResolver = crearNombreClienteResolver ()

                let! res =
                    VentaService.obtenerDespachoDetalle
                        VentaRepository.obtenerDespachoPorId
                        VentaRepository.obtenerVentaPorId
                        LoteRepository.listar
                        nombreProdResolver
                        nombreCliResolver
                        id

                match res with
                | Ok (despachoDto, sugerencias) ->
                    return Results.Ok({| despacho = despachoDto; sugerenciasFifo = sugerencias |})
                | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ObtenerDespachoDetalle")
            .WithTags("Despachos")
        |> ignore

        // ─────────────────────────────────────────────────────────
        // 6. POST /api/despachos/{id}/confirmar (RequireAlmacen)
        // Ejecución física de salida de inventario y cierre de orden con FIFO (RN12 / RNF05)
        // ─────────────────────────────────────────────────────────
        app.MapPost("/api/despachos/{id}/confirmar", Func<HttpContext, string, ConfirmarDespachoRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                let! responsableId = resolverEmpleadoId ctx
                let reqBody = if isNull (box req) then { Observaciones = None } else req

                let! res =
                    VentaService.confirmarDespachoFisico
                        VentaRepository.obtenerDespachoPorId
                        LoteRepository.listar
                        ClientRepository.obtenerPorId
                        VentaRepository.confirmarDespachoFisicoTx
                        responsableId
                        id
                        reqBody

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                | Error (msg, 409) -> return Results.Conflict({| error = msg |})
                | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireAlmacen")
            .WithName("ConfirmarDespachoFisico")
            .WithTags("Despachos")
        |> ignore
