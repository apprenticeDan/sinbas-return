namespace Sinbas.Infrastructure

open System
open Dapper
open Npgsql
open Sinbas.Domain

[<CLIMutable>]
type private SolicitudRow =
    { id: Guid
      tipo: string
      destino_uso_tipo: string
      destino_uso_otro: string
      signo: string
      producto_id: Nullable<Guid>
      lote_id: Nullable<Guid>
      cantidad: decimal
      unidad: string
      motivo: string
      solicitante_id: Guid
      estado: string
      resolucion_aprobador_id: Nullable<Guid>
      resolucion_fecha: Nullable<DateTime>
      resolucion_motivo: string
      entregado_por: Nullable<Guid>
      fecha_entrega: Nullable<DateTime>
      creado_en: DateTime }

module SolicitudRepository =

    // TODO FASE-7: unificar el flujo de despacho físico para VENTA y SOLICITUD
    // para eliminar la duplicación entre SolicitudService y VentaRepository.

    let private mapTipo (tipoStr: string) (destinoTipo: string) (destinoOtro: string) : Result<TipoEgresoInterno, string> =
        match tipoStr with
        | "UsoInterno" ->
            match destinoTipo with
            | "Vivero" -> Ok (TipoEgresoInterno.UsoInterno DestinoUso.Vivero)
            | "Laboratorio" -> Ok (TipoEgresoInterno.UsoInterno DestinoUso.Laboratorio)
            | "Otro" ->
                let s = if isNull destinoOtro then "" else destinoOtro.Trim()
                if String.IsNullOrWhiteSpace s then
                    Error "UsoInterno con destino 'Otro' pero destino_uso_otro vacío"
                else
                    Ok (TipoEgresoInterno.UsoInterno (DestinoUso.Otro s))
            | otro -> Error (sprintf "destino_uso_tipo desconocido: '%s'" otro)
        | "Ajuste" -> Ok TipoEgresoInterno.Ajuste
        | "Merma" -> Ok TipoEgresoInterno.Merma
        | otro -> Error (sprintf "tipo desconocido: '%s'" otro)

    let private mapSigno (s: string) : Result<SignoEgresoInterno, string> =
        match s with
        | "Positivo" -> Ok SignoEgresoInterno.Positivo
        | "Negativo" -> Ok SignoEgresoInterno.Negativo
        | otro -> Error (sprintf "signo desconocido: '%s'" otro)

    let private mapEstado
        (estadoStr: string)
        (resolucionAprobador: Nullable<Guid>)
        (resolucionFecha: Nullable<DateTime>)
        (resolucionMotivo: string)
        (entregadoPor: Nullable<Guid>)
        (fechaEntrega: Nullable<DateTime>)
        : Result<EstadoSolicitudInterna, string> =
        match estadoStr with
        | "Pendiente" -> Ok EstadoSolicitudInterna.Pendiente
        | "Aprobada" ->
            if resolucionAprobador.HasValue && resolucionFecha.HasValue then
                let res = { Aprobador = EmpleadoId resolucionAprobador.Value
                            Fecha = resolucionFecha.Value
                            Motivo = None }
                Ok (EstadoSolicitudInterna.Aprobada res)
            else
                Error "Aprobada sin resolucion_aprobador_id o resolucion_fecha"
        | "Rechazada" ->
            if resolucionAprobador.HasValue && resolucionFecha.HasValue then
                let motivo = if isNull resolucionMotivo then "" else resolucionMotivo
                let res = { Aprobador = EmpleadoId resolucionAprobador.Value
                            Fecha = resolucionFecha.Value
                            Motivo = if String.IsNullOrWhiteSpace motivo then None else Some motivo }
                Ok (EstadoSolicitudInterna.Rechazada res)
            else
                Error "Rechazada sin resolucion_aprobador_id o resolucion_fecha"
        | "Confirmada" ->
            if entregadoPor.HasValue && fechaEntrega.HasValue then
                let ent = { EntregadoPor = EmpleadoId entregadoPor.Value
                            Fecha = fechaEntrega.Value }
                Ok (EstadoSolicitudInterna.Confirmada ent)
            else
                Error "Confirmada sin entregado_por o fecha_entrega"
        | otro -> Error (sprintf "estado desconocido: '%s'" otro)

    let private fromRow (row: SolicitudRow) : Result<SolicitudEgresoInterno, string> =
        match mapTipo row.tipo row.destino_uso_tipo row.destino_uso_otro with
        | Error msg -> Error msg
        | Ok tipo ->
        match mapSigno row.signo with
        | Error msg -> Error msg
        | Ok signo ->
        match UnidadMedida.desdeTexto row.unidad with
        | Error err -> Error (sprintf "unidad inválida: %A" err)
        | Ok unidad ->
        match Cantidad.reconstruir row.cantidad unidad with
        | Error err -> Error (sprintf "cantidad inválida: %A" err)
        | Ok cantidad ->
        match mapEstado row.estado row.resolucion_aprobador_id row.resolucion_fecha row.resolucion_motivo row.entregado_por row.fecha_entrega with
        | Error msg -> Error msg
        | Ok estado ->
        let productoIdOpt = if row.producto_id.HasValue then Some (ProductoId row.producto_id.Value) else None
        let loteIdOpt = if row.lote_id.HasValue then Some (LoteId row.lote_id.Value) else None
        Ok {
            Id = SolicitudEgresoInternoId row.id
            Tipo = tipo
            Signo = signo
            ProductoId = productoIdOpt
            LoteId = loteIdOpt
            Cantidad = cantidad
            Motivo = row.motivo
            Solicitante = EmpleadoId row.solicitante_id
            Estado = estado
            CreadoEn = row.creado_en
        }

    let private tipoToRow (tipo: TipoEgresoInterno) : (string * string * string) =
        match tipo with
        | TipoEgresoInterno.UsoInterno DestinoUso.Vivero -> ("UsoInterno", "Vivero", null)
        | TipoEgresoInterno.UsoInterno DestinoUso.Laboratorio -> ("UsoInterno", "Laboratorio", null)
        | TipoEgresoInterno.UsoInterno (DestinoUso.Otro s) -> ("UsoInterno", "Otro", s)
        | TipoEgresoInterno.Ajuste -> ("Ajuste", null, null)
        | TipoEgresoInterno.Merma -> ("Merma", null, null)

    let private estadoToRow (estado: EstadoSolicitudInterna) =
        match estado with
        | EstadoSolicitudInterna.Pendiente ->
            ("Pendiente", box DBNull.Value, box DBNull.Value, null, box DBNull.Value, box DBNull.Value)
        | EstadoSolicitudInterna.Aprobada res ->
            let (EmpleadoId aId) = res.Aprobador
            ("Aprobada", box aId, box res.Fecha, null, box DBNull.Value, box DBNull.Value)
        | EstadoSolicitudInterna.Rechazada res ->
            let (EmpleadoId aId) = res.Aprobador
            let motivo = res.Motivo |> Option.defaultValue ""
            ("Rechazada", box aId, box res.Fecha, motivo, box DBNull.Value, box DBNull.Value)
        | EstadoSolicitudInterna.Confirmada ent ->
            let (EmpleadoId eId) = ent.EntregadoPor
            ("Confirmada", box DBNull.Value, box DBNull.Value, null, box eId, box ent.Fecha)

    let private reservarLotesTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (solicitudId: SolicitudEgresoInternoId)
        (reservas: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            if List.isEmpty reservas then
                return Ok ()
            else
                let loteIds = reservas |> List.map fst
                let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                match lotesLockedRes with
                | Error msg -> return Error msg
                | Ok lotesLocked ->
                    let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList
                    let rec reservarTodo = function
                        | [] -> Ok []
                        | ((loteId, cantidad) :: rest) ->
                            match Map.tryFind loteId lotesPorId with
                            | None -> Error (sprintf "Lote %A no encontrado tras lock" loteId)
                            | Some lf ->
                                match Lote.reservar cantidad lf with
                                | Error err -> Error (sprintf "Reserva falló en lote %s: %A" (CodigoLote.valor lf.Codigo) err)
                                | Ok lr ->
                                    match reservarTodo rest with
                                    | Error e -> Error e
                                    | Ok acc -> Ok (lr :: acc)

                    match reservarTodo reservas with
                    | Error msg -> return Error msg
                    | Ok lotesReservados ->
                        let rec persistirLotes = function
                            | [] -> async { return Ok () }
                            | l :: rest ->
                                async {
                                    let! r = LoteRepository.actualizarStockYEstadoTx conn tx l
                                    match r with
                                    | Ok () -> return! persistirLotes rest
                                    | Error msg -> return Error msg
                                }
                        let! persistRes = persistirLotes lotesReservados
                        match persistRes with
                        | Error msg -> return Error msg
                        | Ok () ->
                            return! ReservaStockRepository.insertarBatchSolicitud conn tx solicitudId reservas
        }

    let private actualizarLotesTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (solicitud: SolicitudEgresoInterno)
        : Async<Result<unit, string>> =
        async {
            if solicitud.Signo = SignoEgresoInterno.Positivo then
                match solicitud.LoteId with
                | None -> return Ok ()
                | Some loteId ->
                    let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx [ loteId ]
                    match lotesLockedRes with
                    | Error msg -> return Error msg
                    | Ok [] -> return Error "Lote para ajuste no encontrado"
                    | Ok [ lote ] ->
                        match Lote.actualizarSaldo solicitud.Cantidad.Valor lote with
                        | Error err -> return Error (sprintf "Error sumando stock al lote: %A" err)
                        | Ok loteActualizado ->
                            return! LoteRepository.actualizarStockYEstadoTx conn tx loteActualizado
                    | Ok _ -> return Error "Múltiples lotes encontrados para un ID"
            else
                let! reservasRes = ReservaStockRepository.listarActivasPorSolicitud conn tx solicitud.Id
                match reservasRes with
                | Error msg -> return Error msg
                | Ok reservasActivas ->
                    let loteIds = reservasActivas |> List.map fst
                    let! lotesLockedRes = LoteRepository.obtenerMultiplesPorIdParaActualizacion conn tx loteIds
                    match lotesLockedRes with
                    | Error msg -> return Error msg
                    | Ok lotesLocked ->
                        let lotesPorId = lotesLocked |> List.map (fun l -> (l.Id, l)) |> Map.ofList
                        let rec despacharTodo = function
                            | [] -> Ok []
                            | ((loteId, cant) :: rest) ->
                                match Map.tryFind loteId lotesPorId with
                                | None -> Error (sprintf "Lote %A no encontrado" loteId)
                                | Some lf ->
                                    match Lote.despacharReserva cant lf with
                                    | Error err -> Error (sprintf "despacharReserva falló: %A" err)
                                    | Ok ld ->
                                        match despacharTodo rest with
                                        | Error e -> Error e
                                        | Ok acc -> Ok (ld :: acc)

                        match despacharTodo reservasActivas with
                        | Error msg -> return Error msg
                        | Ok lotesDespachados ->
                            let rec persistirLotes = function
                                | [] -> async { return Ok () }
                                | l :: rest ->
                                    async {
                                        let! r = LoteRepository.actualizarStockYEstadoTx conn tx l
                                        match r with
                                        | Ok () -> return! persistirLotes rest
                                        | Error msg -> return Error msg
                                    }
                            return! persistirLotes lotesDespachados
        }

    let private marcarReservasDespachadasTx
        (conn: NpgsqlConnection)
        (tx: NpgsqlTransaction)
        (solicitudId: SolicitudEgresoInternoId)
        (movimientoId: MovimientoId)
        : Async<Result<unit, string>> =
        ReservaStockRepository.marcarDespachadaPorSolicitud conn tx solicitudId movimientoId

    let insertar (solicitud: SolicitudEgresoInterno) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let (SolicitudEgresoInternoId sId) = solicitud.Id
            let (EmpleadoId solId) = solicitud.Solicitante
            let (tipoStr, destinoTipo, destinoOtro) = tipoToRow solicitud.Tipo
            let signoStr = match solicitud.Signo with SignoEgresoInterno.Positivo -> "Positivo" | SignoEgresoInterno.Negativo -> "Negativo"
            let prodId = solicitud.ProductoId |> Option.map (fun (ProductoId g) -> box g) |> Option.defaultValue (box DBNull.Value)
            let loteId = solicitud.LoteId |> Option.map (fun (LoteId g) -> box g) |> Option.defaultValue (box DBNull.Value)
            let (estadoStr, resAprob, resFecha, resMotivo, entPor, fEntrega) = estadoToRow solicitud.Estado

            let sql = """
                INSERT INTO solicitud_egreso_interno (
                    id, tipo, destino_uso_tipo, destino_uso_otro, signo,
                    producto_id, lote_id, cantidad, unidad, motivo, solicitante_id,
                    estado, resolucion_aprobador_id, resolucion_fecha, resolucion_motivo,
                    entregado_por, fecha_entrega, creado_en
                ) VALUES (
                    @id, @tipo, @destinoTipo, @destinoOtro, @signo,
                    @productoId, @loteId, @cantidad, @unidad, @motivo, @solicitanteId,
                    @estado, @resAprob, @resFecha, @resMotivo, @entPor, @fEntrega, @creadoEn
                )
            """
            let p = {|
                id = sId
                tipo = tipoStr
                destinoTipo = (if isNull destinoTipo then box DBNull.Value else box destinoTipo)
                destinoOtro = (if isNull destinoOtro then box DBNull.Value else box destinoOtro)
                signo = signoStr
                productoId = prodId
                loteId = loteId
                cantidad = solicitud.Cantidad.Valor
                unidad = UnidadMedida.aTexto solicitud.Cantidad.Unidad
                motivo = solicitud.Motivo
                solicitanteId = solId
                estado = estadoStr
                resAprob = resAprob
                resFecha = resFecha
                resMotivo = (if isNull resMotivo then box DBNull.Value else box resMotivo)
                entPor = entPor
                fEntrega = fEntrega
                creadoEn = solicitud.CreadoEn
            |}
            let! affected = conn.ExecuteAsync(sql, p) |> Async.AwaitTask
            if affected = 1 then return Ok ()
            else return Error (sprintf "insertar solicitud afectó %d filas" affected)
        }

    let actualizarEstado (solicitud: SolicitudEgresoInterno) : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            let (SolicitudEgresoInternoId sId) = solicitud.Id
            let (estadoStr, resAprob, resFecha, resMotivo, entPor, fEntrega) = estadoToRow solicitud.Estado
            let loteId = solicitud.LoteId |> Option.map (fun (LoteId g) -> box g) |> Option.defaultValue (box DBNull.Value)

            let sql = """
                UPDATE solicitud_egreso_interno
                SET estado = @estado,
                    lote_id = COALESCE(lote_id, @loteId),
                    resolucion_aprobador_id = @resAprob,
                    resolucion_fecha = @resFecha,
                    resolucion_motivo = @resMotivo,
                    entregado_por = @entPor,
                    fecha_entrega = @fEntrega
                WHERE id = @id
            """
            let p = {|
                id = sId
                estado = estadoStr
                loteId = loteId
                resAprob = resAprob
                resFecha = resFecha
                resMotivo = (if isNull resMotivo then box DBNull.Value else box resMotivo)
                entPor = entPor
                fEntrega = fEntrega
            |}
            let! affected = conn.ExecuteAsync(sql, p) |> Async.AwaitTask
            if affected = 1 then return Ok ()
            else return Error (sprintf "actualizarEstado solicitud %s afectó %d filas" (sId.ToString()) affected)
        }

    let insertarConReservaTx
        (solicitud: SolicitudEgresoInterno)
        (reservas: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                // 1. INSERT solicitud PRIMERO (parent), para que la FK se satisfaga
                let (SolicitudEgresoInternoId sId) = solicitud.Id
                let (EmpleadoId solId) = solicitud.Solicitante
                let (tipoStr, destinoTipo, destinoOtro) = tipoToRow solicitud.Tipo
                let signoStr = match solicitud.Signo with SignoEgresoInterno.Positivo -> "Positivo" | SignoEgresoInterno.Negativo -> "Negativo"
                let prodId = solicitud.ProductoId |> Option.map (fun (ProductoId g) -> box g) |> Option.defaultValue (box DBNull.Value)
                let loteId = solicitud.LoteId |> Option.map (fun (LoteId g) -> box g) |> Option.defaultValue (box DBNull.Value)
                let (estadoStr, resAprob, resFecha, resMotivo, entPor, fEntrega) = estadoToRow solicitud.Estado

                let sql = """
                    INSERT INTO solicitud_egreso_interno (
                        id, tipo, destino_uso_tipo, destino_uso_otro, signo,
                        producto_id, lote_id, cantidad, unidad, motivo, solicitante_id,
                        estado, resolucion_aprobador_id, resolucion_fecha, resolucion_motivo,
                        entregado_por, fecha_entrega, creado_en
                    ) VALUES (
                        @id, @tipo, @destinoTipo, @destinoOtro, @signo,
                        @productoId, @loteId, @cantidad, @unidad, @motivo, @solicitanteId,
                        @estado, @resAprob, @resFecha, @resMotivo, @entPor, @fEntrega, @creadoEn
                    )
                """
                let p = {|
                    id = sId
                    tipo = tipoStr
                    destinoTipo = (if isNull destinoTipo then box DBNull.Value else box destinoTipo)
                    destinoOtro = (if isNull destinoOtro then box DBNull.Value else box destinoOtro)
                    signo = signoStr
                    productoId = prodId
                    loteId = loteId
                    cantidad = solicitud.Cantidad.Valor
                    unidad = UnidadMedida.aTexto solicitud.Cantidad.Unidad
                    motivo = solicitud.Motivo
                    solicitanteId = solId
                    estado = estadoStr
                    resAprob = resAprob
                    resFecha = resFecha
                    resMotivo = (if isNull resMotivo then box DBNull.Value else box resMotivo)
                    entPor = entPor
                    fEntrega = fEntrega
                    creadoEn = solicitud.CreadoEn
                |}
                let! affected = conn.ExecuteAsync(sql, p, tx) |> Async.AwaitTask
                if affected <> 1 then
                    tx.Rollback()
                    return Error (sprintf "insertar solicitud afectó %d filas" affected)
                else

                // 2. AHORA reservar lotes (child). La solicitud ya existe, FK se satisface.
                let! resLotes = reservarLotesTx conn tx solicitud.Id reservas
                match resLotes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->
                    tx.Commit()
                    return Ok ()
            with ex ->
                tx.Rollback()
                eprintfn "[SolicitudRepository.insertarConReservaTx] Error: %s" ex.Message
                return Error ex.Message
        }

    let actualizarEstadoConReservaTx
        (solicitud: SolicitudEgresoInterno)
        (reservas: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                let! resLotes = reservarLotesTx conn tx solicitud.Id reservas
                match resLotes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->
                    let (SolicitudEgresoInternoId sId) = solicitud.Id
                    let (estadoStr, resAprob, resFecha, resMotivo, entPor, fEntrega) = estadoToRow solicitud.Estado
                    let loteId = solicitud.LoteId |> Option.map (fun (LoteId g) -> box g) |> Option.defaultValue (box DBNull.Value)

                    let sql = """
                        UPDATE solicitud_egreso_interno
                        SET estado = @estado,
                            lote_id = COALESCE(lote_id, @loteId),
                            resolucion_aprobador_id = @resAprob,
                            resolucion_fecha = @resFecha,
                            resolucion_motivo = @resMotivo,
                            entregado_por = @entPor,
                            fecha_entrega = @fEntrega
                        WHERE id = @id
                    """
                    let p = {|
                        id = sId
                        estado = estadoStr
                        loteId = loteId
                        resAprob = resAprob
                        resFecha = resFecha
                        resMotivo = (if isNull resMotivo then box DBNull.Value else box resMotivo)
                        entPor = entPor
                        fEntrega = fEntrega
                    |}
                    let! affected = conn.ExecuteAsync(sql, p, tx) |> Async.AwaitTask
                    if affected <> 1 then
                        tx.Rollback()
                        return Error (sprintf "actualizarEstado solicitud %s afectó %d filas" (sId.ToString()) affected)
                    else
                        tx.Commit()
                        return Ok ()
            with ex ->
                tx.Rollback()
                eprintfn "[SolicitudRepository.actualizarEstadoConReservaTx] Error: %s" ex.Message
                return Error ex.Message
        }

    let confirmarTx
        (solicitud: SolicitudEgresoInterno)
        (movimiento: MovimientoInventario)
        (_consumos: (LoteId * Cantidad) list)
        : Async<Result<unit, string>> =
        async {
            use conn = DbConnection.crear ()
            conn.Open()
            use tx = conn.BeginTransaction()
            try
                let (SolicitudEgresoInternoId sId) = solicitud.Id
                let (EmpleadoId respId) = movimiento.Responsable
                let (MovimientoId mId) = movimiento.Id

                // 1. INSERT movimiento_inventario (parent)
                let sqlMov = """
                    INSERT INTO movimiento_inventario (
                        id, fecha, responsable_id, tipo, motivo, orden_origen_id, contraparte_ref,
                        contraparte_nombre, departamento, solicitante, observaciones
                    ) VALUES (
                        @id, @fecha, @responsableId, @tipo, @motivo, NULL, NULL,
                        NULL, @departamento, @solicitante, @observaciones
                    )
                """
                let (tipoMovStr, motivoMovStr, deptoStr, solStr) =
                    match solicitud.Tipo with
                    | TipoEgresoInterno.UsoInterno dest ->
                        ("Salida", "UsoInterno", DestinoUso.aTexto dest, "Personal Interno")
                    | TipoEgresoInterno.Merma ->
                        ("Salida", "Merma", null, null)
                    | TipoEgresoInterno.Ajuste ->
                        let t = match solicitud.Signo with SignoEgresoInterno.Positivo -> "Entrada" | SignoEgresoInterno.Negativo -> "Salida"
                        (t, "Ajuste", null, null)

                let pMov = {|
                    id = mId
                    fecha = movimiento.Fecha
                    responsableId = respId
                    tipo = tipoMovStr
                    motivo = motivoMovStr
                    departamento = (if isNull deptoStr then box DBNull.Value else box deptoStr)
                    solicitante = (if isNull solStr then box DBNull.Value else box solStr)
                    observaciones = (match movimiento.Observaciones with Some o -> box o | None -> box DBNull.Value)
                |}
                let! affectedMov = conn.ExecuteAsync(sqlMov, pMov, tx) |> Async.AwaitTask

                if affectedMov <> 1 then
                    tx.Rollback()
                    return Error "No se pudo insertar movimiento_inventario"
                else

                // 2. INSERT linea_movimiento por cada línea (child de movimiento)
                match solicitud.LoteId with
                | Some (LoteId lId) ->
                    let sqlLinea = """
                        INSERT INTO linea_movimiento (id, movimiento_id, lote_id, cantidad, unidad, observaciones)
                        VALUES (@id, @movimiento_id, @lote_id, @cantidad, @unidad, NULL)
                    """
                    let pLinea = {|
                        id = Identidad.nuevo ()
                        movimiento_id = mId
                        lote_id = lId
                        cantidad = solicitud.Cantidad.Valor
                        unidad = UnidadMedida.aTexto solicitud.Cantidad.Unidad
                    |}
                    do! conn.ExecuteAsync(sqlLinea, pLinea, tx) |> Async.AwaitTask |> Async.Ignore
                | None -> ()

                // 3. UPDATE lote: descontar cantidad_actual y cantidad_reservada (o sumar stock si Positivo)
                let! actLotesRes = actualizarLotesTx conn tx solicitud
                match actLotesRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->

                // 4. UPDATE reserva_stock: marcar como despachadas (solo si no es Ajuste Positivo)
                let! resvRes =
                    if solicitud.Signo = SignoEgresoInterno.Positivo then
                        async { return Ok () }
                    else
                        marcarReservasDespachadasTx conn tx solicitud.Id movimiento.Id

                match resvRes with
                | Error msg ->
                    tx.Rollback()
                    return Error msg
                | Ok () ->

                // 5. UPDATE solicitud_egreso_interno SET estado='Confirmada', entregado_por=@resp, fecha_entrega=NOW() WHERE id=@sId
                let (estadoStr, _resAprob, _resFecha, _resMotivo, entPor, fEntrega) = estadoToRow solicitud.Estado
                let sqlUpdateSol = """
                    UPDATE solicitud_egreso_interno
                    SET estado = @estado,
                        entregado_por = @entPor,
                        fecha_entrega = @fEntrega
                    WHERE id = @id
                """
                let pSol = {|
                    id = sId
                    estado = estadoStr
                    entPor = entPor
                    fEntrega = fEntrega
                |}
                let! affectedSol = conn.ExecuteAsync(sqlUpdateSol, pSol, tx) |> Async.AwaitTask

                if affectedSol <> 1 then
                    tx.Rollback()
                    return Error (sprintf "No se pudo actualizar estado de solicitud %s" (sId.ToString()))
                else
                    // 6. tx.Commit()
                    tx.Commit()
                    return Ok ()
            with ex ->
                tx.Rollback()
                eprintfn "[SolicitudRepository.confirmarTx] Error: %s" ex.Message
                return Error ex.Message
        }

    let obtenerPorId (id: SolicitudEgresoInternoId) : Async<SolicitudEgresoInterno option> =
        async {
            use conn = DbConnection.crear ()
            let (SolicitudEgresoInternoId sId) = id
            let sql = "SELECT * FROM solicitud_egreso_interno WHERE id = @id"
            let! rows = conn.QueryAsync<SolicitudRow>(sql, {| id = sId |}) |> Async.AwaitTask
            match rows |> Seq.tryHead with
            | None -> return None
            | Some row ->
                match fromRow row with
                | Ok s -> return Some s
                | Error msg ->
                    eprintfn "[INTEGRIDAD] SolicitudRepository.obtenerPorId id=%s: %s" (sId.ToString()) msg
                    return None
        }

    let listar (estadoFilter: string option) (solicitanteFilter: EmpleadoId option) : Async<SolicitudEgresoInterno list> =
        async {
            use conn = DbConnection.crear ()
            let mutable whereParts = ResizeArray<string>()
            let p = DynamicParameters()
            match estadoFilter with
            | Some e when not (String.IsNullOrWhiteSpace e) ->
                whereParts.Add("estado = @estado")
                p.Add("estado", e)
            | _ -> ()
            match solicitanteFilter with
            | Some (EmpleadoId eId) ->
                whereParts.Add("solicitante_id = @solId")
                p.Add("solId", eId)
            | None -> ()
            let whereClause =
                if whereParts.Count = 0 then "" else "WHERE " + String.Join(" AND ", whereParts)
            let sql = sprintf "SELECT * FROM solicitud_egreso_interno %s ORDER BY creado_en DESC" whereClause
            let! rows = conn.QueryAsync<SolicitudRow>(sql, p) |> Async.AwaitTask
            let rec loop acc = function
                | [] -> List.rev acc
                | (row: SolicitudRow) :: rest ->
                    match fromRow row with
                    | Ok s -> loop (s :: acc) rest
                    | Error msg ->
                        eprintfn "[INTEGRIDAD] SolicitudRepository.listar id=%s: %s" (row.id.ToString()) msg
                        loop acc rest
            return loop [] (rows |> Seq.toList)
        }
