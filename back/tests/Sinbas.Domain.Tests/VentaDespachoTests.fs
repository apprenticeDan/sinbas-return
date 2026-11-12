module VentaDespachoTests

open System
open Xunit
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Feature F8: Confirmación de Venta y Despacho Físico (RF-08 | CU-11)
// Tests unitarios de dominio puro
// ─────────────────────────────────────────────────────────────

let private prodId1 = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000001"))
let private prodId2 = ProductoId (Guid.Parse("01917f3a-0003-7000-8000-000000000002"))
let private usuarioRespId = UsuarioId (Guid.NewGuid())
let private empleadoRespId = EmpleadoId (Guid.NewGuid())
let private clienteId = ClienteId (Guid.NewGuid())

let private crearCantidad valor unidad =
    match Cantidad.crear valor unidad with
    | Ok c -> c
    | Error e -> failwithf "Cantidad error: %A" e

let private proformaVigente () : Proforma =
    let pid = ProformaId (Guid.NewGuid())
    let c1 = crearCantidad 10.0m Kilogramo
    let lineas = [ { ProductoId = prodId1; Cantidad = c1; PrecioUnitario = 150.00m } ]
    let fecha = DateOnly(2026, 10, 7)
    let fv = DateOnly(2026, 10, 14)
    match Proforma.crear pid usuarioRespId (Some clienteId) (Some "Cliente Registrado") lineas fecha (Some fv) (Some "BOB") None with
    | Ok p -> p
    | Error err -> failwithf "Error creando proforma test: %A" err

[<Fact>]
let ``// RF-08 | CU-11 - Confirmar venta desde proforma vigente genera OrdenVenta y Proforma Convertida`` () =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta =
        match CodigoVenta.generar hoy 1 with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    match OrdenVenta.crearDesdeProforma hoy ordenId codVenta p clienteId empleadoRespId with
    | Ok (ordenVenta, proformaConvertida) ->
        // Validaciones de OrdenVenta
        Assert.Equal(ordenId, ordenVenta.Id)
        Assert.Equal("OV-202610-0001", CodigoVenta.valor ordenVenta.Codigo)
        Assert.Equal(p.Id, ordenVenta.ProformaOrigenId)
        Assert.Equal(clienteId, ordenVenta.ClienteId)
        Assert.Equal(hoy, ordenVenta.Fecha)
        Assert.Equal(empleadoRespId, ordenVenta.ResponsableId)
        Assert.Equal(1500.00m, ordenVenta.Total)
        Assert.Equal(EstadoVenta.Confirmada, ordenVenta.Estado)
        Assert.Single(ordenVenta.Lineas) |> ignore

        // Validaciones de Proforma convertida
        Assert.Equal(EstadoProforma.Convertida, proformaConvertida.Estado)
        Assert.Equal(Some ordenId, proformaConvertida.OrdenVentaId)
    | Error err ->
        failwithf "Se esperaba Ok, se obtuvo: %A" err

[<Fact>]
let ``// RF-08 | RN14 - Rechaza confirmacion si la proforma ya esta Convertida o Anulada`` () =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta = CodigoVenta.reconstruir "OV-202610-0003"

    // 1. Proforma Anulada
    let pAnulada =
        match Proforma.anular "Motivo test" usuarioRespId p with
        | Ok a -> a
        | Error e -> failwithf "%A" e

    match OrdenVenta.crearDesdeProforma hoy ordenId codVenta pAnulada clienteId empleadoRespId with
    | Error (OperacionInvalida msg) -> Assert.Contains("anulada", msg)
    | res -> failwithf "Se esperaba Error OperacionInvalida, se obtuvo: %A" res

    // 2. Proforma ya Convertida
    let pConvertida = { p with Estado = EstadoProforma.Convertida }
    match OrdenVenta.crearDesdeProforma hoy ordenId codVenta pConvertida clienteId empleadoRespId with
    | Error (OperacionInvalida msg) -> Assert.Contains("convertida", msg)
    | res -> failwithf "Se esperaba Error OperacionInvalida, se obtuvo: %A" res

