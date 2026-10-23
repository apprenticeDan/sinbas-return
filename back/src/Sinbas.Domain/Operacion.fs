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
    | DevolucionAnulacion of OrdenId

module MotivoIngreso =

    let aTexto = function
        | Compra _ -> "Compra"
        | Recoleccion _ -> "Recoleccion"
        | DonacionRecibida _ -> "Donacion"
        | Devolucion _ -> "Devolucion"
        | TruequeEntrada _ -> "Trueque"
        | DevolucionAnulacion _ -> "DevolucionAnulacion"

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
        | "DevolucionAnulacion" ->
            match contraparteRef with
            | Some oId -> Ok (DevolucionAnulacion (OrdenId oId))
            | None -> Error (ValorRequerido "El ingreso por Devolución por Anulación requiere un OrdenId (contraparte_ref)")
        | desconocido ->
            Error (ValorRequerido (sprintf "Motivo de ingreso desconocido o inválido: '%s'" desconocido))

[<RequireQualifiedAccess>]
type DestinoUso =
    | Vivero
    | Laboratorio
    | Otro of string

module DestinoUso =
    let aTexto = function
        | DestinoUso.Vivero -> "Vivero"
        | DestinoUso.Laboratorio -> "Laboratorio"
        | DestinoUso.Otro s -> if String.IsNullOrWhiteSpace s then "Otro" else s.Trim()

    let desdeTexto (texto: string) : DestinoUso =
        match (if isNull texto then "" else texto.Trim()) with
        | s when String.Equals(s, "Vivero", StringComparison.OrdinalIgnoreCase) -> DestinoUso.Vivero
        | s when String.Equals(s, "Laboratorio", StringComparison.OrdinalIgnoreCase) || String.Equals(s, "Lab", StringComparison.OrdinalIgnoreCase) -> DestinoUso.Laboratorio
        | s when String.IsNullOrWhiteSpace s -> DestinoUso.Otro "General"
        | s -> DestinoUso.Otro s

type MotivoEgreso =
    | Venta of ClienteId
    | MuestraLab of LaboratorioId
    | Merma of causa: string
    | UsoInterno of destino: DestinoUso * solicitante: string
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
            let sol =
                solicitante
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Personal Interno"
            let destino =
                match departamento with
                | Some d when not (String.IsNullOrWhiteSpace d) -> DestinoUso.desdeTexto d
                | _ ->
                    match observaciones with
                    | Some o when not (String.IsNullOrWhiteSpace o) -> DestinoUso.desdeTexto o
                    | _ -> DestinoUso.Otro "General"
            Ok (UsoInterno (destino, sol))
        | "UsoVivero" ->
            let sol =
                solicitante
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Vivero"
            Ok (UsoInterno (DestinoUso.Vivero, sol))
        | "UsoLabor" ->
            let sol =
                solicitante
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                |> Option.defaultValue "Laboratorio"
            Ok (UsoInterno (DestinoUso.Laboratorio, sol))
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

// ─────────────────────────────────────────────────────────────
// Solicitud de Egreso Interno / Movimientos Internos (F10 / OBS-10)
// Unifica UsoInterno, Ajustes y Mermas bajo un modelo común
// ─────────────────────────────────────────────────────────────

type SolicitudEgresoInternoId = SolicitudEgresoInternoId of Guid

[<RequireQualifiedAccess>]
type TipoEgresoInterno =
    | UsoInterno of DestinoUso
    | Ajuste
    | Merma

type SignoEgresoInterno =
    | Positivo
    | Negativo

module SignoEgresoInterno =
    let aDecimal = function
        | Positivo -> 1m
        | Negativo -> -1m

    let aTexto = function
        | Positivo -> "+"
        | Negativo -> "-"

type ResolucionSolicitud =
    { Aprobador: EmpleadoId
      Fecha: DateTime
      Motivo: string option }

type EntregaSolicitud =
    { EntregadoPor: EmpleadoId
      Fecha: DateTime }

type EstadoSolicitudInterna =
    | Pendiente
    | Aprobada of ResolucionSolicitud
    | Rechazada of ResolucionSolicitud
    | Confirmada of EntregaSolicitud

type SolicitudEgresoInterno =
    { Id: SolicitudEgresoInternoId
      Tipo: TipoEgresoInterno
      Signo: SignoEgresoInterno
      ProductoId: ProductoId option
      LoteId: LoteId option
      Cantidad: Cantidad
      Motivo: string
      Solicitante: EmpleadoId
      Estado: EstadoSolicitudInterna
      CreadoEn: DateTime }

