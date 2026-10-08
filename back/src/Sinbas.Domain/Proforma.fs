namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Feature F7: Elaboración de Proformas y Cotizaciones
// (RF-07 | CU-10 | RN04 / RN05 / RN13 / RN16)
// ─────────────────────────────────────────────────────────────

/// Línea individual de una cotización con precio congelado (RN13)
type LineaCotizada =
    { ProductoId: ProductoId
      Cantidad: Cantidad
      PrecioUnitario: decimal }

/// Estado del ciclo de vida de una proforma
[<RequireQualifiedAccess>]
type EstadoProforma =
    | Vigente
    | Vencida
    | Convertida
    | Anulada

module EstadoProforma =
    let aTexto = function
        | EstadoProforma.Vigente    -> "Vigente"
        | EstadoProforma.Vencida    -> "Vencida"
        | EstadoProforma.Convertida -> "Convertida"
        | EstadoProforma.Anulada    -> "Anulada"

    let desdeTexto (s: string) : Result<EstadoProforma, DomainError> =
        match (if isNull s then "" else s.Trim().ToLowerInvariant()) with
        | "vigente"    -> Ok EstadoProforma.Vigente
        | "vencida"    -> Ok EstadoProforma.Vencida
        | "convertida" -> Ok EstadoProforma.Convertida
        | "anulada"    -> Ok EstadoProforma.Anulada
        | otro         -> Error (ValorRequerido (sprintf "Estado de proforma desconocido: '%s'" otro))

[<AutoOpen>]
module ProformaConstantes =
    /// Leyenda legal obligatoria (MF-07-02 / RN05)
    [<Literal>]
    let LeyendaNoReserva =
        "Disponibilidad sujeta a cambios - La proforma no reserva stock"

/// Entidad Proforma completa
type Proforma =
    { Id: ProformaId
      Fecha: DateOnly
      CreadoEn: DateTime
      ResponsableId: UsuarioId
      ClienteId: ClienteId option
      ClienteNombreLibre: string option
      Estado: EstadoProforma
      FechaVencimiento: DateOnly option
      Moneda: string
      Lineas: LineaCotizada list
      Total: decimal
      Leyenda: string
      OrdenVentaId: OrdenId option
      AnulacionMotivo: string option
      AnuladoPor: UsuarioId option
      AnuladoEn: DateTime option
      Observaciones: string option }

// ─────────────────────────────────────────────────────────────
// Módulo Cotizacion — funciones puras de cálculo (RN13)
// ─────────────────────────────────────────────────────────────

module Cotizacion =

    /// Subtotal de una línea = Cantidad × PrecioUnitario,
    /// redondeado con MidpointRounding.AwayFromZero (bancario)
    let subtotal (linea: LineaCotizada) : decimal =
        Math.Round(linea.Cantidad.Valor * linea.PrecioUnitario, 2, MidpointRounding.AwayFromZero)

    /// Total de la cotización = Σ subtotales
    let total (lineas: LineaCotizada list) : decimal =
        lineas |> List.sumBy subtotal

// ─────────────────────────────────────────────────────────────
// Módulo Proforma — crear, proyectar estado, anular
// ─────────────────────────────────────────────────────────────

module Proforma =

    /// Días de vigencia por defecto cuando no se especifica fecha de vencimiento
    let diasVigenciaDefault = 7

    /// Crea una proforma validando invariantes de dominio:
    /// - Lista de líneas no vacía
    /// - Sin productos duplicados
    /// - Cantidades > 0 y precios > 0
    /// - Fecha de vencimiento >= fecha de emisión (si se especifica)
    let crear
        (id: ProformaId)
        (responsableId: UsuarioId)
        (clienteId: ClienteId option)
        (clienteNombreLibre: string option)
        (lineas: LineaCotizada list)
        (fecha: DateOnly)
        (fechaVencimiento: DateOnly option)
        (moneda: string option)
        (observaciones: string option)
        : Result<Proforma, DomainError> =

        if List.isEmpty lineas then
            Error (ValorRequerido "La proforma debe contener al menos una línea de cotización")
        else

        // Sin productos duplicados
        let productoIds = lineas |> List.map (fun l -> l.ProductoId)
        let unicos = productoIds |> List.distinct
        if unicos.Length <> productoIds.Length then
            Error (OperacionInvalida "La proforma contiene productos duplicados")
        else

        // Validar cantidades > 0 y precios > 0
        let lineaInvalida =
            lineas |> List.tryFind (fun l -> l.Cantidad.Valor <= 0m || l.PrecioUnitario <= 0m)
        match lineaInvalida with
        | Some l ->
            Error (CantidadInvalida (sprintf "Cantidad (%M) y precio (%M) deben ser mayores a cero" l.Cantidad.Valor l.PrecioUnitario))
        | None ->

        // Fecha de vencimiento
        let fv =
            match fechaVencimiento with
            | Some fv -> fv
            | None -> fecha.AddDays(diasVigenciaDefault)

        if fv < fecha then
            Error (FechaInvalida "La fecha de vencimiento no puede ser anterior a la fecha de emisión")
        else

        let totalCalc = Cotizacion.total lineas
        let mon = defaultArg moneda "BOB"
        let obsLimpia =
            observaciones
            |> Option.bind (fun s ->
                let t = s.Trim()
                if String.IsNullOrWhiteSpace t then None else Some t)
        let clienteNombreLimpio =
            clienteNombreLibre
            |> Option.bind (fun s ->
                let t = s.Trim()
                if String.IsNullOrWhiteSpace t then None else Some t)

        Ok { Id = id
             Fecha = fecha
             CreadoEn = DateTime.UtcNow
             ResponsableId = responsableId
             ClienteId = clienteId
             ClienteNombreLibre = clienteNombreLimpio
             Estado = EstadoProforma.Vigente
             FechaVencimiento = Some fv
             Moneda = mon
             Lineas = lineas
             Total = totalCalc
             Leyenda = LeyendaNoReserva
             OrdenVentaId = None
             AnulacionMotivo = None
             AnuladoPor = None
             AnuladoEn = None
             Observaciones = obsLimpia }

    /// Proyecta el estado de vencimiento sin mutar base de datos (inmutabilidad funcional pura)
    let proyectarEstado (hoy: DateOnly) (p: Proforma) : EstadoProforma =
        match p.Estado with
        | EstadoProforma.Anulada -> EstadoProforma.Anulada
        | EstadoProforma.Convertida -> EstadoProforma.Convertida
        | _ ->
            match p.FechaVencimiento with
            | Some fv when hoy > fv -> EstadoProforma.Vencida
            | _ -> p.Estado

    /// Anula una proforma exigiendo motivo y responsable
    let anular (motivo: string) (anuladoPor: UsuarioId) (p: Proforma) : Result<Proforma, DomainError> =
        match p.Estado with
        | EstadoProforma.Anulada ->
            Error (OperacionInvalida "La proforma ya fue anulada")
        | EstadoProforma.Convertida ->
            Error (OperacionInvalida "No se puede anular una proforma que ya fue convertida a orden de venta")
        | _ ->
            if String.IsNullOrWhiteSpace motivo then
                Error (ValorRequerido "El motivo de anulación es obligatorio")
            else
                Ok { p with
                        Estado = EstadoProforma.Anulada
                        AnulacionMotivo = Some (motivo.Trim())
                        AnuladoPor = Some anuladoPor
                        AnuladoEn = Some DateTime.UtcNow }
