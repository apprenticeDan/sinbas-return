module Sinbas.Domain.Tests.SolicitudEgresoInternoServiceTests

open System
open Xunit
open Sinbas.Domain
open Sinbas.Application

// ─────────────────────────────────────────────────────────────
// Helpers de Fixtures para Tests de SolicitudService
// ─────────────────────────────────────────────────────────────

let private mkLote (loteId: LoteId) (prodId: ProductoId) (codStr: string) (actual: decimal) (reservada: decimal) (unidad: UnidadMedida) : Lote =
    let cod = unwrap (CodigoLote.desdeString codStr)
    { Id = loteId
      Codigo = cod
      ProductoId = prodId
      Procedencia = None
      CantidadInicial = cantReconst actual unidad
      CantidadActual = cantReconst actual unidad
      CantidadReservada = cantReconst reservada unidad
      FechaIngreso = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))
      Ubicacion = None
      Estado = EstadoLote.Activo
      Observaciones = None }

let private resolverProd _ = "Swietenia macrophylla"
let private resolverLote _ = "SWIETMAC-02608-01"

// ─────────────────────────────────────────────────────────────
// 8 Casos Obligatorios de Fase 6a (Tarea 8)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``1. UsoInterno Pendiente -> Confirmada: reserva al crear, consume al confirmar`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let confEmpId = Identidad.nuevo ()
    let lote = mkLote loteId prodId "SWIETMAC-02608-01" 50m 0m Kilogramo

    let mutable reservasCreadas : (LoteId * Cantidad) list = []
    let mutable solicitudGuardada : SolicitudEgresoInterno option = None

    let mockInsertar (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudGuardada <- Some sol
            reservasCreadas <- res
            return Ok ()
        }

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "UsoInterno"
          DestinoTipo = Some "Vivero"
          DestinoOtro = None
          Signo = "Negativo"
          ProductoId = None
          LoteId = Some (let (LoteId g) = loteId in g.ToString())
          Cantidad = 10m
          Unidad = "Kilogramo"
          Motivo = "Producción de plantines en vivero central" }

    // 1. Crear solicitud UsoInterno -> debe reservar en Pendiente (D1)
    let resCrear =
        SolicitudService.crear
            mockInsertar
            (fun _ -> async { return Some lote })
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    match resCrear with
    | Error (msg, code) -> failwithf "Crear falló con %d: %s" code msg
    | Ok dto ->
        Assert.Equal("Pendiente", dto.Estado)
        Assert.Equal("UsoInterno", dto.Tipo)
        Assert.Equal(Some "Vivero", dto.Destino)
        Assert.Equal(10m, dto.Cantidad)
        // Verificar que reservó al crear
        Assert.Single(reservasCreadas) |> ignore
        let (resLoteId, resCant) = List.head reservasCreadas
        Assert.Equal(loteId, resLoteId)
        Assert.Equal(10m, resCant.Valor)
        Assert.Equal(Kilogramo, resCant.Unidad)

    // 2. Confirmar solicitud -> debe consumir reserva al confirmar
    let mutable consumosConfirmados : (LoteId * Cantidad) list = []
    let mockConfirmarTx (sol: SolicitudEgresoInterno) (_mov: MovimientoInventario) (consumos: (LoteId * Cantidad) list) =
        async {
            consumosConfirmados <- consumos
            return Ok ()
        }

    let solIdRaw = (Option.get solicitudGuardada).Id |> (fun (SolicitudEgresoInternoId g) -> g.ToString())

    let resConfirmar =
        SolicitudService.confirmar
            (fun _ -> async { return solicitudGuardada })
            mockConfirmarTx
            resolverProd
            resolverLote
            confEmpId
            solIdRaw
        |> Async.RunSynchronously

    match resConfirmar with
    | Error (msg, code) -> failwithf "Confirmar falló con %d: %s" code msg
    | Ok dtoConf ->
        Assert.Equal("Confirmada", dtoConf.Estado)
        Assert.Equal(Some (confEmpId.ToString()), dtoConf.EntregadoPor)
        // Verificar que consumió la reserva
        Assert.Single(consumosConfirmados) |> ignore
        let (cId, cCant) = List.head consumosConfirmados
        Assert.Equal(loteId, cId)
        Assert.Equal(10m, cCant.Valor)

