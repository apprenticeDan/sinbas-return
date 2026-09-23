namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Application
open Sinbas.Infrastructure

// ─────────────────────────────────────────────────────────────
// Feature F6: Gestión de Clientes (RF08 | CU-17 | RN18)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type CrearClientePolimorficoRequest =
    { Tipo: string // "Natural" | "Juridica"
      // Persona Natural
      Nombres: string
      ApellidoPaterno: string option
      ApellidoMaterno: string option
      CiNumero: string
      CiComplemento: string option
      CiExtension: string option
      // Persona Jurídica
      RazonSocial: string
      Nit: string
      Representante: RepresentanteRequest option
      // Contacto y ubicación
      Telefono: string option
      Email: string option
      Direccion: string option }

module ClientEndpoints =

    let mapEndpoints (app: WebApplication) =

        // 1. GET /api/clientes — Listar o buscar clientes (multivariable por nombre, NIT o CI)
        app.MapGet("/api/clientes", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let qParam =
                    if ctx.Request.Query.ContainsKey("q") then
                        let v = ctx.Request.Query.["q"].ToString()
                        if String.IsNullOrWhiteSpace v then "" else v.Trim()
                    else ""

                let! clientes =
                    if String.IsNullOrWhiteSpace qParam then
                        ClientService.listarClientes ClientRepository.listarTodos ()
                    else
                        ClientService.buscarClientes ClientRepository.buscarPorTermino qParam

                return Results.Ok(clientes)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("ListarOBuscarClientes")
            .WithTags("Clientes")
        |> ignore

        // 2. GET /api/clientes/{id} — Obtener cliente por ID
        app.MapGet("/api/clientes/{id}", Func<string, Threading.Tasks.Task<IResult>>(fun id ->
            async {
                let! res = ClientService.obtenerClientePorId ClientRepository.obtenerPorId id
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
            .WithName("ObtenerClientePorId")
            .WithTags("Clientes")
        |> ignore

        // 3. POST /api/clientes/natural — Registrar cliente Persona Natural
        app.MapPost("/api/clientes/natural", Func<CrearClienteNaturalRequest, Threading.Tasks.Task<IResult>>(fun req ->
            async {
                let! res = ClientService.crearClienteNatural ClientRepository.insertar req
                match res with
                | Ok dto -> return Results.Created(sprintf "/api/clientes/%s" dto.Id, dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("CrearClienteNatural")
            .WithTags("Clientes")
        |> ignore

        // 4. POST /api/clientes/juridica — Registrar cliente Persona Jurídica
        app.MapPost("/api/clientes/juridica", Func<CrearClienteJuridicaRequest, Threading.Tasks.Task<IResult>>(fun req ->
            async {
                let! res = ClientService.crearClienteJuridica ClientRepository.insertar req
                match res with
                | Ok dto -> return Results.Created(sprintf "/api/clientes/%s" dto.Id, dto)
                | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("CrearClienteJuridica")
            .WithTags("Clientes")
        |> ignore

        // 5. POST /api/clientes — Registrar cliente polimórfico (acepta Natural o Juridica según campo tipo)
        app.MapPost("/api/clientes", Func<CrearClientePolimorficoRequest, Threading.Tasks.Task<IResult>>(fun req ->
            async {
                let tipo = if isNull req.Tipo then "Natural" else req.Tipo.Trim()
                if tipo.Equals("Juridica", StringComparison.OrdinalIgnoreCase) then
                    let juridicaReq: CrearClienteJuridicaRequest =
                        { RazonSocial = req.RazonSocial
                          Nit = req.Nit
                          Representante = req.Representante
                          Telefono = req.Telefono
                          Email = req.Email
                          Direccion = req.Direccion }
                    let! res = ClientService.crearClienteJuridica ClientRepository.insertar juridicaReq
                    match res with
                    | Ok dto -> return Results.Created(sprintf "/api/clientes/%s" dto.Id, dto)
                    | Error msg -> return Results.BadRequest({| error = msg |})
                else
                    let naturalReq: CrearClienteNaturalRequest =
                        { Nombres = req.Nombres
                          ApellidoPaterno = req.ApellidoPaterno
                          ApellidoMaterno = req.ApellidoMaterno
                          CiNumero = req.CiNumero
                          CiComplemento = req.CiComplemento
                          CiExtension = req.CiExtension
                          Telefono = req.Telefono
                          Email = req.Email
                          Direccion = req.Direccion }
                    let! res = ClientService.crearClienteNatural ClientRepository.insertar naturalReq
                    match res with
                    | Ok dto -> return Results.Created(sprintf "/api/clientes/%s" dto.Id, dto)
                    | Error msg -> return Results.BadRequest({| error = msg |})
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("CrearCliente")
            .WithTags("Clientes")
        |> ignore

        // 6. PUT /api/clientes/{id} — Modificar datos de cliente existente (RF08 / CU-17 / RN18)
        app.MapPut("/api/clientes/{id}", Func<string, ActualizarClienteRequest, Threading.Tasks.Task<IResult>>(fun id req ->
            async {
                let! res =
                    ClientService.actualizarCliente
                        ClientRepository.obtenerPorId
                        ClientRepository.actualizar
                        id
                        req

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
            .WithName("ActualizarCliente")
            .WithTags("Clientes")
        |> ignore