[<Fact>]
let ``// RF-08 | CU-11 - OrdenDespacho.desdeVenta genera solicitud pendiente con lineas de producto`` () =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta = CodigoVenta.reconstruir "OV-202610-0004"
    let despachoId = OrdenId (Guid.NewGuid())
    let codDespacho =
        match CodigoDespacho.generar hoy 1 with
        | Ok c -> c
        | Error e -> failwithf "%A" e

    let ordenVenta, _ =
        match OrdenVenta.crearDesdeProforma hoy ordenId codVenta p clienteId empleadoRespId with
        | Ok res -> res
        | Error e -> failwithf "%A" e

    let despacho = Despacho.desdeVenta despachoId codDespacho ordenVenta
    Assert.Equal(despachoId, despacho.Id)
    Assert.Equal("DSP-202610-0001", CodigoDespacho.valor despacho.Codigo)
    Assert.Equal(OrigenDespacho.DeVenta ordenId, despacho.Origen)
    Assert.Equal(Some clienteId, despacho.ClienteId)
    Assert.Equal(EstadoDespacho.Pendiente, despacho.Estado)
    Assert.Single(despacho.Lineas) |> ignore
    Assert.Equal(prodId1, despacho.Lineas.[0].Referencia)
    Assert.Equal(10.0m, despacho.Lineas.[0].Cantidad.Valor)

[<Fact>]
let ``// RF-08 | CU-11 - Ciclo de vida y transiciones de estado de OrdenVenta y OrdenDespacho`` () =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta = CodigoVenta.reconstruir "OV-202610-0005"
    let despachoId = OrdenId (Guid.NewGuid())
    let codDespacho = CodigoDespacho.reconstruir "DSP-202610-0005"

    let ordenVenta, _ =
        match OrdenVenta.crearDesdeProforma hoy ordenId codVenta p clienteId empleadoRespId with
        | Ok res -> res
        | Error e -> failwithf "%A" e

    let despacho = Despacho.desdeVenta despachoId codDespacho ordenVenta
    let movId = MovimientoId (Guid.NewGuid())

    // 1. Marcar despacho como Despachado
    match Despacho.marcarDespachado movId despacho with
    | Ok despachoFinal ->
        Assert.Equal(EstadoDespacho.Despachado movId, despachoFinal.Estado)

        // Marcar despacho por segunda vez debe fallar
        match Despacho.marcarDespachado movId despachoFinal with
        | Error (OperacionInvalida msg) -> Assert.Contains("ya fue despachada", msg)
        | res -> failwithf "Se esperaba error, se obtuvo: %A" res

        // Anular despacho ya despachado debe fallar
        match Despacho.anular "Motivo" empleadoRespId DateTime.UtcNow despachoFinal with
        | Error (OperacionInvalida msg) -> Assert.Contains("ya fue ejecutada", msg)
        | res -> failwithf "Se esperaba error, se obtuvo: %A" res
    | Error e -> failwithf "%A" e

    // 2. Marcar OrdenVenta como Despachada
    match OrdenVenta.marcarDespachada ordenVenta with
    | Ok ventaDespachada ->
        Assert.Equal(EstadoVenta.Despachada, ventaDespachada.Estado)

        // Marcar por segunda vez debe fallar
        match OrdenVenta.marcarDespachada ventaDespachada with
        | Error (OperacionInvalida msg) -> Assert.Contains("ya fue despachada", msg)
        | res -> failwithf "Se esperaba error, se obtuvo: %A" res

        // Anular venta ya despachada debe fallar
        match OrdenVenta.anular "Motivo" empleadoRespId DateTime.UtcNow ventaDespachada with
        | Error (OperacionInvalida msg) -> Assert.Contains("ya ha sido despachada", msg)
        | res -> failwithf "Se esperaba error, se obtuvo: %A" res
    | Error e -> failwithf "%A" e

[<Fact>]
let ``// RF-08 | CU-11 - Anulacion de OrdenVenta en estado Confirmada registra motivo y responsable`` () =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta = CodigoVenta.reconstruir "OV-202610-0006"

    let ordenVenta, _ =
        match OrdenVenta.crearDesdeProforma hoy ordenId codVenta p clienteId empleadoRespId with
        | Ok res -> res
        | Error e -> failwithf "%A" e

    let ahora = DateTime.UtcNow
    match OrdenVenta.anular "Desistimiento comercial" empleadoRespId ahora ordenVenta with
    | Ok ventaAnulada ->
        match ventaAnulada.Estado with
        | EstadoVenta.Anulada an ->
            Assert.Equal("Desistimiento comercial", an.Motivo)
            Assert.Equal(empleadoRespId, an.AnuladoPor)
            Assert.Equal(ahora, an.Fecha)
        | _ -> failwith "Se esperaba estado Anulada"
    | Error e -> failwithf "%A" e

