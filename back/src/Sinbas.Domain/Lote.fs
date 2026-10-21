namespace Sinbas.Domain

open System

/// Estados del ciclo de vida de un lote de germoplasma / semillas (RF06 / RN10 / RN12 / OBS-12).
type EstadoLote =
    /// Lote certificado con calidad conforme y saldo disponible (> 0).
    /// Habilitado para proformas, ventas y despacho FIFO comercial.
    | Activo
    /// Lote en espera de análisis técnico inicial o con parámetros observados.
    /// Físicamente en depósito pero aislado comercialmente (no cotizable ni vendible).
    | EnCuarentena
    /// Saldo disponible consumido en su totalidad (= 0).
    /// Puede reactivarse si ocurre una regularización por ajuste positivo o devolución a almacén.
    | Agotado
    /// Retención preventiva temporal por reanálisis periódico, sospecha fitosanitaria
    /// o instrucción administrativa. Excluido de operaciones comerciales y FIFO.
    | Bloqueado
    /// Declarado no apto de forma definitiva para venta por resolución gerencial.
    /// Destinado a descarte, baja o compostaje (no reactivable a Activo).
    | Rechazado
    /// Histórico cerrado sin saldo activo (o conservado como muestra testigo).
    /// Inmutable para auditoría y trazabilidad legal (INIAF/SENASAG).
    | Archivado

module EstadoLote =

    let aTexto = function
        | Activo -> "Activo"
        | EnCuarentena -> "EnCuarentena"
        | Agotado -> "Agotado"
        | Bloqueado -> "Bloqueado"
        | Rechazado -> "Rechazado"
        | Archivado -> "Archivado"

    let desdeTexto (texto: string) : Result<EstadoLote, DomainError> =
        match (if isNull texto then "" else texto.Trim()) with
        | "Activo" -> Ok Activo
        | "EnCuarentena" -> Ok EnCuarentena
        | "Agotado" -> Ok Agotado
        | "Bloqueado" -> Ok Bloqueado
        | "Rechazado" -> Ok Rechazado
        | "Archivado" -> Ok Archivado
        | desconocido -> Error (ValorRequerido (sprintf "Estado de lote desconocido o inválido: '%s'" desconocido))

    /// Indica si el estado permite cotizaciones, asignación de reservas y ventas comerciales (RN04/RN10).
    let esVendible = function
        | Activo -> true
        | EnCuarentena
        | Agotado
        | Bloqueado
        | Rechazado
        | Archivado -> false

    /// Lista de transiciones permitidas según el grafo formal de estados del dominio (OBS-12).
    let transicionesValidas = function
        | EnCuarentena -> [ Activo; Rechazado; Bloqueado ]
        | Activo -> [ Bloqueado; EnCuarentena; Agotado; Archivado ]
        | Bloqueado -> [ Activo; Agotado; Rechazado; EnCuarentena ]
        | Rechazado -> [ Archivado ]
        | Agotado -> [ Activo; Archivado ]
        | Archivado -> [ ]

    /// Valida si una transición entre estados es legal según la máquina de estados.
    let puedeTransicionar (origen: EstadoLote) (destino: EstadoLote) : bool =
        if origen = destino then true
        else transicionesValidas origen |> List.contains destino

type Lote =
    { Id: LoteId
      Codigo: CodigoLote
      ProductoId: ProductoId
      Procedencia: string option
      CantidadInicial: Cantidad
      CantidadActual: Cantidad
      CantidadReservada: Cantidad
      FechaIngreso: DateOnly
      Ubicacion: string option
      Estado: EstadoLote
      Observaciones: string option }

