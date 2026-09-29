namespace Sinbas.Web

open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Sinbas.Infrastructure
open Sinbas.Application

module AdminEndpoints =

    let mapEndpoints (app: WebApplication) =

        // GET /api/admin/integridad
        // Auditoría en vivo de consistencia e integridad de datos persistidos en PostgreSQL.
        // Inspecciona los repositorios particionados y reporta filas corruptas o en cuarentena.
        app.MapGet("/api/admin/integridad", Func<HttpContext, Threading.Tasks.Task<IResult>>(fun ctx ->
            async {
                let! prodsCol = CatalogRepository.listarColeccion ()
                let! lotesCol = LoteRepository.listarColeccion None None
                let! labCol = LabRepository.listarTodosColeccion ()
                let! movsCol = InventoryRepository.listarColeccionMovimientos None

                let todas =
                    [ yield! prodsCol.Inconsistencias
                      yield! lotesCol.Inconsistencias
                      yield! labCol.Inconsistencias
                      yield! movsCol.Inconsistencias ]

                let porEntidad =
                    todas
                    |> List.countBy (fun e -> e.Entidad)
                    |> Map.ofList

                let detalles =
                    todas
                    |> List.map (fun e ->
                        {| entidad = e.Entidad
                           registroId = e.RegistroId
                           campo = e.Campo
                           valorCrudo = e.ValorCrudo
                           error = sprintf "%A" e.ErrorDominio |})

                let respuesta =
                    {| totalInconsistencias = todas.Length
                       esIntegro = List.isEmpty todas
                       porEntidad = porEntidad
                       detalles = detalles |}

                return Results.Ok(respuesta)
            } |> Async.StartAsTask
        ))
            .RequireAuthorization()
            .WithName("AuditoriaIntegridadEnVivo")
            .WithTags("Administracion")
        |> ignore
