open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Sinbas.Domain
open Sinbas.Infrastructure
open Sinbas.Web

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    // Register Swagger Services
    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let app = builder.Build()

    // Seed default admin user (admin / admin123)
    let adminHash = PasswordHasher.hash "admin123" |> fun (PasswordHash h) -> h
    AuthRepository.seed adminHash

    // Enable Swagger UI in Development
    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

    app.MapGet("/", Func<string>(fun () -> "Hello World!")) |> ignore

    // Register Endpoints
    AuthEndpoints.mapAuthEndpoints app
    UserEndpoints.mapUserEndpoints app

    app.Run()

    0 // Exit code
