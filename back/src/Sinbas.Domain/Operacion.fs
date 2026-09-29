namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────
// Anulación
// ─────────────────────────────────────────────

type Anulacion =
    { Fecha: DateTime
      Motivo: string
      AnuladoPor: EmpleadoId }

// ─────────────────────────────────────────────
// Estados
// ─────────────────────────────────────────────

type EstadoOperacion =
    | Borrador
    | Confirmada
    | Ejecutada
    | Anulada of Anulacion

type EstadoProforma =
    | Borrador
    | Vigente
    | Vencida
    | Convertida of OrdenId
    | ProformaAnulada of Anulacion

// ─────────────────────────────────────────────
// Contraparte
// ─────────────────────────────────────────────

type Contraparte =
    | Cliente of ClienteId
    | Proveedor of ProveedorId
    | Donante of nombre: string
    | Destinatario of nombre: string
    | Interno

// ─────────────────────────────────────────────
// Encabezado
// ─────────────────────────────────────────────

type EncabezadoOperacion =
    { Id: OrdenId
      Fecha: DateOnly
      CreadoEn: DateTime
      Responsable: EmpleadoId
      Contraparte: Contraparte
      Estado: EstadoOperacion
      Observaciones: string option }

type MotivoIngreso =
    | Compra of ProveedorId
    | Recoleccion of campana: string
    | DonacionRecibida of donante: string
    | Devolucion of ClienteId
    | TruequeEntrada of TruequeId

module MotivoIngreso =

    let aTexto = function
        | Compra _ -> "Compra"
        | Recoleccion _ -> "Recoleccion"
        | DonacionRecibida _ -> "Donacion"
        | Devolucion _ -> "Devolucion"
        | TruequeEntrada _ -> "Trueque"

    let desdeTexto (motivo: string) (contraparteRef: Guid option) (contraparteNombre: string option) : Result<MotivoIngreso, DomainError> =
        match (if isNull motivo then "" else motivo.Trim()) with
        | "Compra" ->
            match contraparteRef with
            | Some pId -> Ok (Compra (ProveedorId pId))
            | None -> Error (ValorRequerido "El ingreso por Compra requiere un ProveedorId (contraparte_ref)")
        | "Recoleccion" ->
            let campana =
                contraparteNombre
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Recolección"
            Ok (Recoleccion campana)
        | "Donacion" ->
            let donante =
                contraparteNombre
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Donante Anónimo"
            Ok (DonacionRecibida donante)
        | "Devolucion" ->
            match contraparteRef with
            | Some cId -> Ok (Devolucion (ClienteId cId))
            | None -> Error (ValorRequerido "El ingreso por Devolución requiere un ClienteId (contraparte_ref)")
        | "Trueque" ->
            match contraparteRef with
            | Some tId -> Ok (TruequeEntrada (TruequeId tId))
            | None -> Error (ValorRequerido "El ingreso por Trueque requiere un TruequeId (contraparte_ref)")
        | desconocido ->
            Error (ValorRequerido (sprintf "Motivo de ingreso desconocido o inválido: '%s'" desconocido))

type MotivoEgreso =
    | Venta of ClienteId
    | MuestraLab of LaboratorioId
    | Merma of causa: string
    | UsoInterno of descripcion: string
    | DonacionEnviada of destinatario: string
    | TruequeSalida of TruequeId

module MotivoEgreso =

    let aTexto = function
        | Venta _ -> "Venta"
        | MuestraLab _ -> "MuestraLab"
        | Merma _ -> "Merma"
        | UsoInterno _ -> "UsoInterno"
        | DonacionEnviada _ -> "Donacion"
        | TruequeSalida _ -> "Trueque"

    let desdeTexto
        (motivo: string)
        (contraparteRef: Guid option)
        (contraparteNombre: string option)
        (departamento: string option)
        (solicitante: string option)
        (observaciones: string option)
        : Result<MotivoEgreso, DomainError> =
        match (if isNull motivo then "" else motivo.Trim()) with
        | "Venta" ->
            match contraparteRef with
            | Some cId -> Ok (Venta (ClienteId cId))
            | None -> Error (ValorRequerido "El egreso por Venta requiere un ClienteId (contraparte_ref)")
        | "MuestraLab" ->
            match contraparteRef with
            | Some lId -> Ok (MuestraLab (LaboratorioId lId))
            | None -> Error (ValorRequerido "El egreso por MuestraLab requiere un LaboratorioId (contraparte_ref)")
        | "Merma" ->
            let causa =
                observaciones
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Merma operativa"
            Ok (Merma causa)
        | "UsoInterno" ->
            let partes = [
                match departamento with Some d when not (String.IsNullOrWhiteSpace d) -> sprintf "[Depto: %s]" (d.Trim()) | _ -> ()
                match solicitante with Some s when not (String.IsNullOrWhiteSpace s) -> sprintf "[Solicitante: %s]" (s.Trim()) | _ -> ()
                match observaciones with Some o when not (String.IsNullOrWhiteSpace o) -> o.Trim() | _ -> ()
            ]
            let desc = if List.isEmpty partes then "Uso Interno" else String.concat " " partes
            Ok (UsoInterno desc)
        | "Donacion" ->
            let dest =
                contraparteNombre
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Destinatario General"
            Ok (DonacionEnviada dest)
        | "Trueque" ->
            match contraparteRef with
            | Some tId -> Ok (TruequeSalida (TruequeId tId))
            | None -> Error (ValorRequerido "El egreso por Trueque requiere un TruequeId (contraparte_ref)")
        | desconocido ->
            Error (ValorRequerido (sprintf "Motivo de egreso desconocido o inválido: '%s'" desconocido))

