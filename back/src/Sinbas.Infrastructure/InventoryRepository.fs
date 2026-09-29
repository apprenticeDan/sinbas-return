namespace Sinbas.Infrastructure

open System
open Dapper.FSharp.PostgreSQL
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// REVISIT: Esquema y persistencia de Movimientos de Inventario (F4/F5/F8/F9)
// Diseñado con columnas legibles y directas para facilitar modificaciones.
// Preparado para extensibilidad con:
// - F3: Dictamen de Laboratorio (estado de lotes)
// - F6: Clientes (búsqueda multivariable por nombre, nit, teléfono, etc.)
// - F9: Uso Interno (departamento y funcionario solicitante)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type MovimientoInventarioRow =
    { id: Guid
      fecha: DateTime
      responsable_id: Guid
      tipo: string
      motivo: string
      orden_origen_id: Nullable<Guid>
      contraparte_ref: Nullable<Guid>
      contraparte_nombre: string
      departamento: string
      solicitante: string
      observaciones: string }

[<CLIMutable>]
type LineaMovimientoRow =
    { id: Guid
      movimiento_id: Guid
      lote_id: Guid
      cantidad: decimal
      unidad: string
      observaciones: string }

module private InventoryTables =
    let movimientoTable = table'<MovimientoInventarioRow> "movimiento_inventario"
    let lineaTable = table'<LineaMovimientoRow> "linea_movimiento"