// ─────────────────────────────────────────────────────────────
// Tests OBS-03: Reserva formal de stock en confirmación de venta y liberación en despacho
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``// OBS-03: Reserva formal reduce stock disponible para ventas posteriores pero stock fisico permanece`` () =
    let pId = ProductoId (Guid.NewGuid())
    let lId = LoteId (Guid.NewGuid())
    let cod = unwrap (CodigoLote.generar "Cedrela" "odorata" (DateOnly(2026, 10, 1)) 1)
    let cantInicial = cantReconst 100m Kilogramo
    let lote = unwrap (Lote.crear lId cod pId None cantInicial (DateOnly(2026, 10, 1)) None None)

    // Inicialmente disponible = 100 kg (100000 g)
    Assert.Equal(100000m, Stock.disponibleParaVenta pId [ lote ])
    Assert.Equal(100000m, Stock.stockFisicoProducto pId [ lote ])

    // Se reserva 40 kg para una venta confirmada
    let aReservar = cantReconst 40m Kilogramo
    let loteReservado = unwrap (Lote.reservar aReservar lote)

    // Stock disponible se reduce a 60 kg (60000 g), físico sigue en 100 kg
    Assert.Equal(60000m, Stock.disponibleParaVenta pId [ loteReservado ])
    Assert.Equal(100000m, Stock.stockFisicoProducto pId [ loteReservado ])

[<Fact>]
let ``// OBS-03: Despacho fisico consume stock fisico y libera la reserva asociada`` () =
    let pId = ProductoId (Guid.NewGuid())
    let lId = LoteId (Guid.NewGuid())
    let cod = unwrap (CodigoLote.generar "Cedrela" "odorata" (DateOnly(2026, 10, 1)) 1)
    let cantInicial = cantReconst 100m Kilogramo
    let lote = unwrap (Lote.crear lId cod pId None cantInicial (DateOnly(2026, 10, 1)) None None)

    // Reservar 40 kg
    let loteReservado = unwrap (Lote.reservar (cantReconst 40m Kilogramo) lote)

    // Despachar los 40 kg en almacén
    let loteDespachado = unwrap (Lote.despacharReserva (cantReconst 40m Kilogramo) loteReservado)

    // Stock físico ahora es 60 kg (60000 g), reserva liberada (0 g), disponible = 60 kg (60000 g)
    Assert.Equal(60000m, Stock.stockFisicoProducto pId [ loteDespachado ])
    Assert.Equal(60000m, Stock.disponibleParaVenta pId [ loteDespachado ])
    Assert.Equal(0m, Cantidad.aUnidadBase loteDespachado.CantidadReservada)
    Assert.Equal(60m, Cantidad.valor loteDespachado.CantidadActual)

[<Fact>]
let ``// OBS-03: Fifo.resolverFIFODespacho permite despachar lotes totalmente reservados mientras que Fifo.resolverFIFO bloquea nueva venta por stock insuficiente`` () =
    let pId = ProductoId (Guid.NewGuid())
    let lId = LoteId (Guid.NewGuid())
    let cod = unwrap (CodigoLote.generar "Cedrela" "odorata" (DateOnly(2026, 10, 1)) 1)
    let cantInicial = cantReconst 50m Kilogramo
    let lote = unwrap (Lote.crear lId cod pId None cantInicial (DateOnly(2026, 10, 1)) None None)

    // Lote con 50 kg se reserva al 100% (50 kg) por venta previa
    let loteTotalmenteReservado = unwrap (Lote.reservar (cantReconst 50m Kilogramo) lote)

    // 1. Una nueva proforma/venta intentando reservar 10 kg debe fallar (stock insuficiente disponible = 0)
    let reqNuevaVenta = cantReconst 10m Kilogramo
    match Fifo.resolverFIFO pId reqNuevaVenta [ loteTotalmenteReservado ] with
    | Error (StockInsuficiente msg) -> Assert.Contains("insuficiente", msg.ToLowerInvariant())
    | res -> failwithf "Se esperaba fallo por stock disponible agotado en resolverFIFO, se obtuvo: %A" res

    // 2. El despacho físico en almacén para la venta de 50 kg SÍ debe resolverse exitosamente porque el stock físico está en almacén
    let reqDespacho = cantReconst 50m Kilogramo
    match Fifo.resolverFIFODespacho pId reqDespacho [ loteTotalmenteReservado ] with
    | Ok lineas ->
        Assert.Single(lineas) |> ignore
        Assert.Equal(lId, lineas.[0].Referencia)
        Assert.Equal(50m, Cantidad.valor lineas.[0].Cantidad)
    | Error err -> failwithf "Se esperaba éxito en resolverFIFODespacho, se obtuvo: %A" err

