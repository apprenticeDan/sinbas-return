namespace Sinbas.Infrastructure

open System
open Dapper
open Dapper.FSharp.PostgreSQL
open Npgsql
open Sinbas.Domain
open Sinbas.Application

[<CLIMutable>]
type LoteRow =
    { id: Guid
      codigo: string
      producto_id: Guid
      procedencia: string
      cantidad_inicial: decimal
      cantidad_actual: decimal
      cantidad_reservada: decimal
      unidad: string
      fecha_ingreso: DateOnly
      ubicacion: string
      estado: string
      observaciones: string }

module private LoteTables =
    let loteTable = table'<LoteRow> "lote"

module LoteRepository =

    let private mapearUnidad (uStr: string) : UnidadMedida =
        match (if isNull uStr then "" else uStr.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Gramo
        | "kilogramo" | "kg" -> Kilogramo
        | "mililitro" | "ml" -> Mililitro
        | "litro" | "l" -> Litro
        | _ -> UnidadDiscreta

    let private desmapearUnidad (u: UnidadMedida) : string =
        UnidadMedida.aTexto u

    let private loteFromRow (row: LoteRow) : Result<Lote, ErrorIntegridad> =
        match CodigoLote.desdeString row.codigo with
        | Error err ->
            Error { Entidad = "lote"
                    RegistroId = string row.id
                    Campo = "codigo"
                    ValorCrudo = Some row.codigo
                    ErrorDominio = err }
        | Ok codigo ->
        match EstadoLote.desdeTexto row.estado with
        | Error err ->
            Error { Entidad = "lote"
                    RegistroId = string row.id
                    Campo = "estado"
                    ValorCrudo = Some row.estado
                    ErrorDominio = err }
        | Ok estado ->
        let unidad = mapearUnidad row.unidad
        match Cantidad.reconstruir row.cantidad_inicial unidad with
        | Error err ->
            Error { Entidad = "lote"
                    RegistroId = string row.id
                    Campo = "cantidad_inicial"
                    ValorCrudo = Some (string row.cantidad_inicial)
                    ErrorDominio = err }
        | Ok cantInicial ->
        match Cantidad.reconstruir row.cantidad_actual unidad with
        | Error err ->
            Error { Entidad = "lote"
                    RegistroId = string row.id
                    Campo = "cantidad_actual"
                    ValorCrudo = Some (string row.cantidad_actual)
                    ErrorDominio = err }
        | Ok cantActual ->
        let cantReservada =
            match Cantidad.reconstruir row.cantidad_reservada unidad with
            | Ok c -> c
            | Error _ -> Cantidad.cero unidad
        Ok { Id = LoteId row.id
             Codigo = codigo
             ProductoId = ProductoId row.producto_id
             Procedencia = Option.ofObj row.procedencia
             CantidadInicial = cantInicial
             CantidadActual  = cantActual
             CantidadReservada = cantReservada
             FechaIngreso = row.fecha_ingreso
             Ubicacion = Option.ofObj row.ubicacion
             Estado = estado
             Observaciones = Option.ofObj row.observaciones }

    let private rowFromLote (lote: Lote) : LoteRow =
        let (LoteId lId) = lote.Id
        let (ProductoId pId) = lote.ProductoId

        let estadoStr = EstadoLote.aTexto lote.Estado

        { id = lId
          codigo = CodigoLote.valor lote.Codigo
          producto_id = pId
          procedencia = Option.toObj lote.Procedencia
          cantidad_inicial = lote.CantidadInicial.Valor
          cantidad_actual = lote.CantidadActual.Valor
          cantidad_reservada = lote.CantidadReservada.Valor
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

            return
                rows
                |> Seq.tryHead
                |> Option.bind (fun row ->
                    match loteFromRow row with
                    | Ok lote -> Some lote
                    | Error err ->
                        eprintfn "[INTEGRIDAD_CRITICA] LoteRepository.obtenerPorId id=%s campo=%s: %A"
                            err.RegistroId err.Campo err.ErrorDominio
                        None)
        }

    let listarColeccion (productoIdFilter: ProductoId option) (estadoFilter: EstadoLote option) : Async<LecturaColeccion<Lote>> =
        async {
            use conn = DbConnection.crear ()

            let! rows =
                select {
                    for l in LoteTables.loteTable do
                    selectAll
                }
                |> conn.SelectAsync<LoteRow>
                |> Async.AwaitTask

            let coleccion = LecturaColeccion.particionar loteFromRow rows

            let validosFiltrados =
                coleccion.Validos
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

            return { Validos = validosFiltrados; Inconsistencias = coleccion.Inconsistencias }
        }

    let listar (productoIdFilter: ProductoId option) (estadoFilter: EstadoLote option) : Async<Lote list> =
        async {
            let! col = listarColeccion productoIdFilter estadoFilter
            return col.Validos
        }

    /// Devuelve TODOS los códigos de lote en DB sin pasar por reconstrucción
    /// de dominio. Útil para chequear unicidad de secuenciales cuando algunos
    /// lotes podrían estar en Inconsistencias (ej. legacy con formato viejo).
    let listarCodigosRaw () : Async<string list> =
        async {
            use conn = DbConnection.crear ()
            let! codes =
                conn.QueryAsync<string>("SELECT codigo FROM lote")
                |> Async.AwaitTask
            return codes |> Seq.toList
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

    /// Lock + reconstrucción de múltiples lotes dentro de una transacción externa (FOR UPDATE)
    let obtenerMultiplesPorIdParaActualizacion
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (ids: LoteId list)
        : Async<Result<Lote list, string>> =
        async {
            if List.isEmpty ids then return Ok []
            else
                let guids = ids |> List.map (fun (LoteId g) -> g) |> List.toArray
                let sql = "SELECT * FROM lote WHERE id = ANY(@ids) FOR UPDATE"
                let! rows = conn.QueryAsync<LoteRow>(sql, {| ids = guids |}, tx) |> Async.AwaitTask
                let rec reconstruir acc = function
                    | [] -> Ok (List.rev acc)
                    | (row: LoteRow) :: rest ->
                        match loteFromRow row with
                        | Ok lote -> reconstruir (lote :: acc) rest
                        | Error err ->
                            eprintfn "[INTEGRIDAD] LoteRepository.obtenerMultiplesPorIdParaActualizacion id=%s campo=%s: %A"
                                err.RegistroId err.Campo err.ErrorDominio
                            Error (sprintf "Lote %s inconsistente en campo '%s'" err.RegistroId err.Campo)
                return reconstruir [] (rows |> Seq.toList)
        }

    /// Persiste el estado completo de un lote (stock + reserva + estado) dentro de una transacción externa
    let actualizarStockYEstadoTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (lote: Lote)
        : Async<Result<unit, string>> =
        async {
            let (LoteId gId) = lote.Id
            let sql = """
                UPDATE lote
                SET cantidad_actual    = @cantidad_actual,
                    cantidad_reservada = @cantidad_reservada,
                    estado             = @estado
                WHERE id = @id
            """
            let p = {|
                id                 = gId
                cantidad_actual    = lote.CantidadActual.Valor
                cantidad_reservada = lote.CantidadReservada.Valor
                estado             = EstadoLote.aTexto lote.Estado
            |}
            let! affected = conn.ExecuteAsync(sql, p, tx) |> Async.AwaitTask
            if affected = 1 then return Ok ()
            else return Error (sprintf "Lote %s no se pudo actualizar (affectedRows=%d)" (gId.ToString()) affected)
        }
