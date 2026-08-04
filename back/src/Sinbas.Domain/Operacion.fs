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

type MotivoEgreso =
    | Venta of ClienteId
    | MuestraLab of LaboratorioId
    | Merma of causa: string
    | UsoInterno of descripcion: string
    | DonacionEnviada of destinatario: string
    | TruequeSalida of TruequeId

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
