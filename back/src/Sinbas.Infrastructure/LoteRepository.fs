namespace Sinbas.Infrastructure

open System
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain

[<CLIMutable>]
type LoteRow =
    { id: Guid
      codigo: string
      producto_id: Guid
      procedencia: string
      cantidad_inicial: decimal
      cantidad_actual: decimal
      unidad: string
      fecha_ingreso: DateOnly
      ubicacion: string
      estado: string
      observaciones: string }

module private LoteTables =
    let loteTable = table'<LoteRow> "lote"

module LoteRepository =

    let private mapearUnidad (uStr: string) : Unidad =
        match uStr with
        | "Gramo" -> Gramo
        | "Kilogramo" -> Kilogramo
        | "Unidad_" -> Unidad_
        | _ -> Kilogramo

    let private desmapearUnidad (u: Unidad) : string =
        match u with
        | Gramo -> "Gramo"
        | Kilogramo -> "Kilogramo"
        | Unidad_ -> "Unidad_"
        | Bolsa _ -> "Bolsa"

    let private loteFromRow (row: LoteRow) : Lote =
        let codigo =
            match CodigoLote.desdeString row.codigo with
            | Ok c -> c
            | Error _ -> failwithf "Código de lote corrupto en BD: %s" row.codigo

        let unidad = mapearUnidad row.unidad

        let estado =
            match row.estado with
            | "Activo" -> Activo
            | "Agotado" -> Agotado
            | "Bloqueado" -> Bloqueado
            | "Rechazado" -> Rechazado
            | "Archivado" -> Archivado
            | _ -> Activo

        { Id = LoteId row.id
          Codigo = codigo
          ProductoId = ProductoId row.producto_id
          Procedencia = Option.ofObj row.procedencia
          CantidadInicial = { Valor = row.cantidad_inicial; Unidad = unidad }
          CantidadActual = { Valor = row.cantidad_actual; Unidad = unidad }
          FechaIngreso = row.fecha_ingreso
          Ubicacion = Option.ofObj row.ubicacion
          Estado = estado
          Observaciones = Option.ofObj row.observaciones }

    let private rowFromLote (lote: Lote) : LoteRow =
        let (LoteId lId) = lote.Id
        let (ProductoId pId) = lote.ProductoId

        let estadoStr =
            match lote.Estado with
            | Activo -> "Activo"
            | Agotado -> "Agotado"
            | Bloqueado -> "Bloqueado"
            | Rechazado -> "Rechazado"
            | Archivado -> "Archivado"

        { id = lId
          codigo = CodigoLote.valor lote.Codigo
          producto_id = pId
          procedencia = Option.toObj lote.Procedencia
          cantidad_inicial = lote.CantidadInicial.Valor
          cantidad_actual = lote.CantidadActual.Valor
          unidad = desmapearUnidad lote.CantidadInicial.Unidad
          fecha_ingreso = lote.FechaIngreso
          ubicacion = Option.toObj lote.Ubicacion
          estado = estadoStr
          observaciones = Option.toObj lote.Observaciones }

    let insertar (lote: Lote) : Async<unit> =
        async {
            use conn = DbConnection.crear ()
            let row = rowFromLote lote

            do! insert {
                    into LoteTables.loteTable
                    value row
                }
                |> conn.InsertAsync
                |> Async.AwaitTask
                |> Async.Ignore
        }

    let obtenerPorId (id: LoteId) : Async<Lote option> =
        async {
            use conn = DbConnection.crear ()
            let (LoteId gId) = id

            let! rows =
                select {
                    for l in LoteTables.loteTable do
                    where (l.id = gId)
                }
                |> conn.SelectAsync<LoteRow>
                |> Async.AwaitTask

            return rows |> Seq.tryHead |> Option.map loteFromRow
        }

    let listar (productoIdFilter: ProductoId option) (estadoFilter: EstadoLote option) : Async<Lote list> =
        async {
            use conn = DbConnection.crear ()

            let! rows =
                select {
                    for l in LoteTables.loteTable do
                    selectAll
                }
                |> conn.SelectAsync<LoteRow>
                |> Async.AwaitTask

            let lotes = rows |> Seq.map loteFromRow |> Seq.toList

            let lotesFiltrados =
                lotes
                |> List.filter (fun l ->
                    let matchProd =
                        match productoIdFilter with
                        | None -> true
                        | Some pid -> l.ProductoId = pid

                    let matchEst =
                        match estadoFilter with
                        | None -> true
                        | Some est -> l.Estado = est

                    matchProd && matchEst)

            return lotesFiltrados
        }

    let actualizarStockYEstado (lote: Lote) : Async<unit> =
        async {
            use conn = DbConnection.crear ()
            let (LoteId gId) = lote.Id
            let row = rowFromLote lote

            do! update {
                    for l in LoteTables.loteTable do
                    set row
                    where (l.id = gId)
                }
                |> conn.UpdateAsync
                |> Async.AwaitTask
                |> Async.Ignore
        }
