namespace Sinbas.Infrastructure

open System
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain

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

    let private analisisFromRow (row: AnalisisLaboratorioRow) : AnalisisLaboratorio =
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
        | Ok a -> a
        | Error err -> failwithf "Registro de análisis corrupto en BD: %A" err

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

            return rows |> Seq.tryHead |> Option.map analisisFromRow
        }

    let listarPorLoteId (LoteId loteId: LoteId) : Async<AnalisisLaboratorio list> =
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

            return rows |> Seq.map analisisFromRow |> Seq.toList
        }

    let obtenerUltimoPorLoteId (loteId: LoteId) : Async<AnalisisLaboratorio option> =
        async {
            let! lista = listarPorLoteId loteId
            return lista |> List.tryHead
        }

    let listarTodos () : Async<AnalisisLaboratorio list> =
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

            return rows |> Seq.map analisisFromRow |> Seq.toList
        }
