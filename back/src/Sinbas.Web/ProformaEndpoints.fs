namespace Sinbas.Web

open System
open System.Security.Claims
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F7: Proformas — Minimal API Endpoints
// ─────────────────────────────────────────────────────────────

module ProformaEndpoints =

    /// Helper: resolver nombre de producto desde el catálogo
    let private crearNombreResolver () : ProductoId -> string =
        let cache = System.Collections.Concurrent.ConcurrentDictionary<ProductoId, string>()
        fun pid ->
            cache.GetOrAdd(pid, fun _ ->
                let result =
                    CatalogRepository.buscarPorId pid
                    |> Async.RunSynchronously
                result
                |> Option.map Producto.nombreVisible
                |> Option.defaultValue "?")

    /// Helper: obtener stock disponible de un producto (en unidad base)
    let private obtenerStockProducto (pid: ProductoId) : Async<decimal> =
        async {
            let! lotes = LoteRepository.listar (Some pid) (Some Activo)
            return Stock.disponibleParaVenta pid lotes
        }

    /// Helper: extraer UsuarioId del claim del JWT
    let private obtenerUsuarioId (ctx: HttpContext) : Guid option =
        let claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
        if isNull claim then None
        else
            match Guid.TryParse(claim.Value) with
            | true, g -> Some g
            | false, _ -> None

    let mapEndpoints (app: WebApplication) =

        // 1. POST /api/proformas — Crear proforma (RequireComercial)
        app.MapPost("/api/proformas", Func<HttpContext, CrearProformaRequest, Threading.Tasks.Task<IResult>>(fun ctx req ->
            async {
                match obtenerUsuarioId ctx with
                | None -> return Results.Unauthorized()
                | Some responsableId ->
                    let! res =
                        ProformaService.elaborarProforma
                            ClientRepository.obtenerPorId
                            CatalogRepository.buscarPorId
                            obtenerStockProducto
                            ProformaRepository.insertar
                            responsableId
                            req

                    match res with
                    | Ok dto -> return Results.Created(sprintf "/api/proformas/%s" dto.Id, dto)
                    | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                    | Error (msg, 409) -> return Results.Conflict({| error = msg |})
                    | Error (msg, 422) -> return Results.UnprocessableEntity({| error = msg |})
                    | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("CrearProforma")
            .WithTags("Proformas")
        |> ignore

        // 2. GET /api/proformas — Listar proformas con filtros opcionales
        app.MapGet("/api/proformas", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let estadoFilter =
                    if ctx.Request.Query.ContainsKey("estado") then
                        let v = ctx.Request.Query.["estado"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let clienteIdFilter =
                    if ctx.Request.Query.ContainsKey("clienteId") then
                        let v = ctx.Request.Query.["clienteId"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let resolver = crearNombreResolver ()
                let! dtos =
                    ProformaService.listarProformas
                        ProformaRepository.listar
                        resolver
                        estadoFilter
                        clienteIdFilter

                return Results.Ok(dtos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("ListarProformas")
            .WithTags("Proformas")
        |> ignore

        // 3. GET /api/proformas/{id} — Detalle de proforma
        app.MapGet("/api/proformas/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let resolver = crearNombreResolver ()
                let! res =
                    ProformaService.obtenerProformaPorId
                        ProformaRepository.obtenerPorId
                        resolver
                        id

                match res with
                | Ok dto -> return Results.Ok(dto)
                | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("ObtenerProforma")
            .WithTags("Proformas")
        |> ignore

        // 4. PUT /api/proformas/{id}/anular — Anulación controlada
        app.MapPut("/api/proformas/{id}/anular", Func<HttpContext, string, AnularProformaRequest, Threading.Tasks.Task<IResult>>(fun ctx id req ->
            async {
                match obtenerUsuarioId ctx with
                | None -> return Results.Unauthorized()
                | Some responsableId ->
                    let resolver = crearNombreResolver ()
                    let! res =
                        ProformaService.anularProforma
                            ProformaRepository.obtenerPorId
                            ProformaRepository.actualizarEstado
                            resolver
                            responsableId
                            id
                            req

                    match res with
                    | Ok dto -> return Results.Ok(dto)
                    | Error (msg, 404) -> return Results.NotFound({| error = msg |})
                    | Error (msg, _)   -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireComercial")
            .WithName("AnularProforma")
            .WithTags("Proformas")
        |> ignore

        // 5. GET /api/inventario/stock-disponible/{productoId} — Stock disponible para cotización
        //    Endpoint auxiliar consumido por el frontend para validación en vivo
        app.MapGet("/api/inventario/stock-disponible/{productoId}", Func<string, Threading.Tasks.Task<IResult>>(fun productoIdStr ->
            async {
                match Guid.TryParse(productoIdStr) with
                | false, _ -> return Results.BadRequest({| error = "ID de producto inválido" |})
                | true, pGuid ->
                    let prodId = ProductoId pGuid
                    let! optProd = CatalogRepository.buscarPorId prodId
                    match optProd with
                    | None -> return Results.NotFound({| error = "Producto no encontrado" |})
                    | Some prod ->
                        let! stockBase = obtenerStockProducto prodId
                        let unidad = prod.Base.Presentacion.Unidad
                        let esMayor = unidad = Kilogramo || unidad = Litro
                        let stockDisplay = if esMayor then stockBase / 1000m else stockBase
                        return Results.Ok({|
                            productoId = productoIdStr
                            nombreProducto = Producto.nombreVisible prod
                            stockDisponibleBase = stockBase
                            stockDisponible = stockDisplay
                            unidad = UnidadMedida.aTexto unidad
                            unidadEtiqueta = UnidadMedida.etiqueta unidad
                            precioOficial = prod.PrecioOficial |> Option.map (fun p -> p.Valor) |> Option.defaultValue 0m
                            moneda = prod.PrecioOficial |> Option.map (fun p -> p.Moneda) |> Option.defaultValue "BOB"
                            aptoParaVenta = Producto.esAptoParaVenta prod
                        |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("StockDisponibleProducto")
            .WithTags("Inventario")
        |> ignore