[<Fact>]
let ``2. Merma Pendiente -> Rechazada: no reserva, no consume, no libera`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let rechEmpId = Identidad.nuevo ()
    let lote = mkLote loteId prodId "SWIETMAC-02608-01" 20m 0m Kilogramo

    let mutable reservasCreadas : (LoteId * Cantidad) list = []
    let mutable solicitudGuardada : SolicitudEgresoInterno option = None

    let mockInsertar (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudGuardada <- Some sol
            reservasCreadas <- res
            return Ok ()
        }

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "Merma"
          DestinoTipo = None
          DestinoOtro = None
          Signo = "Negativo"
          ProductoId = None
          LoteId = Some (let (LoteId g) = loteId in g.ToString())
          Cantidad = 5m
          Unidad = "Kilogramo"
          Motivo = "Semilla podrida por filtración de agua" }

    // 1. Crear Merma -> NO debe reservar al crear (D2)
    let resCrear =
        SolicitudService.crear
            mockInsertar
            (fun _ -> async { return Some lote })
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    match resCrear with
    | Error (msg, code) -> failwithf "Crear falló con %d: %s" code msg
    | Ok dto ->
        Assert.Equal("Pendiente", dto.Estado)
        Assert.Equal("Merma", dto.Tipo)
        // No reserva al crear
        Assert.Empty(reservasCreadas)

    // 2. Rechazar Merma -> pasa a Rechazada sin tocar reservas
    let mutable solicitudActualizada : SolicitudEgresoInterno option = None
    let mockActualizarEstado (sol: SolicitudEgresoInterno) =
        async {
            solicitudActualizada <- Some sol
            return Ok ()
        }

    let solIdRaw = (Option.get solicitudGuardada).Id |> (fun (SolicitudEgresoInternoId g) -> g.ToString())
    let reqRechazar : RechazarSolicitudRequest = { Motivo = "Causa no demostrada en informe" }

    let resRechazar =
        SolicitudService.rechazar
            (fun _ -> async { return solicitudGuardada })
            mockActualizarEstado
            resolverProd
            resolverLote
            rechEmpId
            solIdRaw
            reqRechazar
        |> Async.RunSynchronously

    match resRechazar with
    | Error (msg, code) -> failwithf "Rechazar falló con %d: %s" code msg
    | Ok dtoRech ->
        Assert.Equal("Rechazada", dtoRech.Estado)
        Assert.Equal(Some (rechEmpId.ToString()), dtoRech.AprobadorId)
        Assert.Equal(Some "Causa no demostrada en informe", dtoRech.MotivoResolucion)

[<Fact>]
let ``3. Merma Pendiente -> Aprobada -> Confirmada: reserva al aprobar, consume al confirmar`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let aprobEmpId = Identidad.nuevo ()
    let confEmpId = Identidad.nuevo ()
    let lote = mkLote loteId prodId "SWIETMAC-02608-01" 30m 0m Kilogramo

    let mutable reservasCreadas : (LoteId * Cantidad) list = []
    let mutable solicitudActual : SolicitudEgresoInterno option = None

    let mockInsertar (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasCreadas <- res
            return Ok ()
        }

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "Merma"
          DestinoTipo = None
          DestinoOtro = None
          Signo = "Negativo"
          ProductoId = None
          LoteId = Some (let (LoteId g) = loteId in g.ToString())
          Cantidad = 3m
          Unidad = "Kilogramo"
          Motivo = "Avería por polilla en almacén seco" }

    // 1. Crear Merma -> no reserva
    let resCrear =
        SolicitudService.crear
            mockInsertar
            (fun _ -> async { return Some lote })
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    Assert.True(resCrear |> Result.isOk)
    Assert.Empty(reservasCreadas)

    // 2. Aprobar Merma -> DEBE reservar en Aprobada (D2)
    let mutable reservasAprobadas : (LoteId * Cantidad) list = []
    let mockActualizarConReserva (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasAprobadas <- res
            return Ok ()
        }

    let solIdRaw = (Option.get solicitudActual).Id |> (fun (SolicitudEgresoInternoId g) -> g.ToString())

    let resAprobar =
        SolicitudService.aprobar
            (fun _ -> async { return solicitudActual })
            mockActualizarConReserva
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            aprobEmpId
            solIdRaw
        |> Async.RunSynchronously

    match resAprobar with
    | Error (msg, code) -> failwithf "Aprobar falló con %d: %s" code msg
    | Ok dtoAprob ->
        Assert.Equal("Aprobada", dtoAprob.Estado)
        Assert.Single(reservasAprobadas) |> ignore
        let (rId, rCant) = List.head reservasAprobadas
        Assert.Equal(loteId, rId)
        Assert.Equal(3m, rCant.Valor)

    // 3. Confirmar Merma -> consume la reserva
    let mutable consumosConfirmados : (LoteId * Cantidad) list = []
    let mockConfirmarTx (sol: SolicitudEgresoInterno) (_mov: MovimientoInventario) (consumos: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            consumosConfirmados <- consumos
            return Ok ()
        }

    let resConfirmar =
        SolicitudService.confirmar
            (fun _ -> async { return solicitudActual })
            mockConfirmarTx
            resolverProd
            resolverLote
            confEmpId
            solIdRaw
        |> Async.RunSynchronously

    match resConfirmar with
    | Error (msg, code) -> failwithf "Confirmar falló con %d: %s" code msg
    | Ok dtoConf ->
        Assert.Equal("Confirmada", dtoConf.Estado)
        Assert.Single(consumosConfirmados) |> ignore
        let (cId, cCant) = List.head consumosConfirmados
        Assert.Equal(loteId, cId)
        Assert.Equal(3m, cCant.Valor)