// ─────────────────────────────────────────────────────────────
// Tests Rama 04: Anulación, Ownership y Validaciones (Q1 / RN04)
// ─────────────────────────────────────────────────────────────

let private crearOrdenVentaParaTest (responsableId: EmpleadoId) : OrdenVenta =
    let p = proformaVigente ()
    let hoy = DateOnly(2026, 10, 8)
    let ordenId = OrdenId (Guid.NewGuid())
    let codVenta = CodigoVenta.reconstruir "OV-202610-TEST"
    match OrdenVenta.crearDesdeProforma hoy ordenId codVenta p clienteId responsableId with
    | Ok (ov, _) -> ov
    | Error err -> failwithf "Error creando OrdenVenta: %A" err

let private crearActorConRol (rol: NombreRol) (empleadoId: EmpleadoId) : Actor =
    { UsuarioId = UsuarioId (Guid.NewGuid())
      EmpleadoId = empleadoId
      Roles = Set.singleton rol }

[<Theory>]
[<InlineData("Administrador", true, true)>]
[<InlineData("Administrador", false, true)>]
[<InlineData("Gerencia", true, true)>]
[<InlineData("Gerencia", false, true)>]
[<InlineData("Comercial", true, true)>]
[<InlineData("Comercial", false, false)>]
[<InlineData("Almacen", true, false)>]
[<InlineData("Almacen", false, false)>]
let ``// Q1 - puedeAnularVenta verifica ownership y escalamiento a Gerencia/Admin (8 casos: 4 roles x 2 ownership)`` (rolStr: string, esEmisor: bool, esperado: bool) =
    let emisorId = EmpleadoId (Guid.NewGuid())
    let otroId = EmpleadoId (Guid.NewGuid())
    let actorId = if esEmisor then emisorId else otroId
    let rol =
        match NombreRol.desdeTexto rolStr with
        | Some r -> r
        | None -> failwithf "Rol desconocido: %s" rolStr
    let actor = crearActorConRol rol actorId
    let venta = crearOrdenVentaParaTest emisorId

    let resultado = Venta.puedeAnularVenta actor venta
    if esperado then
        Assert.Equal(Ok (), resultado)
    else
        match resultado with
        | Error (PermisoDenegado _) -> ()
        | other -> failwithf "Se esperaba Error (PermisoDenegado _), se obtuvo: %A" other

[<Fact>]
let ``// Anulacion.validarMotivo rechaza cadenas vacias, espacios o nulos`` () =
    match Anulacion.validarMotivo "" with
    | Error (ValorRequerido msg) -> Assert.Contains("obligatorio", msg)
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

    match Anulacion.validarMotivo "   " with
    | Error (ValorRequerido msg) -> Assert.Contains("obligatorio", msg)
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

    match Anulacion.validarMotivo null with
    | Error (ValorRequerido msg) -> Assert.Contains("obligatorio", msg)
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

