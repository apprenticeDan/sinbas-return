namespace Sinbas.Application

open System
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Fase 6a: Solicitud de Egreso Interno — Service Layer
// DTOs y lógica de aplicación (UsoInterno, Merma, Ajuste)
// ─────────────────────────────────────────────────────────────

[<CLIMutable>]
type SolicitudDto =
    { Id: string
      Tipo: string
      Destino: string option
      Signo: string
      ProductoId: string option
      NombreProducto: string option
      LoteId: string option
      CodigoLote: string option
      Cantidad: decimal
      Unidad: string
      Motivo: string
      SolicitanteId: string
      Estado: string
      AprobadorId: string option
      FechaResolucion: string option
      MotivoResolucion: string option
      EntregadoPor: string option
      FechaEntrega: string option
      CreadoEn: string }

[<CLIMutable>]
type CrearSolicitudRequest =
    { Tipo: string                // "UsoInterno" | "Ajuste" | "Merma"
      DestinoTipo: string option  // "Vivero" | "Laboratorio" | "Otro" (solo UsoInterno)
      DestinoOtro: string option  // solo si DestinoTipo = "Otro"
      Signo: string               // "Positivo" | "Negativo"
      ProductoId: string option
      LoteId: string option
      Cantidad: decimal
      Unidad: string
      Motivo: string }

[<CLIMutable>]
type AprobarSolicitudRequest = { Placeholder: string option }

[<CLIMutable>]
type RechazarSolicitudRequest =
    { Motivo: string }

[<CLIMutable>]
type ConfirmarSolicitudRequest = { Placeholder: string option }

