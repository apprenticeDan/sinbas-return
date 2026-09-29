namespace Sinbas.Infrastructure

open System
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain
open Sinbas.Application

[<CLIMutable>]
type AnalisisLaboratorioRow =
    { id: Guid
      lote_id: Guid
      fecha_analisis: DateOnly
      germinacion: decimal
      pureza: decimal
      humedad: decimal
      viabilidad: decimal
      semillas_puras_kg: int
      semillas_impurezas_kg: int
      dictamen: string
      observaciones: string }

module private LabTables =
    let analisisTable = table'<AnalisisLaboratorioRow> "analisis_laboratorio"

module LabRepository =

    let private rowFromAnalisis (a: AnalisisLaboratorio) : AnalisisLaboratorioRow =
        let (LaboratorioId id) = a.Id
        let (LoteId lId) = a.LoteId
        { id = id
          lote_id = lId
          fecha_analisis = a.FechaAnalisis
          germinacion = PorcentajeCalidad.valor a.Germinacion
          pureza = PorcentajeCalidad.valor a.Pureza
          humedad = PorcentajeCalidad.valor a.Humedad
          viabilidad = PorcentajeCalidad.valor a.Viabilidad
          semillas_puras_kg = a.SemillasPurasKg
          semillas_impurezas_kg = a.SemillasImpurezasKg
          dictamen = DictamenCalidad.aTexto a.Dictamen
          observaciones = Option.toObj a.Observaciones }

    let private analisisFromRow (row: AnalisisLaboratorioRow) : Result<AnalisisLaboratorio, ErrorIntegridad> =
        match AnalisisLaboratorio.reconstruir
                row.id
                row.lote_id
                row.fecha_analisis
                row.germinacion
                row.pureza
                row.humedad
                row.viabilidad
                row.semillas_puras_kg
                row.semillas_impurezas_kg
                row.dictamen
                (Option.ofObj row.observaciones) with
        | Ok a -> Ok a
        | Error err ->
            Error { Entidad = "analisis_laboratorio"
                    RegistroId = string row.id
                    Campo = "parametros_calidad"
                    ValorCrudo = Some (sprintf "G:%M, P:%M, H:%M, V:%M, D:%s" row.germinacion row.pureza row.humedad row.viabilidad row.dictamen)
                    ErrorDominio = err }

    let insertar (analisis: AnalisisLaboratorio) : Async<unit> =
        async {
            use conn = DbConnection.crear ()
            let row = rowFromAnalisis analisis
            let! _ =
                insert {
                    into LabTables.analisisTable
                    value row
                }
                |> conn.InsertAsync
                |> Async.AwaitTask
            return ()
        }

    let obtenerPorId (LaboratorioId id: LaboratorioId) : Async<AnalisisLaboratorio option> =
        async {
            use conn = DbConnection.crear ()
            let! rows =
                select {
                    for a in LabTables.analisisTable do
                    where (a.id = id)
                }
                |> conn.SelectAsync<AnalisisLaboratorioRow>
                |> Async.AwaitTask

            return
                rows
                |> Seq.tryHead
                |> Option.bind (fun row ->
                    match analisisFromRow row with
                    | Ok a -> Some a
                    | Error err ->
                        eprintfn "[INTEGRIDAD_CRITICA] LabRepository.obtenerPorId id=%s campo=%s: %A"
                            err.RegistroId err.Campo err.ErrorDominio
                        None)
        }

    let listarColeccionPorLoteId (LoteId loteId: LoteId) : Async<LecturaColeccion<AnalisisLaboratorio>> =
        async {
            use conn = DbConnection.crear ()
            let! rows =
                select {
                    for a in LabTables.analisisTable do
                    where (a.lote_id = loteId)
                    orderByDescending a.fecha_analisis
                }
                |> conn.SelectAsync<AnalisisLaboratorioRow>
                |> Async.AwaitTask

            return LecturaColeccion.particionar analisisFromRow rows
        }

    let listarPorLoteId (loteId: LoteId) : Async<AnalisisLaboratorio list> =
        async {
            let! col = listarColeccionPorLoteId loteId
            return col.Validos
        }

    let obtenerUltimoPorLoteId (loteId: LoteId) : Async<AnalisisLaboratorio option> =
        async {
            let! lista = listarPorLoteId loteId
            return lista |> List.tryHead
        }

    let listarTodosColeccion () : Async<LecturaColeccion<AnalisisLaboratorio>> =
        async {
            use conn = DbConnection.crear ()
            let! rows =
                select {
                    for a in LabTables.analisisTable do
                    selectAll
                    orderByDescending a.fecha_analisis
                }
                |> conn.SelectAsync<AnalisisLaboratorioRow>
                |> Async.AwaitTask

            return LecturaColeccion.particionar analisisFromRow rows
        }

    let listarTodos () : Async<AnalisisLaboratorio list> =
        async {
            let! col = listarTodosColeccion ()
            return col.Validos
        }