[<Fact>]
let ``// Anulacion.validarMotivo rechaza motivos con longitud menor a MotivoMinimoLength`` () =
    match Anulacion.validarMotivo "a" with
    | Error (ValorRequerido msg) -> Assert.Contains(sprintf "%d caracteres" Anulacion.MotivoMinimoLength, msg)
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

    match Anulacion.validarMotivo "ab" with
    | Error (ValorRequerido msg) -> Assert.Contains(sprintf "%d caracteres" Anulacion.MotivoMinimoLength, msg)
    | res -> failwithf "Se esperaba Error ValorRequerido, se obtuvo: %A" res

    match Anulacion.validarMotivo "  ab  " with
    | Error (ValorRequerido msg) -> Assert.Contains(sprintf "%d caracteres" Anulacion.MotivoMinimoLength, msg)
    | res -> failwithf "Se esperaba Error ValorRequerido con trim, se obtuvo: %A" res

[<Fact>]
let ``// Anulacion.validarMotivo acepta motivos de longitud minima o superior con trim`` () =
    match Anulacion.validarMotivo "abc" with
    | Ok m -> Assert.Equal("abc", m)
    | Error e -> failwithf "Se esperaba Ok, se obtuvo: %A" e

    match Anulacion.validarMotivo "  Error en digitación de cliente  " with
    | Ok m -> Assert.Equal("Error en digitación de cliente", m)
    | Error e -> failwithf "Se esperaba Ok, se obtuvo: %A" e

[<Fact>]
let ``// EstadoVenta.aTextoEstado retorna representacion de texto exacta para todos los casos`` () =
    Assert.Equal("Confirmada", EstadoVenta.aTextoEstado EstadoVenta.Confirmada)
    Assert.Equal("Despachada", EstadoVenta.aTextoEstado EstadoVenta.Despachada)
    let anulacion = { Fecha = DateTime.UtcNow; Motivo = "Prueba"; AnuladoPor = EmpleadoId Guid.Empty }
    Assert.Equal("Anulada", EstadoVenta.aTextoEstado (EstadoVenta.Anulada anulacion))

    // Roundtrip con desdeTexto
    match EstadoVenta.desdeTexto (EstadoVenta.aTextoEstado EstadoVenta.Confirmada) None with
    | Ok EstadoVenta.Confirmada -> ()
    | res -> failwithf "Roundtrip Confirmada falló: %A" res

    match EstadoVenta.desdeTexto (EstadoVenta.aTextoEstado EstadoVenta.Despachada) None with
    | Ok EstadoVenta.Despachada -> ()
    | res -> failwithf "Roundtrip Despachada falló: %A" res

    match EstadoVenta.desdeTexto (EstadoVenta.aTextoEstado (EstadoVenta.Anulada anulacion)) (Some anulacion) with
    | Ok (EstadoVenta.Anulada a) -> Assert.Equal("Prueba", a.Motivo)
    | res -> failwithf "Roundtrip Anulada falló: %A" res

[<Fact>]
let ``// EstadoDespacho.aTextoEstado retorna representacion de texto exacta para todos los casos`` () =
    Assert.Equal("Pendiente", EstadoDespacho.aTextoEstado EstadoDespacho.Pendiente)
    let movId = MovimientoId (Guid.NewGuid())
    Assert.Equal("Despachado", EstadoDespacho.aTextoEstado (EstadoDespacho.Despachado movId))
    let anulacion = { Fecha = DateTime.UtcNow; Motivo = "Prueba"; AnuladoPor = EmpleadoId Guid.Empty }
    Assert.Equal("Anulado", EstadoDespacho.aTextoEstado (EstadoDespacho.Anulado anulacion))

    // Roundtrip con desdeTexto
    match EstadoDespacho.desdeTexto (EstadoDespacho.aTextoEstado EstadoDespacho.Pendiente) None None with
    | Ok EstadoDespacho.Pendiente -> ()
    | res -> failwithf "Roundtrip Pendiente falló: %A" res

    match EstadoDespacho.desdeTexto (EstadoDespacho.aTextoEstado (EstadoDespacho.Despachado movId)) (Some (let (MovimientoId g) = movId in g)) None with
    | Ok (EstadoDespacho.Despachado m) -> Assert.Equal(movId, m)
    | res -> failwithf "Roundtrip Despachado falló: %A" res

    match EstadoDespacho.desdeTexto (EstadoDespacho.aTextoEstado (EstadoDespacho.Anulado anulacion)) None (Some anulacion) with
    | Ok (EstadoDespacho.Anulado a) -> Assert.Equal("Prueba", a.Motivo)
    | res -> failwithf "Roundtrip Anulado falló: %A" res
