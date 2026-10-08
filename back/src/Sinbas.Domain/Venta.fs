namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Feature F8: Confirmación de Venta y Despacho Físico
// (RF-08 | CU-11 | RN04 / RN05 / RN12 / RN14 / RNF05)
// ─────────────────────────────────────────────────────────────

type CodigoVenta = private CodigoVenta of string

module CodigoVenta =
    let valor (CodigoVenta c) = c

    let crear (s: string) : Result<CodigoVenta, DomainError> =
        if String.IsNullOrWhiteSpace s then
            Error (ValorRequerido "El código de venta no puede estar vacío")
        else
            Ok (CodigoVenta (s.Trim().ToUpperInvariant()))

    let reconstruir (s: string) : CodigoVenta =
        CodigoVenta (if isNull s then "" else s.Trim())

    let generar (fecha: DateOnly) (secuencia: int) : Result<CodigoVenta, DomainError> =
        if secuencia < 1 then
            Error (SecuenciaInvalida (sprintf "Secuencia de venta inválida: %d" secuencia))
        else
            let codigo = sprintf "OV-%d%02d-%04d" fecha.Year fecha.Month secuencia
            Ok (CodigoVenta codigo)

[<RequireQualifiedAccess>]
type EstadoVenta =
    | Confirmada
    | Despachada
    | Anulada of Anulacion

module EstadoVenta =
    let aTexto = function
        | EstadoVenta.Confirmada -> "Confirmada"
        | EstadoVenta.Despachada -> "Despachada"
        | EstadoVenta.Anulada _  -> "Anulada"

    let desdeTexto (s: string) (anulacion: Anulacion option) : Result<EstadoVenta, DomainError> =
        match (if isNull s then "" else s.Trim().ToLowerInvariant()) with
        | "confirmada" -> Ok EstadoVenta.Confirmada
        | "despachada" -> Ok EstadoVenta.Despachada
        | "anulada" ->
            match anulacion with
            | Some a -> Ok (EstadoVenta.Anulada a)
            | None ->
                Ok (EstadoVenta.Anulada {
                    Fecha = DateTime.UtcNow
                    Motivo = "Anulación registrada"
                    AnuladoPor = EmpleadoId Guid.Empty
                })
        | otro -> Error (ValorRequerido (sprintf "Estado de venta desconocido: '%s'" otro))

type OrdenVenta =
    { Id: OrdenId
      Codigo: CodigoVenta
      ProformaOrigenId: ProformaId
      ClienteId: ClienteId
      Fecha: DateOnly
      ResponsableId: EmpleadoId
      Lineas: LineaCotizada list
      Total: decimal
      Moneda: string
      Estado: EstadoVenta
      CreadoEn: DateTime }

module OrdenVenta =

    /// Crea una OrdenVenta a partir de una Proforma vigente (RN14 / D4 / RNF05)
    /// Transforma la proforma a Convertida y retorna la tupla (OrdenVenta, ProformaConvertida).
    let crearDesdeProforma
        (hoy: DateOnly)
        (ordenId: OrdenId)
        (codigo: CodigoVenta)
        (proforma: Proforma)
        (clienteId: ClienteId)
        (responsableId: EmpleadoId)
        : Result<OrdenVenta * Proforma, DomainError> =

        // 1. Validar estado de la proforma
        match proforma.Estado with
        | EstadoProforma.Anulada ->
            Error (OperacionInvalida "No se puede confirmar una proforma que ya fue anulada")
        | EstadoProforma.Convertida ->
            Error (OperacionInvalida "La proforma ya fue convertida a orden de venta previamente")
        | EstadoProforma.Vencida ->
            Error (OperacionInvalida "La proforma se encuentra vencida")
        | EstadoProforma.Vigente ->
            // Verificar si proyectada a hoy ya venció
            match proforma.FechaVencimiento with
            | Some fv when hoy > fv ->
                Error (OperacionInvalida (sprintf "La proforma venció el %s y no puede confirmarse" (fv.ToString("yyyy-MM-dd"))))
            | _ ->

            // 2. Validar que tenga líneas
            if List.isEmpty proforma.Lineas then
                Error (ValorRequerido "La proforma no contiene líneas cotizadas para generar la venta")
            else

            // 3. Validar recálculo de total
            let totalRecalculado = Cotizacion.total proforma.Lineas
            if totalRecalculado <= 0m then
                Error (CantidadInvalida "El total de la orden de venta debe ser mayor a cero")
            else

            let ahora = DateTime.UtcNow
            let ordenVenta : OrdenVenta =
                { Id = ordenId
                  Codigo = codigo
                  ProformaOrigenId = proforma.Id
                  ClienteId = clienteId
                  Fecha = hoy
                  ResponsableId = responsableId
                  Lineas = proforma.Lineas
                  Total = totalRecalculado
                  Moneda = proforma.Moneda
                  Estado = EstadoVenta.Confirmada
                  CreadoEn = ahora }

            let proformaActualizada : Proforma =
                { proforma with
                    Estado = EstadoProforma.Convertida
                    OrdenVentaId = Some ordenId }

            Ok (ordenVenta, proformaActualizada)

    /// Marca la orden de venta como Despachada una vez ejecutada la salida física
    let marcarDespachada (orden: OrdenVenta) : Result<OrdenVenta, DomainError> =
        match orden.Estado with
        | EstadoVenta.Despachada ->
            Error (OperacionInvalida "La orden de venta ya fue despachada")
        | EstadoVenta.Anulada _ ->
            Error (OperacionInvalida "No se puede despachar una orden de venta anulada")
        | EstadoVenta.Confirmada ->
            Ok { orden with Estado = EstadoVenta.Despachada }

    /// Anula una orden de venta si aún no ha sido despachada físicamente
    let anular (motivo: string) (anuladoPor: EmpleadoId) (ahora: DateTime) (orden: OrdenVenta) : Result<OrdenVenta, DomainError> =
        match orden.Estado with
        | EstadoVenta.Despachada ->
            Error (OperacionInvalida "No se puede anular una orden de venta que ya ha sido despachada físicamente")
        | EstadoVenta.Anulada _ ->
            Error (OperacionInvalida "La orden de venta ya se encuentra anulada")
        | EstadoVenta.Confirmada ->
            if String.IsNullOrWhiteSpace motivo then
                Error (ValorRequerido "El motivo de anulación de la venta es obligatorio")
            else
                let anulacion : Anulacion =
                    { Fecha = ahora
                      Motivo = motivo.Trim()
                      AnuladoPor = anuladoPor }
                Ok { orden with Estado = EstadoVenta.Anulada anulacion }