[<Fact>]
let ``4. Ajuste Positivo Pendiente -> Aprobada -> Confirmada: no reserva, suma cantidad_actual`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let aprobEmpId = Identidad.nuevo ()
    let confEmpId = Identidad.nuevo ()
    let lote = mkLote loteId prodId "SWIETMAC-02608-01" 10m 0m Kilogramo

    let mutable reservasCrear : (LoteId * Cantidad) list = []
    let mutable solicitudActual : SolicitudEgresoInterno option = None

    let mockInsertar (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasCrear <- res
            return Ok ()
        }

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "Ajuste"
          DestinoTipo = None
          DestinoOtro = None
          Signo = "Positivo"
          ProductoId = None
          LoteId = Some (let (LoteId g) = loteId in g.ToString())
          Cantidad = 2m
          Unidad = "Kilogramo"
          Motivo = "Sobrante de inventario constatado en auditoría" }

    // 1. Crear Ajuste Positivo -> nunca reserva (D3)
    let resCrear =
        SolicitudService.crear
            mockInsertar
            (fun _ -> async { return Some lote })
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    Assert.True(resCrear |> Result.isOk)
    Assert.Empty(reservasCrear)

    // 2. Aprobar Ajuste Positivo -> nunca reserva (D3)
    let mutable reservasAprobadas : (LoteId * Cantidad) list = []
    let mockActualizarConReserva (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasAprobadas <- res
            return Ok ()
        }

    let solIdRaw = (Option.get solicitudActual).Id |> (fun (SolicitudEgresoInternoId g) -> g.ToString())

    let resAprobar =
        SolicitudService.aprobar
            (fun _ -> async { return solicitudActual })
            mockActualizarConReserva
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            aprobEmpId
            solIdRaw
        |> Async.RunSynchronously

    Assert.True(resAprobar |> Result.isOk)
    Assert.Empty(reservasAprobadas)

    // 3. Confirmar Ajuste Positivo -> no consume reservas (consumos es vacio)
    let mutable consumosConfirmados : (LoteId * Cantidad) list = []
    let mockConfirmarTx (sol: SolicitudEgresoInterno) (_mov: MovimientoInventario) (consumos: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            consumosConfirmados <- consumos
            return Ok ()
        }

    let resConfirmar =
        SolicitudService.confirmar
            (fun _ -> async { return solicitudActual })
            mockConfirmarTx
            resolverProd
            resolverLote
            confEmpId
            solIdRaw
        |> Async.RunSynchronously

    match resConfirmar with
    | Error (msg, code) -> failwithf "Confirmar falló con %d: %s" code msg
    | Ok dtoConf ->
        Assert.Equal("Confirmada", dtoConf.Estado)
        Assert.Empty(consumosConfirmados)

[<Fact>]
let ``5. Ajuste Negativo Pendiente -> Aprobada -> Confirmada: reserva al aprobar, consume al confirmar`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let aprobEmpId = Identidad.nuevo ()
    let confEmpId = Identidad.nuevo ()
    let lote = mkLote loteId prodId "SWIETMAC-02608-01" 25m 0m Kilogramo

    let mutable reservasCrear : (LoteId * Cantidad) list = []
    let mutable solicitudActual : SolicitudEgresoInterno option = None

    let mockInsertar (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasCrear <- res
            return Ok ()
        }

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "Ajuste"
          DestinoTipo = None
          DestinoOtro = None
          Signo = "Negativo"
          ProductoId = None
          LoteId = Some (let (LoteId g) = loteId in g.ToString())
          Cantidad = 4m
          Unidad = "Kilogramo"
          Motivo = "Faltante físico por re-pesaje" }

    // 1. Crear Ajuste Negativo -> no reserva al crear
    let resCrear =
        SolicitudService.crear
            mockInsertar
            (fun _ -> async { return Some lote })
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    Assert.True(resCrear |> Result.isOk)
    Assert.Empty(reservasCrear)

    // 2. Aprobar Ajuste Negativo -> DEBE reservar (D2)
    let mutable reservasAprobadas : (LoteId * Cantidad) list = []
    let mockActualizarConReserva (sol: SolicitudEgresoInterno) (res: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            reservasAprobadas <- res
            return Ok ()
        }

    let solIdRaw = (Option.get solicitudActual).Id |> (fun (SolicitudEgresoInternoId g) -> g.ToString())

    let resAprobar =
        SolicitudService.aprobar
            (fun _ -> async { return solicitudActual })
            mockActualizarConReserva
            (fun _ _ -> async { return [ lote ] })
            resolverProd
            resolverLote
            aprobEmpId
            solIdRaw
        |> Async.RunSynchronously

    Assert.True(resAprobar |> Result.isOk)
    Assert.Single(reservasAprobadas) |> ignore
    let (rId, rCant) = List.head reservasAprobadas
    Assert.Equal(loteId, rId)
    Assert.Equal(4m, rCant.Valor)

    // 3. Confirmar Ajuste Negativo -> consume reservas
    let mutable consumosConfirmados : (LoteId * Cantidad) list = []
    let mockConfirmarTx (sol: SolicitudEgresoInterno) (_mov: MovimientoInventario) (consumos: (LoteId * Cantidad) list) =
        async {
            solicitudActual <- Some sol
            consumosConfirmados <- consumos
            return Ok ()
        }

    let resConfirmar =
        SolicitudService.confirmar
            (fun _ -> async { return solicitudActual })
            mockConfirmarTx
            resolverProd
            resolverLote
            confEmpId
            solIdRaw
        |> Async.RunSynchronously

    match resConfirmar with
    | Error (msg, code) -> failwithf "Confirmar falló con %d: %s" code msg
    | Ok dtoConf ->
        Assert.Equal("Confirmada", dtoConf.Estado)
        Assert.Single(consumosConfirmados) |> ignore
        let (cId, cCant) = List.head consumosConfirmados
        Assert.Equal(loteId, cId)
        Assert.Equal(4m, cCant.Valor)

