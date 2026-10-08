namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Feature F8: Despacho Físico de Inventario
// (RF-08 | CU-11 | RN12 / RN14 / FIFO)
// ─────────────────────────────────────────────────────────────

type CodigoDespacho = private CodigoDespacho of string

module CodigoDespacho =
    let valor (CodigoDespacho c) = c

    let crear (s: string) : Result<CodigoDespacho, DomainError> =
        if String.IsNullOrWhiteSpace s then
            Error (ValorRequerido "El código de despacho no puede estar vacío")
        else
            Ok (CodigoDespacho (s.Trim().ToUpperInvariant()))

    let reconstruir (s: string) : CodigoDespacho =
        CodigoDespacho (if isNull s then "" else s.Trim())

    let generar (fecha: DateOnly) (secuencia: int) : Result<CodigoDespacho, DomainError> =
        if secuencia < 1 then
            Error (SecuenciaInvalida (sprintf "Secuencia de despacho inválida: %d" secuencia))
        else
            let codigo = sprintf "DSP-%d%02d-%04d" fecha.Year fecha.Month secuencia
            Ok (CodigoDespacho codigo)

[<RequireQualifiedAccess>]
type OrigenDespacho =
    | DeVenta of OrdenId
    | InternaSolicitud of OrdenId

module OrigenDespacho =
    let aTexto = function
        | OrigenDespacho.DeVenta _ -> "DeVenta"
        | OrigenDespacho.InternaSolicitud _ -> "InternaSolicitud"

    let ordenId = function
        | OrigenDespacho.DeVenta (OrdenId oid) -> oid
        | OrigenDespacho.InternaSolicitud (OrdenId oid) -> oid

    let desdeTexto (tipo: string) (ordenId: Guid) : Result<OrigenDespacho, DomainError> =
        match (if isNull tipo then "" else tipo.Trim().ToLowerInvariant()) with
        | "deventa" | "venta" -> Ok (OrigenDespacho.DeVenta (OrdenId ordenId))
        | "internasolicitud" | "interno" | "solicitud" -> Ok (OrigenDespacho.InternaSolicitud (OrdenId ordenId))
        | otro -> Error (ValorRequerido (sprintf "Tipo de origen de despacho desconocido: '%s'" otro))

[<RequireQualifiedAccess>]
type EstadoDespacho =
    | Pendiente
    | Despachado of MovimientoId
    | Anulado of Anulacion

module EstadoDespacho =
    let aTexto = function
        | EstadoDespacho.Pendiente -> "Pendiente"
        | EstadoDespacho.Despachado _ -> "Despachado"
        | EstadoDespacho.Anulado _ -> "Anulado"

    let desdeTexto (s: string) (movimientoId: Guid option) (anulacion: Anulacion option) : Result<EstadoDespacho, DomainError> =
        match (if isNull s then "" else s.Trim().ToLowerInvariant()) with
        | "pendiente" -> Ok EstadoDespacho.Pendiente
        | "despachado" ->
            match movimientoId with
            | Some mId -> Ok (EstadoDespacho.Despachado (MovimientoId mId))
            | None -> Ok (EstadoDespacho.Despachado (MovimientoId Guid.Empty))
        | "anulado" ->
            match anulacion with
            | Some a -> Ok (EstadoDespacho.Anulado a)
            | None ->
                Ok (EstadoDespacho.Anulado {
                    Fecha = DateTime.UtcNow
                    Motivo = "Anulación registrada"
                    AnuladoPor = EmpleadoId Guid.Empty
                })
        | otro -> Error (ValorRequerido (sprintf "Estado de despacho desconocido: '%s'" otro))

type OrdenDespacho =
    { Id: OrdenId
      Codigo: CodigoDespacho
      Origen: OrigenDespacho
      ClienteId: ClienteId option
      Lineas: LineaSolicitud list
      Estado: EstadoDespacho
      CreadoEn: DateTime }

module Despacho =

    /// Construye una OrdenDespacho a partir de una OrdenVenta confirmada
    let desdeVenta
        (despachoId: OrdenId)
        (codigo: CodigoDespacho)
        (ordenVenta: OrdenVenta)
        : OrdenDespacho =

        let lineasSolicitud : LineaSolicitud list =
            ordenVenta.Lineas
            |> List.map (fun l -> { Referencia = l.ProductoId; Cantidad = l.Cantidad })

        { Id = despachoId
          Codigo = codigo
          Origen = OrigenDespacho.DeVenta ordenVenta.Id
          ClienteId = Some ordenVenta.ClienteId
          Lineas = lineasSolicitud
          Estado = EstadoDespacho.Pendiente
          CreadoEn = DateTime.UtcNow }

    /// Marca la orden de despacho como Despachada asociándole el MovimientoId generado en almacén
    let marcarDespachado
        (movimientoId: MovimientoId)
        (ordenDespacho: OrdenDespacho)
        : Result<OrdenDespacho, DomainError> =

        match ordenDespacho.Estado with
        | EstadoDespacho.Despachado _ ->
            Error (OperacionInvalida "La orden de despacho ya fue despachada")
        | EstadoDespacho.Anulado _ ->
            Error (OperacionInvalida "No se puede despachar una orden de despacho anulada")
        | EstadoDespacho.Pendiente ->
            Ok { ordenDespacho with Estado = EstadoDespacho.Despachado movimientoId }

    /// Anula una orden de despacho pendiente
    let anular
        (motivo: string)
        (anuladoPor: EmpleadoId)
        (ahora: DateTime)
        (ordenDespacho: OrdenDespacho)
        : Result<OrdenDespacho, DomainError> =

        match ordenDespacho.Estado with
        | EstadoDespacho.Despachado _ ->
            Error (OperacionInvalida "No se puede anular una orden de despacho que ya fue ejecutada físicamente")
        | EstadoDespacho.Anulado _ ->
            Error (OperacionInvalida "La orden de despacho ya se encuentra anulada")
        | EstadoDespacho.Pendiente ->
            if String.IsNullOrWhiteSpace motivo then
                Error (ValorRequerido "El motivo de anulación del despacho es obligatorio")
            else
                let anulacion : Anulacion =
                    { Fecha = ahora
                      Motivo = motivo.Trim()
                      AnuladoPor = anuladoPor }
                Ok { ordenDespacho with Estado = EstadoDespacho.Anulado anulacion }