module InventoryRepository =

    let private mapearUnidad (uStr: string) : UnidadMedida =
        match (if isNull uStr then "" else uStr.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Gramo
        | "kilogramo" | "kg" -> Kilogramo
        | "mililitro" | "ml" -> Mililitro
        | "litro" | "l" -> Litro
        | _ -> UnidadDiscreta

    let private desmapearUnidad (u: UnidadMedida) : string =
        UnidadMedida.aTexto u

    let private desmapearTipoMovimiento (tipo: TipoMovimiento) : string * string * Nullable<Guid> * string =
        match tipo with
        | Entrada motivoIngreso ->
            let motivoStr = MotivoIngreso.aTexto motivoIngreso
            match motivoIngreso with
            | Compra (ProveedorId provId) -> ("Entrada", motivoStr, Nullable provId, null)
            | Recoleccion campana -> ("Entrada", motivoStr, Nullable(), campana)
            | DonacionRecibida donante -> ("Entrada", motivoStr, Nullable(), donante)
            | Devolucion (ClienteId cliId) -> ("Entrada", motivoStr, Nullable cliId, null)
            | TruequeEntrada (TruequeId truId) -> ("Entrada", motivoStr, Nullable truId, null)
        | Salida motivoEgreso ->
            let motivoStr = MotivoEgreso.aTexto motivoEgreso
            match motivoEgreso with
            | Venta (ClienteId cliId) -> ("Salida", motivoStr, Nullable cliId, null)
            | MuestraLab (LaboratorioId labId) -> ("Salida", motivoStr, Nullable labId, null)
            | Merma _ -> ("Salida", motivoStr, Nullable(), null)
            | UsoInterno _ -> ("Salida", motivoStr, Nullable(), null)
            | DonacionEnviada dest -> ("Salida", motivoStr, Nullable(), dest)
            | TruequeSalida (TruequeId truId) -> ("Salida", motivoStr, Nullable truId, null)

    let private aMovimientoDominio (row: MovimientoInventarioRow) (lineasRows: LineaMovimientoRow list) : Result<MovimientoInventario, string> =
        let contraparteOpt = if row.contraparte_ref.HasValue then Some row.contraparte_ref.Value else None
        let nombreOpt = Option.ofObj row.contraparte_nombre
        let deptoOpt = Option.ofObj row.departamento
        let solOpt = Option.ofObj row.solicitante
        let obsOpt = Option.ofObj row.observaciones

        match TipoMovimiento.resolver
                row.tipo
                row.motivo
                contraparteOpt
                nombreOpt
                deptoOpt
                solOpt
                obsOpt with
        | Error err ->
            Error (sprintf "Movimiento %A: %A" row.id err)
        | Ok tipoDominio ->

        let (movId: MovimientoId) = MovimientoId row.id
        let (empId: EmpleadoId) = EmpleadoId row.responsable_id
        let ordenOpt =
            if row.orden_origen_id.HasValue then Some (OrdenId row.orden_origen_id.Value)
            else None

        let lineasResult =
            let rec loop acc remaining =
                match remaining with
                | [] -> Ok (List.rev acc)
                | l :: tail ->
                    let loteId = LoteId l.lote_id
                    match Cantidad.reconstruir l.cantidad (mapearUnidad l.unidad) with
                    | Error err -> Error (sprintf "Movimiento %A, Línea %A: cantidad inválida — %A" row.id l.id err)
                    | Ok cantidad ->
                        let linea : LineaMovimiento = { Referencia = loteId; Cantidad = cantidad }
                        loop (linea :: acc) tail
            loop [] lineasRows

        match lineasResult with
        | Error err -> Error err
        | Ok lineas ->
            Ok { Id = movId
                 Fecha = row.fecha
                 Responsable = empId
                 Tipo = tipoDominio
                 OrdenOrigen = ordenOpt
                 Lineas = lineas
                 Observaciones = obsOpt }

    let insertarMovimiento
        (movimiento: MovimientoInventario)
        (contraparteNombre: string option)
        (departamento: string option)
        (solicitante: string option)
        : Async<unit> =
        async {
            use conn = DbConnection.crear ()
            let (MovimientoId mId) = movimiento.Id
            let (EmpleadoId eId) = movimiento.Responsable
            let tipoStr, motivoStr, refGuid, defaultNombre = desmapearTipoMovimiento movimiento.Tipo

            let nombreFinal =
                match contraparteNombre with
                | Some n when not (String.IsNullOrWhiteSpace n) -> n
                | _ -> defaultNombre

            let ordenGuid =
                match movimiento.OrdenOrigen with
                | Some (OrdenId oId) -> Nullable oId
                | None -> Nullable ()

            let movRow : MovimientoInventarioRow =
                { id = mId
                  fecha = movimiento.Fecha
                  responsable_id = eId
                  tipo = tipoStr
                  motivo = motivoStr
                  orden_origen_id = ordenGuid
                  contraparte_ref = refGuid
                  contraparte_nombre = Option.toObj (Option.ofObj nombreFinal)
                  departamento = Option.toObj departamento
                  solicitante = Option.toObj solicitante
                  observaciones = Option.toObj movimiento.Observaciones }

            do! insert {
                    into InventoryTables.movimientoTable
                    value movRow
                }
                |> conn.InsertAsync
                |> Async.AwaitTask
                |> Async.Ignore

            for linea in movimiento.Lineas do
                let (LoteId lId) = linea.Referencia
                let lineaRow : LineaMovimientoRow =
                    { id = Identidad.nuevo ()
                      movimiento_id = mId
                      lote_id = lId
                      cantidad = Cantidad.valor linea.Cantidad
                      unidad = desmapearUnidad (Cantidad.unidad linea.Cantidad)
                      observaciones = null }

                do! insert {
                        into InventoryTables.lineaTable
                        value lineaRow
                    }
                    |> conn.InsertAsync
                    |> Async.AwaitTask
                    |> Async.Ignore
        }

    let listarMovimientos
        (tipoFilter: string option)
        : Async<(MovimientoInventario * MetadataMovimiento) list> =
        async {
            use conn = DbConnection.crear ()

            let! movRows =
                select {
                    for m in InventoryTables.movimientoTable do
                    selectAll
                }
                |> conn.SelectAsync<MovimientoInventarioRow>
                |> Async.AwaitTask

            let! lineaRows =
                select {
                    for l in InventoryTables.lineaTable do
                    selectAll
                }
                |> conn.SelectAsync<LineaMovimientoRow>
                |> Async.AwaitTask

            let lineasPorMov =
                lineaRows
                |> Seq.groupBy (fun l -> l.movimiento_id)
                |> Seq.map (fun (mId, lineas) -> (mId, Seq.toList lineas))
                |> Map.ofSeq

            let filtrados =
                movRows
                |> Seq.filter (fun m ->
                    match tipoFilter with
                    | None -> true
                    | Some t -> String.Equals(m.tipo, t, StringComparison.OrdinalIgnoreCase))
                |> Seq.sortByDescending (fun m -> m.fecha)
                |> Seq.choose (fun m ->
                    let lineas = lineasPorMov |> Map.tryFind m.id |> Option.defaultValue []
                    match aMovimientoDominio m lineas with
                    | Error msg ->
                        eprintfn "[WARN] InventoryRepository.listarMovimientos: %s" msg
                        None
                    | Ok dom ->
                        let meta =
                            { ContraparteNombre = Option.ofObj m.contraparte_nombre
                              Departamento = Option.ofObj m.departamento
                              Solicitante = Option.ofObj m.solicitante }
                        Some (dom, meta))
                |> Seq.toList

            return filtrados
        }

    let listarTodosMovimientosDominio () : Async<MovimientoInventario list> =
        async {
            let! conMeta = listarMovimientos None
            return conMeta |> List.map fst
        }

    let listarMovimientosPorLote (loteId: LoteId) : Async<MovimientoInventario list> =
        async {
            let! todos = listarTodosMovimientosDominio ()
            return todos |> List.filter (fun m ->
                m.Lineas |> List.exists (fun l -> l.Referencia = loteId))
        }
