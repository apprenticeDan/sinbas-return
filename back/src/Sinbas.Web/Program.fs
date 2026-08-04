open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Sinbas.Web

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    
    // Register Swagger Services
    builder.Services.AddEndpointsApiExplorer() |> ignore
    builder.Services.AddSwaggerGen() |> ignore

    let app = builder.Build()

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
