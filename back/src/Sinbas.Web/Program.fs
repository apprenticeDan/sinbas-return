open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Sinbas.Web
open Sinbas.Infrastructure

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    // Configurar CORS para el frontend (SolidJS)
    builder.Services.AddCors(fun options ->
        options.AddPolicy("AllowAll", fun policy ->
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader() |> ignore
        )
    ) |> ignore

    // Configurar JSON serialization para DTOs
    builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(fun (options: Microsoft.AspNetCore.Http.Json.JsonOptions) ->
        options.SerializerOptions.PropertyNameCaseInsensitive <- true
        options.SerializerOptions.PropertyNamingPolicy <- System.Text.Json.JsonNamingPolicy.CamelCase
    ) |> ignore

    // Register Swagger Services
    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let app = builder.Build()

    // Inicializar tablas y datos iniciales de la base de datos
    DbConnection.inicializar()

    app.UseCors("AllowAll") |> ignore

    // Enable Swagger UI in Development
    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

    app.MapGet("/", Func<string>(fun () -> "SINBAS API v2.0.0 — Online")) |> ignore

    // Register Endpoints
    AuthEndpoints.mapAuthEndpoints app
    UserEndpoints.mapUserEndpoints app

    app.Run()

    0 // Exit code

