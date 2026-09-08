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

    let private mapearTipoMovimiento
        (tipoStr: string)
        (motivoStr: string)
        (contraparteRef: Nullable<Guid>)
        (contraparteNombre: string)
        (departamento: string)
        (solicitante: string)
        (observaciones: string)
        : TipoMovimiento =
        match tipoStr with
        | "Entrada" ->
            match motivoStr with
            | "Compra" ->
                let pId =
                    if contraparteRef.HasValue then ProveedorId contraparteRef.Value
                    else ProveedorId (Guid.Parse("01917f3a-0005-7000-8000-000000000001"))
                Entrada (Compra pId)
            | "Recoleccion" ->
                let campana = if String.IsNullOrWhiteSpace contraparteNombre then "Campaña General" else contraparteNombre
                Entrada (Recoleccion campana)
            | "Donacion" ->
                let donante = if String.IsNullOrWhiteSpace contraparteNombre then "Donante Anónimo" else contraparteNombre
                Entrada (DonacionRecibida donante)
            | "Devolucion" ->
                let cId =
                    if contraparteRef.HasValue then ClienteId contraparteRef.Value
                    else ClienteId (Guid.Parse("01917f3a-0006-7000-8000-000000000001"))
                Entrada (Devolucion cId)
            | "Trueque" ->
                let tId =
                    if contraparteRef.HasValue then TruequeId contraparteRef.Value
                    else TruequeId (Guid.Parse("01917f3a-0007-7000-8000-000000000001"))
                Entrada (TruequeEntrada tId)
            | _ ->
                let nom = if String.IsNullOrWhiteSpace motivoStr then "Ingreso General" else motivoStr
                Entrada (Recoleccion nom)
        | "Salida" ->
            match motivoStr with
            | "Venta" ->
                let cId =
                    if contraparteRef.HasValue then ClienteId contraparteRef.Value
                    else ClienteId (Guid.Parse("01917f3a-0006-7000-8000-000000000001"))
                Salida (Venta cId)
            | "Merma" ->
                let causa = if String.IsNullOrWhiteSpace observaciones then "Merma operativa" else observaciones
                Salida (Merma causa)
            | "UsoInterno" ->
                let partes = [
                    if not (String.IsNullOrWhiteSpace departamento) then sprintf "[Depto: %s]" departamento
                    if not (String.IsNullOrWhiteSpace solicitante) then sprintf "[Solicitante: %s]" solicitante
                    if not (String.IsNullOrWhiteSpace observaciones) then observaciones
                ]
                let desc = if List.isEmpty partes then "Uso Interno" else String.concat " " partes
                Salida (UsoInterno desc)
            | "MuestraLab" ->
                let lId =
                    if contraparteRef.HasValue then LaboratorioId contraparteRef.Value
                    else LaboratorioId (Guid.Parse("01917f3a-0008-7000-8000-000000000001"))
                Salida (MuestraLab lId)
            | "Donacion" ->
                let dest = if String.IsNullOrWhiteSpace contraparteNombre then "Destinatario General" else contraparteNombre
                Salida (DonacionEnviada dest)
            | "Trueque" ->
                let tId =
                    if contraparteRef.HasValue then TruequeId contraparteRef.Value
                    else TruequeId (Guid.Parse("01917f3a-0007-7000-8000-000000000001"))
                Salida (TruequeSalida tId)
            | _ ->
                Salida (UsoInterno (if String.IsNullOrWhiteSpace motivoStr then "Salida General" else motivoStr))
        | _ ->
            Entrada (Recoleccion "Movimiento no tipificado")

    let private desmapearTipoMovimiento (tipo: TipoMovimiento) : string * string * Nullable<Guid> * string =
        match tipo with
        | Entrada motivoIngreso ->
            match motivoIngreso with
            | Compra (ProveedorId provId) -> ("Entrada", "Compra", Nullable provId, null)
            | Recoleccion campana -> ("Entrada", "Recoleccion", Nullable(), campana)
            | DonacionRecibida donante -> ("Entrada", "Donacion", Nullable(), donante)
            | Devolucion (ClienteId cliId) -> ("Entrada", "Devolucion", Nullable cliId, null)
            | TruequeEntrada (TruequeId truId) -> ("Entrada", "Trueque", Nullable truId, null)
        | Salida motivoEgreso ->
            match motivoEgreso with
            | Venta (ClienteId cliId) -> ("Salida", "Venta", Nullable cliId, null)
            | MuestraLab (LaboratorioId labId) -> ("Salida", "MuestraLab", Nullable labId, null)
            | Merma causa -> ("Salida", "Merma", Nullable(), null)
            | UsoInterno desc -> ("Salida", "UsoInterno", Nullable(), null)
            | DonacionEnviada dest -> ("Salida", "Donacion", Nullable(), dest)
            | TruequeSalida (TruequeId truId) -> ("Salida", "Trueque", Nullable truId, null)

    let private aMovimientoDominio (row: MovimientoInventarioRow) (lineasRows: LineaMovimientoRow list) : MovimientoInventario =
        let (movId: MovimientoId) = MovimientoId row.id
        let (empId: EmpleadoId) = EmpleadoId row.responsable_id
        let ordenOpt =
            if row.orden_origen_id.HasValue then Some (OrdenId row.orden_origen_id.Value)
            else None
        let obsOpt =
            if String.IsNullOrWhiteSpace row.observaciones then None
            else Some row.observaciones

        let lineas =
            lineasRows
            |> List.map (fun l ->
                let loteId = LoteId l.lote_id
                let cantidad = { Valor = l.cantidad; Unidad = mapearUnidad l.unidad }
                { Referencia = loteId; Cantidad = cantidad } : LineaMovimiento)

        let tipoDominio =
            mapearTipoMovimiento
                row.tipo
                row.motivo
                row.contraparte_ref
                row.contraparte_nombre
                row.departamento
                row.solicitante
                row.observaciones

        { Id = movId
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
                      cantidad = linea.Cantidad.Valor
                      unidad = desmapearUnidad linea.Cantidad.Unidad
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
                |> Seq.map (fun m ->
                    let lineas = lineasPorMov |> Map.tryFind m.id |> Option.defaultValue []
                    let dom = aMovimientoDominio m lineas
                    let meta =
                        { ContraparteNombre = Option.ofObj m.contraparte_nombre
                          Departamento = Option.ofObj m.departamento
                          Solicitante = Option.ofObj m.solicitante }
                    (dom, meta))
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