module SolicitudService =

    let private errorToString (err: DomainError) : string =
        match err with
        | CantidadInvalida msg | UnidadIncompatible msg | CodigoLoteInvalido msg
        | NombreInvalido msg | CIInvalido msg | ValorRequerido msg | SecuenciaInvalida msg
        | StockInsuficiente msg | SimbolosNoPermitidos msg | LetrasNoPermitidas msg
        | EmpleadoDuplicado msg | SinAnalisisLaboratorio msg | LoteRechazado msg
        | PorcentajeInvalido msg | FechaInvalida msg | OperacionInvalida msg -> msg

    let private parsearTipo (tipoStr: string) (destinoTipo: string option) (destinoOtro: string option) : Result<TipoEgresoInterno, string> =
        let ts = if isNull tipoStr then "" else tipoStr.Trim()
        match ts with
        | "UsoInterno" ->
            let dt = destinoTipo |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim())) |> Option.defaultValue ""
            match dt with
            | "Vivero" -> Ok (TipoEgresoInterno.UsoInterno DestinoUso.Vivero)
            | "Laboratorio" -> Ok (TipoEgresoInterno.UsoInterno DestinoUso.Laboratorio)
            | "Otro" ->
                let otro = destinoOtro |> Option.bind (fun s -> if String.IsNullOrWhiteSpace s then None else Some (s.Trim()))
                match otro with
                | Some v -> Ok (TipoEgresoInterno.UsoInterno (DestinoUso.Otro v))
                | None -> Error "Si destino es 'Otro', debe indicarse en qué consiste (DestinoOtro)"
            | "" -> Error "UsoInterno requiere indicar DestinoTipo (Vivero, Laboratorio, Otro)"
            | _ -> Error (sprintf "DestinoTipo no válido: '%s'" dt)
        | "Ajuste" -> Ok TipoEgresoInterno.Ajuste
        | "Merma" -> Ok TipoEgresoInterno.Merma
        | "" -> Error "El tipo de solicitud es obligatorio"
        | _ -> Error (sprintf "Tipo no válido: '%s'. Valores permitidos: UsoInterno, Ajuste, Merma" ts)

    let private parsearSigno (s: string) : Result<SignoEgresoInterno, string> =
        match (if isNull s then "" else s.Trim()) with
        | "Positivo" -> Ok SignoEgresoInterno.Positivo
        | "Negativo" -> Ok SignoEgresoInterno.Negativo
        | "" -> Error "El signo es obligatorio (Positivo o Negativo)"
        | otro -> Error (sprintf "Signo no válido: '%s'. Valores permitidos: Positivo, Negativo" otro)

    let private solicitudToDto
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (sol: SolicitudEgresoInterno)
        : SolicitudDto =
        let (SolicitudEgresoInternoId sId) = sol.Id
        let (EmpleadoId empId) = sol.Solicitante
        let destinoStr =
            match sol.Tipo with
            | TipoEgresoInterno.UsoInterno dest -> Some (DestinoUso.aTexto dest)
            | _ -> None
        let (estadoStr, aprobadorId, fechaRes, motivoRes, entregadoPor, fechaEntrega) =
            match sol.Estado with
            | EstadoSolicitudInterna.Pendiente ->
                ("Pendiente", None, None, None, None, None)
            | EstadoSolicitudInterna.Aprobada res ->
                let (EmpleadoId aId) = res.Aprobador
                ("Aprobada", Some (aId.ToString()), Some (res.Fecha.ToString("o")), None, None, None)
            | EstadoSolicitudInterna.Rechazada res ->
                let (EmpleadoId aId) = res.Aprobador
                ("Rechazada", Some (aId.ToString()), Some (res.Fecha.ToString("o")), res.Motivo, None, None)
            | EstadoSolicitudInterna.Confirmada ent ->
                let (EmpleadoId eId) = ent.EntregadoPor
                ("Confirmada", None, None, None, Some (eId.ToString()), Some (ent.Fecha.ToString("o")))

        let tipoStr =
            match sol.Tipo with
            | TipoEgresoInterno.UsoInterno _ -> "UsoInterno"
            | TipoEgresoInterno.Ajuste -> "Ajuste"
            | TipoEgresoInterno.Merma -> "Merma"
        let signoStr =
            match sol.Signo with
            | SignoEgresoInterno.Positivo -> "Positivo"
            | SignoEgresoInterno.Negativo -> "Negativo"

        { Id = sId.ToString()
          Tipo = tipoStr
          Destino = destinoStr
          Signo = signoStr
          ProductoId = sol.ProductoId |> Option.map (fun (ProductoId g) -> g.ToString())
          NombreProducto = sol.ProductoId |> Option.map resolverNombreProducto
          LoteId = sol.LoteId |> Option.map (fun (LoteId g) -> g.ToString())
          CodigoLote = sol.LoteId |> Option.map resolverCodigoLote
          Cantidad = sol.Cantidad.Valor
          Unidad = UnidadMedida.aTexto sol.Cantidad.Unidad
          Motivo = sol.Motivo
          SolicitanteId = empId.ToString()
          Estado = estadoStr
          AprobadorId = aprobadorId
          FechaResolucion = fechaRes
          MotivoResolucion = motivoRes
          EntregadoPor = entregadoPor
          FechaEntrega = fechaEntrega
          CreadoEn = sol.CreadoEn.ToString("o") }

    // ─────────────────────────────────────────────────────────
    // crear: Crea solicitud de egreso interno
    // D1: UsoInterno reserva en Pendiente (al crear)
    // D2/D3: Merma y Ajuste no reservan al crear
    // ─────────────────────────────────────────────────────────

    let crear
        (insertarSolicitudConReserva: SolicitudEgresoInterno -> (LoteId * Cantidad) list -> Async<Result<unit, string>>)
        (obtenerLote: LoteId -> Async<Lote option>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (solicitanteId: Guid)
        (req: CrearSolicitudRequest)
        : Async<Result<SolicitudDto, string * int>> =
        async {
            // 1. Parsear tipo y signo
            match parsearTipo req.Tipo req.DestinoTipo req.DestinoOtro with
            | Error msg -> return Error (msg, 400)
            | Ok tipo ->
            match parsearSigno req.Signo with
            | Error msg -> return Error (msg, 400)
            | Ok signo ->

            // 2. Parsear unidad y cantidad
            match UnidadMedida.desdeTexto req.Unidad with
            | Error err -> return Error (errorToString err, 400)
            | Ok unidad ->
            match Cantidad.reconstruir req.Cantidad unidad with
            | Error err -> return Error (errorToString err, 400)
            | Ok cantidad ->

            // 3. Resolver producto/lote y determinar reservas iniciales
            let! lotesResolucion =
                async {
                    match req.LoteId with
                    | Some lIdStr when not (String.IsNullOrWhiteSpace lIdStr) ->
                        match Guid.TryParse(lIdStr) with
                        | false, _ -> return Error ("LoteId inválido", 400)
                        | true, lGuid ->
                            let lId = LoteId lGuid
                            let! loteOpt = obtenerLote lId
                            match loteOpt with
                            | None -> return Error ("Lote no encontrado", 404)
                            | Some lote ->
                                let res =
                                    match tipo with
                                    | TipoEgresoInterno.UsoInterno _ -> [ (lId, cantidad) ]
                                    | _ -> []
                                return Ok (Some lote.ProductoId, Some lId, res)
                    | _ ->
                        match req.ProductoId with
                        | Some pIdStr when not (String.IsNullOrWhiteSpace pIdStr) ->
                            match Guid.TryParse(pIdStr) with
                            | false, _ -> return Error ("ProductoId inválido", 400)
                            | true, pGuid ->
                                let pId = ProductoId pGuid
                                if signo = SignoEgresoInterno.Negativo then
                                    let! lotesActivos = listarLotes (Some pId) (Some Activo)
                                    match Fifo.resolverFIFO pId cantidad lotesActivos with
                                    | Error err -> return Error (errorToString err, 422)
                                    | Ok lineas ->
                                        if lineas.Length > 1 then
                                            return Error ("La cantidad requiere múltiples lotes; divida la solicitud en varias solicitudes de un solo lote cada una", 422)
                                        else
                                            match lineas with
                                            | [ l ] ->
                                                let res =
                                                    match tipo with
                                                    | TipoEgresoInterno.UsoInterno _ -> [ (l.Referencia, l.Cantidad) ]
                                                    | _ -> []
                                                return Ok (Some pId, Some l.Referencia, res)
                                            | _ ->
                                                return Error ("No se encontraron lotes para asignar", 422)
                                else
                                    if tipo = TipoEgresoInterno.Ajuste then
                                        return Error ("Un Ajuste Positivo requiere especificar el lote físico afectado", 400)
                                    else
                                        return Ok (Some pId, None, [])
                        | _ ->
                            return Ok (None, None, [])
                }

            match lotesResolucion with
            | Error err -> return Error err
            | Ok (productoIdOpt, loteIdOpt, reservas) ->

            // 4. Crear dominio y validar invariantes
            let solId = SolicitudEgresoInternoId (Identidad.nuevo ())
            let creado = DateTime.UtcNow
            let empId = EmpleadoId solicitanteId

            match SolicitudEgresoInterno.crear solId tipo signo productoIdOpt loteIdOpt cantidad req.Motivo empId creado with
            | Error err -> return Error (errorToString err, 422)
            | Ok solicitud ->

            // 5. Insertar solicitud y reservas (si UsoInterno)
            match! insertarSolicitudConReserva solicitud reservas with
            | Error msg -> return Error (msg, 500)
            | Ok () ->
                return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote solicitud)
        }

    // ─────────────────────────────────────────────────────────
    // aprobar: Aprueba solicitud de Merma/Ajuste (gerencia)
    // D2: Merma y Ajuste Negativo reservan en Aprobada
    // D3: Ajuste Positivo no reserva
    // ─────────────────────────────────────────────────────────

    let aprobar
        (obtener: SolicitudEgresoInternoId -> Async<SolicitudEgresoInterno option>)
        (actualizarEstadoConReserva: SolicitudEgresoInterno -> (LoteId * Cantidad) list -> Async<Result<unit, string>>)
        (listarLotes: ProductoId option -> EstadoLote option -> Async<Lote list>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (rolesAprobador: string list)
        (aprobadorId: Guid)
        (solicitudIdRaw: string)
        : Async<Result<SolicitudDto, string * int>> =
        async {
            match Guid.TryParse(solicitudIdRaw) with
            | false, _ -> return Error ("ID de solicitud inválido", 400)
            | true, sGuid ->
            let sId = SolicitudEgresoInternoId sGuid
            let! solOpt = obtener sId
            match solOpt with
            | None -> return Error ("Solicitud no encontrada", 404)
            | Some sol ->

            let esExento =
                rolesAprobador
                |> List.exists (fun r -> r = "Gerencia" || r = "Administrador")

            let aprobador = EmpleadoId aprobadorId
            match SolicitudEgresoInterno.aprobar aprobador esExento DateTime.UtcNow sol with
            | Error err -> return Error (errorToString err, 409)
            | Ok aprobada ->

            // Resolver reservas si es egreso negativo (Merma / Ajuste Negativo)
            if aprobada.Signo = SignoEgresoInterno.Negativo then
                match aprobada.LoteId with
                | Some lId ->
                    let reservas = [ (lId, aprobada.Cantidad) ]
                    match! actualizarEstadoConReserva aprobada reservas with
                    | Error msg -> return Error (msg, 500)
                    | Ok () ->
                        return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote aprobada)
                | None ->
                    match aprobada.ProductoId with
                    | Some pId ->
                        let! lotesActivos = listarLotes (Some pId) (Some Activo)
                        match Fifo.resolverFIFO pId aprobada.Cantidad lotesActivos with
                        | Error err -> return Error (errorToString err, 422)
                        | Ok lineas ->
                            if lineas.Length > 1 then
                                return Error ("La cantidad requiere múltiples lotes; divida la solicitud en varias solicitudes de un solo lote cada una", 422)
                            else
                                match lineas with
                                | [ l ] ->
                                    let aprobadaConLote = { aprobada with LoteId = Some l.Referencia }
                                    let reservas = [ (l.Referencia, l.Cantidad) ]
                                    match! actualizarEstadoConReserva aprobadaConLote reservas with
                                    | Error msg -> return Error (msg, 500)
                                    | Ok () ->
                                        return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote aprobadaConLote)
                                | _ ->
                                    return Error ("No se encontraron lotes para asignar", 422)
                    | None ->
                        return Error ("La solicitud no tiene lote ni producto asociado", 422)
            else
                // Ajuste Positivo: nunca reserva
                match! actualizarEstadoConReserva aprobada [] with
                | Error msg -> return Error (msg, 500)
                | Ok () ->
                    return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote aprobada)
        }

    // ─────────────────────────────────────────────────────────
    // rechazar: Rechaza solicitud (gerencia)
    // ─────────────────────────────────────────────────────────

    let rechazar
        (obtener: SolicitudEgresoInternoId -> Async<SolicitudEgresoInterno option>)
        (actualizarEstado: SolicitudEgresoInterno -> Async<Result<unit, string>>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (rechazadorId: Guid)
        (solicitudIdRaw: string)
        (req: RechazarSolicitudRequest)
        : Async<Result<SolicitudDto, string * int>> =
        async {
            match Guid.TryParse(solicitudIdRaw) with
            | false, _ -> return Error ("ID de solicitud inválido", 400)
            | true, sGuid ->
            let sId = SolicitudEgresoInternoId sGuid
            let! solOpt = obtener sId
            match solOpt with
            | None -> return Error ("Solicitud no encontrada", 404)
            | Some sol ->

            let rechazador = EmpleadoId rechazadorId
            match SolicitudEgresoInterno.rechazar rechazador req.Motivo DateTime.UtcNow sol with
            | Error err ->
                let status = match err with ValorRequerido _ -> 400 | _ -> 409
                return Error (errorToString err, status)
            | Ok rechazada ->

            // TODO FASE-7: si en el futuro se permite rechazar post-creación UsoInterno, liberar reservas.
            match! actualizarEstado rechazada with
            | Error msg -> return Error (msg, 500)
            | Ok () ->
                return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote rechazada)
        }

    // ─────────────────────────────────────────────────────────
    // confirmar: Confirma solicitud (almacén entrega física)
    // Consume reservas (negativo) o suma stock (positivo)
    // ─────────────────────────────────────────────────────────

    let confirmar
        (obtener: SolicitudEgresoInternoId -> Async<SolicitudEgresoInterno option>)
        (confirmarTx: SolicitudEgresoInterno -> MovimientoInventario -> (LoteId * Cantidad) list -> Async<Result<unit, string>>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (confirmadorId: Guid)
        (solicitudIdRaw: string)
        : Async<Result<SolicitudDto, string * int>> =
        async {
            match Guid.TryParse(solicitudIdRaw) with
            | false, _ -> return Error ("ID de solicitud inválido", 400)
            | true, sGuid ->
            let sId = SolicitudEgresoInternoId sGuid
            let! solOpt = obtener sId
            match solOpt with
            | None -> return Error ("Solicitud no encontrada", 404)
            | Some sol ->

            let confirmador = EmpleadoId confirmadorId
            match SolicitudEgresoInterno.confirmar confirmador DateTime.UtcNow sol with
            | Error err -> return Error (errorToString err, 409)
            | Ok confirmada ->

            let movId = MovimientoId (Identidad.nuevo ())
            let ahora = DateTime.UtcNow

            let tipoMov =
                if confirmada.Signo = SignoEgresoInterno.Positivo then
                    Entrada (MotivoIngreso.Recoleccion "Ajuste de inventario")
                else
                    match confirmada.Tipo with
                    | TipoEgresoInterno.UsoInterno dest -> Salida (MotivoEgreso.UsoInterno (dest, "Personal Interno"))
                    | TipoEgresoInterno.Merma -> Salida (MotivoEgreso.Merma confirmada.Motivo)
                    | TipoEgresoInterno.Ajuste -> Salida (MotivoEgreso.UsoInterno (DestinoUso.Otro "Ajuste", "Personal Interno"))

            let lineasMov =
                match confirmada.LoteId with
                | Some lId -> [ { Referencia = lId; Cantidad = confirmada.Cantidad } ]
                | None -> []

            let movimiento : MovimientoInventario =
                { Id = movId
                  Fecha = ahora
                  Responsable = confirmador
                  Tipo = tipoMov
                  OrdenOrigen = None
                  Lineas = lineasMov
                  Observaciones = Some confirmada.Motivo }

            let consumos =
                if confirmada.Signo = SignoEgresoInterno.Positivo then
                    []
                else
                    match confirmada.LoteId with
                    | Some lId -> [ (lId, confirmada.Cantidad) ]
                    | None -> []

            match! confirmarTx confirmada movimiento consumos with
            | Error msg -> return Error (msg, 500)
            | Ok () ->
                return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote confirmada)
        }

    // ─────────────────────────────────────────────────────────
    // listar: Lista solicitudes con filtros opcionales
    // ─────────────────────────────────────────────────────────

    let listar
        (listarDb: string option -> EmpleadoId option -> Async<SolicitudEgresoInterno list>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (estadoFilter: string option)
        (solicitanteFilter: EmpleadoId option)
        : Async<SolicitudDto list> =
        async {
            let! solicitudes = listarDb estadoFilter solicitanteFilter
            return solicitudes |> List.map (solicitudToDto resolverNombreProducto resolverCodigoLote)
        }

    // ─────────────────────────────────────────────────────────
    // obtenerPorId: Detalle de una solicitud
    // ─────────────────────────────────────────────────────────

    let obtenerPorId
        (obtener: SolicitudEgresoInternoId -> Async<SolicitudEgresoInterno option>)
        (resolverNombreProducto: ProductoId -> string)
        (resolverCodigoLote: LoteId -> string)
        (solicitudIdRaw: string)
        : Async<Result<SolicitudDto, string * int>> =
        async {
            match Guid.TryParse(solicitudIdRaw) with
            | false, _ -> return Error ("ID de solicitud inválido", 400)
            | true, sGuid ->
            let sId = SolicitudEgresoInternoId sGuid
            let! solOpt = obtener sId
            match solOpt with
            | None -> return Error ("Solicitud no encontrada", 404)
            | Some sol ->
                return Ok (solicitudToDto resolverNombreProducto resolverCodigoLote sol)
        }
