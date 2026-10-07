namespace Sinbas.Domain

open System

type EstadoLote =
    | Activo
    | EnCuarentena
    | Agotado
    | Bloqueado
    | Rechazado
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

type Lote =
    { Id: LoteId
      Codigo: CodigoLote
      ProductoId: ProductoId
      Procedencia: string option
      CantidadInicial: Cantidad
      CantidadActual: Cantidad
      FechaIngreso: DateOnly
      Ubicacion: string option
      Estado: EstadoLote
      Observaciones: string option }

module Lote =

    let estaActivo lote = lote.Estado = Activo

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
                  FechaIngreso = fechaIngreso
                  Ubicacion = ubicacion
                  Estado = Activo
                  Observaciones = observaciones }

    /// Actualiza el saldo proyectado del lote (en unidad base) y reevalúa su estado si llega a cero,
    /// preservando la unidad física declarada en el lote.
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
                    if saldoNormalizado = 0m then Agotado
                    elif lote.Estado = Agotado && saldoNormalizado > 0m then Activo
                    else lote.Estado

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

    let bloquear motivo lote =
        let observaciones =
            match lote.Observaciones with
            | None -> Some(sprintf "[BLOQUEADO]: %s" motivo)
            | Some prev -> Some(sprintf "%s | [BLOQUEADO]: %s" prev motivo)

        { lote with
            Estado = Bloqueado
            Observaciones = observaciones }

    let archivar lote = { lote with Estado = Archivado }

    /// RN12: Aplica el resultado del análisis técnico de laboratorio al lote.
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
            | Rechazado -> lote
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