[<Fact>]
let ``6. Aprobador = solicitante en Merma/Ajuste: retorna OperacionInvalida (RN11)`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let mismoUsuarioId = Identidad.nuevo ()
    let solId = SolicitudEgresoInternoId (Identidad.nuevo ())

    let solMerma =
        unwrap (
            SolicitudEgresoInterno.crear
                solId
                TipoEgresoInterno.Merma
                SignoEgresoInterno.Negativo
                (Some prodId)
                (Some loteId)
                (cantReconst 5m Kilogramo)
                "Semilla con hongo"
                (EmpleadoId mismoUsuarioId)
                DateTime.UtcNow
        )

    let solIdRaw = let (SolicitudEgresoInternoId g) = solId in g.ToString()

    let res =
        SolicitudService.aprobar
            (fun _ -> async { return Some solMerma })
            (fun _ _ -> async { return Ok () })
            (fun _ _ -> async { return [] })
            resolverProd
            resolverLote
            mismoUsuarioId
            solIdRaw
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Se esperaba rechazo por RN11 pero fue exitoso"
    | Error (msg, code) ->
        Assert.Equal(409, code)
        Assert.Contains("RN11", msg)

[<Fact>]
let ``7. UsoInterno intentando aprobarse: retorna OperacionInvalida`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let loteId = LoteId (Identidad.nuevo ())
    let solEmpId = Identidad.nuevo ()
    let aprobEmpId = Identidad.nuevo ()
    let solId = SolicitudEgresoInternoId (Identidad.nuevo ())

    let solUso =
        unwrap (
            SolicitudEgresoInterno.crear
                solId
                (TipoEgresoInterno.UsoInterno DestinoUso.Vivero)
                SignoEgresoInterno.Negativo
                (Some prodId)
                (Some loteId)
                (cantReconst 10m Kilogramo)
                "Semillas para almácigos"
                (EmpleadoId solEmpId)
                DateTime.UtcNow
        )

    let solIdRaw = let (SolicitudEgresoInternoId g) = solId in g.ToString()

    let res =
        SolicitudService.aprobar
            (fun _ -> async { return Some solUso })
            (fun _ _ -> async { return Ok () })
            (fun _ _ -> async { return [] })
            resolverProd
            resolverLote
            aprobEmpId
            solIdRaw
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "UsoInterno no debe permitir aprobación"
    | Error (msg, code) ->
        Assert.Equal(409, code)
        Assert.Contains("no requiere aprobación previa", msg)

