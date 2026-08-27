namespace Sinbas.Web

open System
open System.Security.Claims
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure

module CatalogEndpoints =

    let mapCatalogEndpoints (app: WebApplication) =

        let listarProductos = CatalogRepository.listarTodos
        let guardarProducto = CatalogRepository.guardar
        let buscarPorId = CatalogRepository.buscarPorId

        // GET /api/catalogo/productos
        app.MapGet("/api/catalogo/productos", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let catParam =
                    if ctx.Request.Query.ContainsKey("categoria") then
                        let v = ctx.Request.Query.["categoria"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let estParam =
                    if ctx.Request.Query.ContainsKey("estado") then
                        let v = ctx.Request.Query.["estado"].ToString()
                        if String.IsNullOrWhiteSpace v then None else Some v
                    else None

                let! productos = CatalogService.listarCatalogo listarProductos catParam estParam
                return Results.Ok(productos)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarCatalogo")
            .WithTags("Catalogo")
        |> ignore

        // POST /api/catalogo/productos
        app.MapPost("/api/catalogo/productos", Func<CrearProductoRequest, Threading.Tasks.Task<IResult>>(fun req ->
            async {
                let! res = CatalogService.crearProducto guardarProducto req
                match res with
                | Ok prod -> return Results.Created(sprintf "/api/catalogo/productos/%s" (prod.Id.ToString()), prod)
                | Error err -> return Results.BadRequest({| error = err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("CrearProducto")
            .WithTags("Catalogo")
        |> ignore

        // PUT /api/catalogo/productos/{id}/precio
        app.MapPut("/api/catalogo/productos/{id}/precio", Func<Guid, AsignarPrecioRequest, HttpContext, Threading.Tasks.Task<IResult>>(fun id req ctx ->
            async {
                let reqId = { req with ProductoId = id }
                let usrClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)
                let usrGuid =
                    if usrClaim <> null then
                        match Guid.TryParse(usrClaim.Value) with
                        | true, g -> g
                        | _ -> Guid.Empty
                    else
                        Guid.Empty

                let! res = CatalogService.asignarPrecio buscarPorId guardarProducto reqId usrGuid
                match res with
                | Ok prod -> return Results.Ok(prod)
                | Error err -> return Results.BadRequest({| error = err |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization("RequireGerencia")
            .WithName("AsignarPrecioOficial")
            .WithTags("Catalogo")
        |> ignore
