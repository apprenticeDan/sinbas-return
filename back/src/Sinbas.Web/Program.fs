open System
open System.Text
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.IdentityModel.Tokens
open System.Security.Claims
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

    // Configurar Autenticación JWT y Autorización
    let config = builder.Configuration
    let secreto = defaultArg (Option.ofObj (config.["Jwt:Secret"])) "SuperSecretKeyForSinbasDevSecurityOnly"
    let emisor = defaultArg (Option.ofObj (config.["Jwt:Issuer"])) "Sinbas"
    let audiencia = defaultArg (Option.ofObj (config.["Jwt:Audience"])) "SinbasClient"

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(fun options ->
            options.TokenValidationParameters <- TokenValidationParameters(
                ValidateIssuer = true,
                ValidIssuer = emisor,
                ValidateAudience = true,
                ValidAudience = audiencia,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto)),
                ValidateLifetime = true,
                RoleClaimType = ClaimTypes.Role
            )
        ) |> ignore

    builder.Services.AddAuthorization(fun options ->
        options.AddPolicy("RequireAdmin", fun policy -> policy.RequireRole("Administrador") |> ignore)
        options.AddPolicy("RequireGerencia", fun policy -> policy.RequireRole("Administrador", "Gerencia") |> ignore)
        options.AddPolicy("RequireAlmacen", fun policy -> policy.RequireRole("Administrador", "Almacen") |> ignore)
        options.AddPolicy("RequireLaboratorio", fun policy -> policy.RequireRole("Administrador", "Laboratorio") |> ignore)
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
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore

    // Enable Swagger UI in Development
    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore

    app.MapGet("/", Func<string>(fun () -> "SINBAS API v2.0.0 — Online")) |> ignore

    // Register Endpoints
    AuthEndpoints.mapAuthEndpoints app
    UserEndpoints.mapUserEndpoints app
    CatalogEndpoints.mapCatalogEndpoints app
    LoteEndpoints.mapEndpoints app

    app.Run()

    0 // Exit code