[<Fact>]
let ``8. FIFO requiere >1 lote para la cantidad: retorna 422 con mensaje`` () =
    let prodId = ProductoId (Identidad.nuevo ())
    let lote1 = mkLote (LoteId (Identidad.nuevo ())) prodId "SWIETMAC-02608-01" 3m 0m Kilogramo
    let lote2 = mkLote (LoteId (Identidad.nuevo ())) prodId "SWIETMAC-02608-02" 3m 0m Kilogramo
    let solEmpId = Identidad.nuevo ()

    let reqCrear : CrearSolicitudRequest =
        { Tipo = "Merma"
          DestinoTipo = None
          DestinoOtro = None
          Signo = "Negativo"
          ProductoId = Some (let (ProductoId g) = prodId in g.ToString())
          LoteId = None
          Cantidad = 5m
          Unidad = "Kilogramo"
          Motivo = "Merma general de producto" }

    let res =
        SolicitudService.crear
            (fun _ _ -> async { return Ok () })
            (fun _ -> async { return None })
            (fun _ _ -> async { return [ lote1; lote2 ] })
            resolverProd
            resolverLote
            solEmpId
            reqCrear
        |> Async.RunSynchronously

    match res with
    | Ok _ -> failwith "Se esperaba rechazo 422 por requerir múltiples lotes"
    | Error (msg, code) ->
        Assert.Equal(422, code)
        Assert.Contains("requiere múltiples lotes", msg)