type Linea<'TRef> =
    { Referencia: 'TRef
      Cantidad: Cantidad }

type LineaSolicitud = Linea<ProductoId>
type LineaMovimiento = Linea<LoteId>

(* type LineaProforma =
    { ProductoId: ProductoId
      Cantidad: Cantidad }

type LineaIngreso =
    { ProductoId: ProductoId
      Cantidad: Cantidad }

type LineaSolicitud =
    { ProductoId: ProductoId
      Cantidad: Cantidad }

type LineaMovimiento = { LoteId: LoteId; Cantidad: Cantidad } *)

// ─────────────────────────────────────────────────────────────
// Documento genérico — composición sobre encabezado
// 'TMotivo  = tipo del motivo (ingreso o egreso)
// 'TLinea   = tipo de línea (intención o ejecutada)
// ─────────────────────────────────────────────────────────────

type Documento<'TMotivo, 'TLinea> =
    { Encabezado: EncabezadoOperacion
      Motivo: 'TMotivo
      Lineas: 'TLinea list }

// ─────────────────────────────────────────────────────────────
// Solicitudes — intención sin lotes resueltos
// ─────────────────────────────────────────────────────────────

type SolicitudIngreso = Documento<MotivoIngreso, LineaSolicitud>
type SolicitudEgreso = Documento<MotivoEgreso, LineaSolicitud>

// ─────────────────────────────────────────────────────────────
// Órdenes — lotes ya asignados, listas para ejecutar
// ─────────────────────────────────────────────────────────────

type OrdenIngreso = Documento<MotivoIngreso, LineaMovimiento>
type OrdenEgreso = Documento<MotivoEgreso, LineaMovimiento>


// type Venta = Documento<MotivoEgreso, LineaSolicitud>

type Proforma =
    { Encabezado: EncabezadoOperacion
      FechaVencimiento: DateOnly
      EstadoProforma: EstadoProforma
      Lineas: LineaSolicitud list }

type Trueque =
    { Id: TruequeId
      Encabezado: EncabezadoOperacion
      OrdenIngreso: OrdenIngreso
      OrdenEgreso: OrdenEgreso }

// ─────────────────────────────────────────────────────────────
// Funciones puras sobre estados
// ─────────────────────────────────────────────────────────────

module EstadoOperacion =

    let esEjecutada =
        function
        | Ejecutada -> true
        | _ -> false

    let esAnulada =
        function
        | Anulada _ -> true
        | _ -> false

    let puedeEjecutarse =
        function
        | Confirmada -> true
        | _ -> false

    let anular (motivo: string) (empleado: EmpleadoId) (ahora: DateTime) (estado: EstadoOperacion) =
        match estado with
        | Ejecutada -> Error(ValorRequerido "No se puede anular una operación ya ejecutada")
        | Anulada _ -> Error(ValorRequerido "La operación ya está anulada")
        | _ ->
            Ok(
                Anulada
                    { Fecha = ahora
                      Motivo = motivo
                      AnuladoPor = empleado }
            )

module EstadoProforma =

    let estaVigente (hoy: DateOnly) (p: Proforma) =
        match p.EstadoProforma with
        | Vigente -> p.FechaVencimiento >= hoy
        | _ -> false

    let emitir (p: Proforma) =
        match p.EstadoProforma with
        | Borrador -> Ok { p with EstadoProforma = Vigente }
        | _ -> Error(ValorRequerido "Solo una proforma en estado Borrador puede pasar a Vigente")

    let vencer (p: Proforma) =
        match p.EstadoProforma with
        | Vigente -> { p with EstadoProforma = Vencida }
        | _ -> p

    let convertir (ordenId: OrdenId) (p: Proforma) =
        match p.EstadoProforma with
        | Vigente ->
            Ok
                { p with
                    EstadoProforma = Convertida ordenId }
        | Vencida -> Error(ValorRequerido "No se puede convertir una proforma vencida")
        | _ -> Error(ValorRequerido "La proforma no está vigente")

    let anular (motivo: string) (empleado: EmpleadoId) (ahora: DateTime) (p: Proforma) =
        match p.EstadoProforma with
        | Convertida _ -> Error(ValorRequerido "No se puede anular una proforma ya convertida")
        | ProformaAnulada _ -> Error(ValorRequerido "La proforma ya está anulada")
        | _ ->
            Ok
                { p with
                    EstadoProforma =
                        ProformaAnulada
                            { Fecha = ahora
                              Motivo = motivo
                              AnuladoPor = empleado } }