module SolicitudEgresoInterno =

    let crear
        (id: SolicitudEgresoInternoId)
        (tipo: TipoEgresoInterno)
        (signo: SignoEgresoInterno)
        (productoId: ProductoId option)
        (loteId: LoteId option)
        (cantidad: Cantidad)
        (motivo: string)
        (solicitante: EmpleadoId)
        (creadoEn: DateTime)
        : Result<SolicitudEgresoInterno, DomainError> =
        if String.IsNullOrWhiteSpace motivo then
            Error (ValorRequerido "El motivo de la solicitud es obligatorio")
        elif Cantidad.valor cantidad <= 0m then
            Error (CantidadInvalida "La cantidad debe ser mayor a cero")
        elif Option.isNone productoId && Option.isNone loteId then
            Error (ValorRequerido "La solicitud debe especificar al menos un producto o un lote")
        elif (match tipo with TipoEgresoInterno.UsoInterno _ | TipoEgresoInterno.Merma -> signo = Positivo | TipoEgresoInterno.Ajuste -> false) then
            Error (OperacionInvalida "El uso interno o merma no puede tener signo positivo")
        else
            Ok {
                Id = id
                Tipo = tipo
                Signo = signo
                ProductoId = productoId
                LoteId = loteId
                Cantidad = cantidad
                Motivo = motivo.Trim()
                Solicitante = solicitante
                Estado = Pendiente
                CreadoEn = creadoEn
            }

    let aprobar
        (aprobador: EmpleadoId)
        (fecha: DateTime)
        (solicitud: SolicitudEgresoInterno)
        : Result<SolicitudEgresoInterno, DomainError> =
        match solicitud.Estado with
        | Pendiente ->
            match solicitud.Tipo with
            | TipoEgresoInterno.UsoInterno _ ->
                Error (OperacionInvalida "El uso interno no requiere aprobación previa sino confirmación física de entrega")
            | TipoEgresoInterno.Ajuste | TipoEgresoInterno.Merma ->
                if aprobador = solicitud.Solicitante then
                    Error (OperacionInvalida "RN11: El aprobador no puede ser el mismo usuario que registró la solicitud")
                else
                    let resolucion = { Aprobador = aprobador; Fecha = fecha; Motivo = None }
                    Ok { solicitud with Estado = Aprobada resolucion }
        | Aprobada _ -> Error (OperacionInvalida "La solicitud ya se encuentra aprobada")
        | Rechazada _ -> Error (OperacionInvalida "No se puede aprobar una solicitud rechazada")
        | Confirmada _ -> Error (OperacionInvalida "No se puede aprobar una solicitud ya confirmada")

    let rechazar
        (rechazador: EmpleadoId)
        (motivoRechazo: string)
        (fecha: DateTime)
        (solicitud: SolicitudEgresoInterno)
        : Result<SolicitudEgresoInterno, DomainError> =
        if String.IsNullOrWhiteSpace motivoRechazo then
            Error (ValorRequerido "El rechazo de la solicitud exige un motivo justificado")
        else
            match solicitud.Estado with
            | Pendiente ->
                let resolucion = { Aprobador = rechazador; Fecha = fecha; Motivo = Some (motivoRechazo.Trim()) }
                Ok { solicitud with Estado = Rechazada resolucion }
            | Aprobada _ -> Error (OperacionInvalida "No se puede rechazar una solicitud que ya fue aprobada")
            | Rechazada _ -> Error (OperacionInvalida "La solicitud ya se encuentra rechazada")
            | Confirmada _ -> Error (OperacionInvalida "No se puede rechazar una solicitud ya confirmada")

    let confirmar
        (confirmador: EmpleadoId)
        (fecha: DateTime)
        (solicitud: SolicitudEgresoInterno)
        : Result<SolicitudEgresoInterno, DomainError> =
        match solicitud.Estado with
        | Pendiente ->
            match solicitud.Tipo with
            | TipoEgresoInterno.UsoInterno _ ->
                let entrega = { EntregadoPor = confirmador; Fecha = fecha }
                Ok { solicitud with Estado = Confirmada entrega }
            | TipoEgresoInterno.Ajuste | TipoEgresoInterno.Merma ->
                Error (OperacionInvalida "Los ajustes y mermas requieren aprobación gerencial previa antes de su confirmación")
        | Aprobada _ ->
            let entrega = { EntregadoPor = confirmador; Fecha = fecha }
            Ok { solicitud with Estado = Confirmada entrega }
        | Rechazada _ -> Error (OperacionInvalida "No se puede confirmar una solicitud rechazada")
        | Confirmada _ -> Error (OperacionInvalida "La solicitud ya se encuentra confirmada")