module Lote =

    let estaActivo lote = lote.Estado = Activo

    /// Crea una entidad Lote con un estado inicial explícito (OBS-12).
    let crearConEstado
        (id: LoteId)
        (codigo: CodigoLote)
        (productoId: ProductoId)
        (procedencia: string option)
        (cantidadInicial: Cantidad)
        (fechaIngreso: DateOnly)
        (ubicacion: string option)
        (estadoInicial: EstadoLote)
        (observaciones: string option)
        : Result<Lote, DomainError> =
        if cantidadInicial.Valor <= 0m then
            Error(CantidadInvalida "La cantidad inicial de ingreso al lote debe ser mayor a cero")
        else
            Ok
                { Id = id
                  Codigo = codigo
                  ProductoId = productoId
                  Procedencia = procedencia
                  CantidadInicial = cantidadInicial
                  CantidadActual = cantidadInicial
                  CantidadReservada = Cantidad.cero cantidadInicial.Unidad
                  FechaIngreso = fechaIngreso
                  Ubicacion = ubicacion
                  Estado = estadoInicial
                  Observaciones = observaciones }

    /// Crea un lote listo para comercialización (Activo) (preserva compatibilidad operativa).
    let crear
        (id: LoteId)
        (codigo: CodigoLote)
        (productoId: ProductoId)
        (procedencia: string option)
        (cantidadInicial: Cantidad)
        (fechaIngreso: DateOnly)
        (ubicacion: string option)
        (observaciones: string option)
        : Result<Lote, DomainError> =
        crearConEstado id codigo productoId procedencia cantidadInicial fechaIngreso ubicacion Activo observaciones

    /// Crea un lote en recepción inicial de almacén, en cuarentena técnica pendiente de laboratorio (OBS-12).
    let crearEnCuarentena
        (id: LoteId)
        (codigo: CodigoLote)
        (productoId: ProductoId)
        (procedencia: string option)
        (cantidadInicial: Cantidad)
        (fechaIngreso: DateOnly)
        (ubicacion: string option)
        (observaciones: string option)
        : Result<Lote, DomainError> =
        crearConEstado id codigo productoId procedencia cantidadInicial fechaIngreso ubicacion EnCuarentena observaciones

    /// Evalúa si el lote puede cambiar al estado objetivo según las reglas de dominio (OBS-12).
    let puedeTransicionarA (nuevoEstado: EstadoLote) (lote: Lote) : bool =
        EstadoLote.puedeTransicionar lote.Estado nuevoEstado

    /// Stock disponible del lote en su unidad base (Físico - Reservado) (OBS-03)
    let stockDisponibleBase (lote: Lote) : decimal =
        if lote.Estado = Activo then
            let fisicoBase = Cantidad.aUnidadBase lote.CantidadActual
            let reservadoBase = Cantidad.aUnidadBase lote.CantidadReservada
            max 0m (fisicoBase - reservadoBase)
        else
            0m

    /// Reserva stock formal sobre el lote para una venta confirmada (OBS-03)
    let reservar (cantidadAReservar: Cantidad) (lote: Lote) : Result<Lote, DomainError> =
        if cantidadAReservar.Valor <= 0m then
            Error (CantidadInvalida "La cantidad a reservar debe ser mayor a cero")
        else
            let dispBase = stockDisponibleBase lote
            let reqBase = Cantidad.aUnidadBase cantidadAReservar
            if reqBase > dispBase then
                Error (
                    StockInsuficiente (
                        sprintf "Stock disponible insuficiente para reservar en lote '%s'. Disponible: %.2f, Solicitado: %.2f"
                            (CodigoLote.valor lote.Codigo) dispBase reqBase
                    )
                )
            else
                let targetUnit = lote.CantidadReservada.Unidad
                let uBase = UnidadMedida.unidadBase targetUnit
                let nuevaReservaBase = (Cantidad.aUnidadBase lote.CantidadReservada) + reqBase
                match UnidadMedida.convertir uBase targetUnit nuevaReservaBase with
                | Error err -> Error err
                | Ok valEnUnidad ->
                    match Cantidad.reconstruir valEnUnidad targetUnit with
                    | Error err -> Error err
                    | Ok nuevaReservada ->
                        Ok { lote with CantidadReservada = nuevaReservada }

    /// Descuenta simultáneamente el stock físico y la reserva al ejecutar despacho físico en almacén (OBS-03)
    let despacharReserva (cantidadADespachar: Cantidad) (lote: Lote) : Result<Lote, DomainError> =
        if cantidadADespachar.Valor <= 0m then
            Error (CantidadInvalida "La cantidad a despachar debe ser mayor a cero")
        else
            let reqBase = Cantidad.aUnidadBase cantidadADespachar
            let saldoBase = Cantidad.aUnidadBase lote.CantidadActual
            let reservadaBase = Cantidad.aUnidadBase lote.CantidadReservada
            let nuevoSaldoBase = max 0m (saldoBase - reqBase)
            let nuevaReservaBase = max 0m (reservadaBase - reqBase)

            let targetUnit = lote.CantidadActual.Unidad
            let uBase = UnidadMedida.unidadBase targetUnit
            match UnidadMedida.convertir uBase targetUnit nuevoSaldoBase, UnidadMedida.convertir uBase targetUnit nuevaReservaBase with
            | Ok valSaldo, Ok valReserva ->
                match Cantidad.reconstruir valSaldo targetUnit, Cantidad.reconstruir valReserva targetUnit with
                | Ok nuevaCantActual, Ok nuevaCantReservada ->
                    let nuevoEstado =
                        match lote.Estado with
                        | Activo when nuevoSaldoBase <= 0m -> Agotado
                        | otro -> otro
                    Ok { lote with CantidadActual = nuevaCantActual; CantidadReservada = nuevaCantReservada; Estado = nuevoEstado }
                | Error err, _ | _, Error err -> Error err
            | Error err, _ | _, Error err -> Error err

    /// Libera stock reservado cuando una orden de venta es anulada antes de su despacho
    let liberarReserva (cantidadALiberar: Cantidad) (lote: Lote) : Result<Lote, DomainError> =
        if cantidadALiberar.Valor <= 0m then
            Error (CantidadInvalida "La cantidad a liberar debe ser mayor a cero")
        else
            let libBase = Cantidad.aUnidadBase cantidadALiberar
            let reservadaBase = Cantidad.aUnidadBase lote.CantidadReservada
            let nuevaReservaBase = max 0m (reservadaBase - libBase)
            let targetUnit = lote.CantidadReservada.Unidad
            let uBase = UnidadMedida.unidadBase targetUnit
            match UnidadMedida.convertir uBase targetUnit nuevaReservaBase with
            | Error err -> Error err
            | Ok valReserva ->
                match Cantidad.reconstruir valReserva targetUnit with
                | Error err -> Error err
                | Ok nuevaCantReservada ->
                    Ok { lote with CantidadReservada = nuevaCantReservada }

    /// Actualiza el saldo proyectado del lote (en unidad base) y reevalúa su estado si llega a cero,
    /// preservando la unidad física declarada en el lote y respetando estados protegidos (OBS-12).
    let actualizarSaldo (nuevoSaldoBase: decimal) (lote: Lote) : Result<Lote, DomainError> =
        let saldoNormalizado = max 0m nuevoSaldoBase
        let targetUnit = lote.CantidadInicial.Unidad
        let uBase = UnidadMedida.unidadBase targetUnit
        match UnidadMedida.convertir uBase targetUnit saldoNormalizado with
        | Error err -> Error err
        | Ok valorEnUnidadLote ->
            match Cantidad.reconstruir valorEnUnidadLote targetUnit with
            | Error err -> Error err
            | Ok nuevaCantidad ->
                let nuevoEstado =
                    match lote.Estado with
                    | Activo when saldoNormalizado = 0m -> Agotado
                    | Agotado when saldoNormalizado > 0m -> Activo
                    // Estados congelados o terminales (Bloqueado, Rechazado, Archivado, EnCuarentena)
                    // actualizan su saldo pero no alteran su estado comercial sin acción formal:
                    | otro -> otro

                Ok { lote with
                        CantidadActual = nuevaCantidad
                        Estado = nuevoEstado }

    let descontarStock (cantidadADescontar: Cantidad) (lote: Lote) : Result<Lote, DomainError> =
        if cantidadADescontar.Valor <= 0m then
            Error (CantidadInvalida (sprintf "La cantidad a descontar debe ser mayor a cero, recibido: %M" cantidadADescontar.Valor))
        else
            match Cantidad.esSuficiente lote.CantidadActual cantidadADescontar with
            | Error err -> Error err
            | Ok false ->
                Error(
                    StockInsuficiente(
                        sprintf
                            "Stock insuficiente en lote '%s'. Disponible: %s, Solicitado: %s"
                            (CodigoLote.valor lote.Codigo)
                            (Cantidad.formatear lote.CantidadActual)
                            (Cantidad.formatear cantidadADescontar)
                    )
                )
            | Ok true ->
                let disponibleBase = Cantidad.aUnidadBase lote.CantidadActual
                let aDescontarBase = Cantidad.aUnidadBase cantidadADescontar
                let restanteBase = disponibleBase - aDescontarBase
                actualizarSaldo restanteBase lote

    let marcarAgotado lote =
        actualizarSaldo 0m lote

    /// Bloquea temporalmente un lote por sospecha técnica, alerta fitosanitaria o reanálisis (OBS-12).
    let bloquear (motivo: string) (lote: Lote) : Result<Lote, DomainError> =
        if String.IsNullOrWhiteSpace motivo then
            Error (ValorRequerido "Se requiere un motivo no vacío para bloquear el lote")
        else
            match lote.Estado with
            | Archivado ->
                Error (OperacionInvalida "No se puede bloquear un lote archivado")
            | Rechazado ->
                Error (OperacionInvalida "No se puede bloquear un lote rechazado definitivamente")
            | Bloqueado ->
                Error (OperacionInvalida "El lote ya se encuentra en estado Bloqueado")
            | _ ->
                let nota = sprintf "[BLOQUEADO]: %s" (motivo.Trim())
                let obsActualizada =
                    match lote.Observaciones with
                    | None -> Some nota
                    | Some prev -> Some (sprintf "%s | %s" prev nota)

                Ok { lote with
                        Estado = Bloqueado
                        Observaciones = obsActualizada }

    /// Desbloquea un lote levantando la retención preventiva tras verificación técnica (OBS-12).
    let desbloquear (justificacion: string) (lote: Lote) : Result<Lote, DomainError> =
        if String.IsNullOrWhiteSpace justificacion then
            Error (ValorRequerido "Se requiere una justificación no vacía para levantar el bloqueo del lote")
        else
            match lote.Estado with
            | Bloqueado ->
                let nota = sprintf "[DESBLOQUEADO]: %s" (justificacion.Trim())
                let obsActualizada =
                    match lote.Observaciones with
                    | None -> Some nota
                    | Some prev -> Some (sprintf "%s | %s" prev nota)

                let targetEstado =
                    if (Cantidad.aUnidadBase lote.CantidadActual) > 0m then Activo
                    else Agotado

                Ok { lote with
                        Estado = targetEstado
                        Observaciones = obsActualizada }
            | otro ->
                Error (OperacionInvalida (sprintf "Solo un lote en estado 'Bloqueado' puede ser desbloqueado, estado actual: %A" otro))

    /// Archiva un lote para cierre histórico o retiro a colección testigo (OBS-12).
    let archivar (justificacionOpt: string option) (lote: Lote) : Result<Lote, DomainError> =
        match lote.Estado with
        | Archivado ->
            Error (OperacionInvalida "El lote ya se encuentra archivado")
        | EnCuarentena
        | Bloqueado ->
            Error (OperacionInvalida (sprintf "No se puede archivar un lote en estado '%A'; debe resolverse su situación técnica primero" lote.Estado))
        | _ ->
            let tieneReservas = (Cantidad.aUnidadBase lote.CantidadReservada) > 0m
            if tieneReservas then
                Error (OperacionInvalida "No se puede archivar un lote con reservas de venta activas")
            else
                let tieneSaldo = (Cantidad.aUnidadBase lote.CantidadActual) > 0m
                match lote.Estado, tieneSaldo with
                | Activo, true ->
                    match justificacionOpt with
                    | None ->
                        Error (OperacionInvalida "No se puede archivar un lote activo con saldo disponible sin justificación formal (ej. retiro a muestra testigo)")
                    | Some j when String.IsNullOrWhiteSpace j ->
                        Error (OperacionInvalida "No se puede archivar un lote activo con saldo disponible sin justificación formal (ej. retiro a muestra testigo)")
                    | Some j ->
                        let nota = sprintf "[ARCHIVADO (Muestra Testigo)]: %s" (j.Trim())
                        let obs =
                            match lote.Observaciones with
                            | None -> Some nota
                            | Some prev -> Some (sprintf "%s | %s" prev nota)
                        Ok { lote with Estado = Archivado; Observaciones = obs }
                | _ ->
                    let obs =
                        match justificacionOpt with
                        | Some j when not (String.IsNullOrWhiteSpace j) ->
                            let nota = sprintf "[ARCHIVADO]: %s" (j.Trim())
                            match lote.Observaciones with
                            | None -> Some nota
                            | Some prev -> Some (sprintf "%s | %s" prev nota)
                        | _ -> lote.Observaciones
                    Ok { lote with Estado = Archivado; Observaciones = obs }

    /// RN12 / OBS-12: Aplica el resultado del análisis técnico de laboratorio al lote.
    /// - DictamenCalidad.Aprobado: Si el lote no estaba en cuarentena ni rechazado, se confirma como Activo.
    ///   Si el lote ya estaba EnCuarentena o Rechazado, el dictamen NO altera el estado comercial
    ///   automáticamente; se preserva en su estado para requerir la decisión explícita de Gerencia.
    /// - DictamenCalidad.Observado: El lote pasa a EnCuarentena (salvo que ya esté Rechazado por Gerencia).
    let aplicarDictamenLaboratorio (dictamen: DictamenCalidad) (lote: Lote) : Lote =
        match dictamen with
        | DictamenCalidad.Aprobado ->
            match lote.Estado with
            | EnCuarentena
            | Rechazado -> lote
            | _ -> { lote with Estado = Activo }
        | DictamenCalidad.Observado _ ->
            match lote.Estado with
            | Rechazado
            | Archivado -> lote
            | _ -> { lote with Estado = EnCuarentena }

    /// Acción de Gerencia: Levanta la cuarentena de un lote tras evaluar los análisis y contraensayos.
    let liberarCuarentena (justificacion: string) (gerente: EmpleadoId) (lote: Lote) : Result<Lote, DomainError> =
        if String.IsNullOrWhiteSpace justificacion then
            Error (ValorRequerido "Se requiere una justificación formal para levantar la cuarentena del lote")
        else
            match lote.Estado with
            | EnCuarentena ->
                let (EmpleadoId gId) = gerente
                let nota = sprintf "[CUARENTENA LEVANTADA por %s]: %s" (gId.ToString()) (justificacion.Trim())
                let obsActualizada =
                    match lote.Observaciones with
                    | None -> Some nota
                    | Some prev -> Some (sprintf "%s | %s" prev nota)
                Ok { lote with Estado = Activo; Observaciones = obsActualizada }
            | otro ->
                Error (OperacionInvalida (sprintf "Solo un lote en estado 'EnCuarentena' puede ser liberado, estado actual: %A" otro))

    /// Acción de Gerencia: Rechaza definitivamente un lote en cuarentena o bloqueado.
    let rechazarDefinitivamente (motivo: string) (gerente: EmpleadoId) (lote: Lote) : Result<Lote, DomainError> =
        if String.IsNullOrWhiteSpace motivo then
            Error (ValorRequerido "Se requiere un motivo para rechazar definitivamente el lote")
        else
            match lote.Estado with
            | EnCuarentena | Bloqueado ->
                let (EmpleadoId gId) = gerente
                let nota = sprintf "[RECHAZADO DEFINITIVAMENTE por %s]: %s" (gId.ToString()) (motivo.Trim())
                let obsActualizada =
                    match lote.Observaciones with
                    | None -> Some nota
                    | Some prev -> Some (sprintf "%s | %s" prev nota)
                Ok { lote with Estado = Rechazado; Observaciones = obsActualizada }
            | Rechazado ->
                Error (OperacionInvalida "El lote ya se encuentra en estado Rechazado")
            | otro ->
                Error (OperacionInvalida (sprintf "Solo un lote en cuarentena o bloqueado puede ser rechazado definitivamente, estado actual: %A" otro))

    /// Acción de Gerencia: Solicita formalmente un nuevo análisis técnico (contraensayo) para un lote en cuarentena.
    let solicitarNuevoAnalisis (instruccion: string) (gerente: EmpleadoId) (lote: Lote) : Result<Lote, DomainError> =
        if String.IsNullOrWhiteSpace instruccion then
            Error (ValorRequerido "Se requiere una instrucción o motivo para solicitar un nuevo análisis")
        else
            match lote.Estado with
            | EnCuarentena ->
                let (EmpleadoId gId) = gerente
                let nota = sprintf "[REANÁLISIS SOLICITADO por %s]: %s" (gId.ToString()) (instruccion.Trim())
                let obsActualizada =
                    match lote.Observaciones with
                    | None -> Some nota
                    | Some prev -> Some (sprintf "%s | %s" prev nota)
                Ok { lote with Observaciones = obsActualizada }
            | otro ->
                Error (OperacionInvalida (sprintf "Solo se puede solicitar reanálisis para un lote en cuarentena, estado actual: %A" otro))
