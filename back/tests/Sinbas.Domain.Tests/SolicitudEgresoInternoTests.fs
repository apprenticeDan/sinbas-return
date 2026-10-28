module Sinbas.Domain.Tests.SolicitudEgresoInternoTests

open System
open Xunit
open Sinbas.Domain

// ─────────────────────────────────────────────────────────────
// Helpers de prueba
// ─────────────────────────────────────────────────────────────

let private crearCantidad valor unidad =
    unwrap (Cantidad.reconstruir valor unidad)

let private nuevoEmpleado () = EmpleadoId (Identidad.nuevo ())
let private nuevoProducto () = ProductoId (Identidad.nuevo ())
let private nuevoLote () = LoteId (Identidad.nuevo ())
let private nuevaSolicitudId () = SolicitudEgresoInternoId (Identidad.nuevo ())

// ─────────────────────────────────────────────────────────────
// 1. MotivoIngreso: DevolucionAnulacion
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``MotivoIngreso.aTexto retorna DevolucionAnulacion para devolucion por anulacion`` () =
    let ordenId = OrdenId (Identidad.nuevo ())
    let motivo = DevolucionAnulacion ordenId
    Assert.Equal("DevolucionAnulacion", MotivoIngreso.aTexto motivo)

[<Fact>]
let ``MotivoIngreso.desdeTexto resuelve DevolucionAnulacion con OrdenId correctamente`` () =
    let guid = Identidad.nuevo ()
    let res = MotivoIngreso.desdeTexto "DevolucionAnulacion" (Some guid) None
    match res with
    | Ok (DevolucionAnulacion (OrdenId oId)) -> Assert.Equal(guid, oId)
    | other -> failwithf "Se esperaba DevolucionAnulacion pero se obtuvo: %A" other

[<Fact>]
let ``MotivoIngreso.desdeTexto rechaza DevolucionAnulacion si no incluye contraparte_ref`` () =
    let res = MotivoIngreso.desdeTexto "DevolucionAnulacion" None None
    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("OrdenId", msg)
    | other -> failwithf "Se esperaba error ValorRequerido pero se obtuvo: %A" other

// ─────────────────────────────────────────────────────────────
// 2. MotivoEgreso: Mapeo Legado y DestinoUso
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``MotivoEgreso.aTexto retorna UsoInterno`` () =
    let motivo = UsoInterno (DestinoUso.Vivero, "Almacenero")
    Assert.Equal("UsoInterno", MotivoEgreso.aTexto motivo)

[<Fact>]
let ``MotivoEgreso.desdeTexto mapea texto legado UsoVivero a UsoInterno con destino Vivero`` () =
    let res = MotivoEgreso.desdeTexto "UsoVivero" None None None (Some "Juan Perez") None
    match res with
    | Ok (UsoInterno (destino, solicitante)) ->
        Assert.Equal(DestinoUso.Vivero, destino)
        Assert.Equal("Juan Perez", solicitante)
    | other -> failwithf "Se esperaba UsoInterno(Vivero) pero se obtuvo: %A" other

[<Fact>]
let ``MotivoEgreso.desdeTexto mapea texto legado UsoLabor a UsoInterno con destino Laboratorio`` () =
    let res = MotivoEgreso.desdeTexto "UsoLabor" None None None (Some "Dra. Gomez") None
    match res with
    | Ok (UsoInterno (destino, solicitante)) ->
        Assert.Equal(DestinoUso.Laboratorio, destino)
        Assert.Equal("Dra. Gomez", solicitante)
    | other -> failwithf "Se esperaba UsoInterno(Laboratorio) pero se obtuvo: %A" other

[<Fact>]
let ``MotivoEgreso.desdeTexto con UsoInterno resuelve destino por departamento u observaciones`` () =
    // Depto vivero
    let resVivero = MotivoEgreso.desdeTexto "UsoInterno" None None (Some "Vivero") (Some "Carlos") None
    match resVivero with
    | Ok (UsoInterno (DestinoUso.Vivero, "Carlos")) -> ()
    | other -> failwithf "Fallo mapeo Vivero: %A" other

    // Depto laboratorio
    let resLab = MotivoEgreso.desdeTexto "UsoInterno" None None (Some "Laboratorio") (Some "Maria") None
    match resLab with
    | Ok (UsoInterno (DestinoUso.Laboratorio, "Maria")) -> ()
    | other -> failwithf "Fallo mapeo Laboratorio: %A" other

    // Destino otro
    let resOtro = MotivoEgreso.desdeTexto "UsoInterno" None None (Some "Investigacion") (Some "Ana") None
    match resOtro with
    | Ok (UsoInterno (DestinoUso.Otro "Investigacion", "Ana")) -> ()
    | other -> failwithf "Fallo mapeo Otro: %A" other

[<Fact>]
let ``MotivoEgreso.desdeTexto preserva motivos persistidos sin alteracion`` () =
    let cliId = Identidad.nuevo ()
    let labId = Identidad.nuevo ()
    let truId = Identidad.nuevo ()

    Assert.Equal(Ok (Venta (ClienteId cliId)), MotivoEgreso.desdeTexto "Venta" (Some cliId) None None None None)
    Assert.Equal(Ok (MuestraLab (LaboratorioId labId)), MotivoEgreso.desdeTexto "MuestraLab" (Some labId) None None None None)
    Assert.Equal(Ok (Merma "Semilla dañada"), MotivoEgreso.desdeTexto "Merma" None None None None (Some "Semilla dañada"))
    Assert.Equal(Ok (DonacionEnviada "Escuela"), MotivoEgreso.desdeTexto "Donacion" None (Some "Escuela") None None None)
    Assert.Equal(Ok (TruequeSalida (TruequeId truId)), MotivoEgreso.desdeTexto "Trueque" (Some truId) None None None None)

// ─────────────────────────────────────────────────────────────
// 3. Invariantes puras de SolicitudEgresoInterno
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``SolicitudEgresoInterno.crear valida creacion correcta en estado Pendiente`` () =
    let solId = nuevaSolicitudId ()
    let solicitante = nuevoEmpleado ()
    let loteId = nuevoLote ()
    let cant = crearCantidad 100m Gramo
    let creado = DateTime.UtcNow

    let res =
        SolicitudEgresoInterno.crear
            solId
            TipoEgresoInterno.Ajuste
            SignoEgresoInterno.Negativo
            None
            (Some loteId)
            cant
            "Diferencia de pesaje en inventario físico"
            solicitante
            creado

    match res with
    | Ok sol ->
        Assert.Equal(solId, sol.Id)
        Assert.Equal(TipoEgresoInterno.Ajuste, sol.Tipo)
        Assert.Equal(SignoEgresoInterno.Negativo, sol.Signo)
        Assert.Equal(EstadoSolicitudInterna.Pendiente, sol.Estado)
        Assert.Equal(solicitante, sol.Solicitante)
        Assert.Equal("Diferencia de pesaje en inventario físico", sol.Motivo)
    | Error err -> failwithf "No debió fallar: %A" err

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData(null)>]
let ``Invariante: motivo obligatorio es rechazado si es vacio o nulo`` (motivoInvalido: string) =
    let res =
        SolicitudEgresoInterno.crear
            (nuevaSolicitudId ())
            TipoEgresoInterno.Merma
            SignoEgresoInterno.Negativo
            None
            (Some (nuevoLote ()))
            (crearCantidad 50m Gramo)
            motivoInvalido
            (nuevoEmpleado ())
            DateTime.UtcNow

    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("motivo", msg, StringComparison.OrdinalIgnoreCase)
    | other -> failwithf "Se esperaba ValorRequerido pero se obtuvo: %A" other

[<Fact>]
let ``Invariante: cantidad debe ser mayor a cero y rechaza cantidad cero`` () =
    let res =
        SolicitudEgresoInterno.crear
            (nuevaSolicitudId ())
            TipoEgresoInterno.Ajuste
            SignoEgresoInterno.Positivo
            None
            (Some (nuevoLote ()))
            (Cantidad.cero Gramo)
            "Ajuste por conteo"
            (nuevoEmpleado ())
            DateTime.UtcNow

    match res with
    | Error (CantidadInvalida msg) -> Assert.Contains("mayor a cero", msg)
    | other -> failwithf "Se esperaba CantidadInvalida pero se obtuvo: %A" other

[<Fact>]
let ``Invariante: la solicitud debe especificar al menos un producto o un lote`` () =
    let res =
        SolicitudEgresoInterno.crear
            (nuevaSolicitudId ())
            TipoEgresoInterno.Ajuste
            SignoEgresoInterno.Positivo
            None
            None // Sin producto ni lote
            (crearCantidad 10m Gramo)
            "Ajuste sin referencia"
            (nuevoEmpleado ())
            DateTime.UtcNow

    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("producto o un lote", msg)
    | other -> failwithf "Se esperaba ValorRequerido pero se obtuvo: %A" other

[<Fact>]
let ``Invariante: Ajuste Positivo requiere LoteId obligatorio`` () =
    let solId = SolicitudEgresoInternoId (Identidad.nuevo ())
    let prodId = ProductoId (Identidad.nuevo ())
    let cant = crearCantidad 2m Kilogramo
    let r =
        SolicitudEgresoInterno.crear
            solId
            TipoEgresoInterno.Ajuste
            Positivo
            (Some prodId)
            None // ← sin lote
            cant
            "Sobrante de inventario"
            (nuevoEmpleado ())
            DateTime.UtcNow
    match r with
    | Error (ValorRequerido msg) when msg.Contains "Ajuste Positivo" -> ()
    | Error e -> failwithf "esperaba ValorRequerido, recibí %A" e
    | Ok _ -> failwith "esperaba error"

[<Fact>]
let ``Invariante: UsoInterno y Merma no pueden crearse con signo positivo`` () =
    let loteId = nuevoLote ()
    let emp = nuevoEmpleado ()

    let resUsoPositivo =
        SolicitudEgresoInterno.crear
            (nuevaSolicitudId ())
            (TipoEgresoInterno.UsoInterno DestinoUso.Vivero)
            SignoEgresoInterno.Positivo
            None
            (Some loteId)
            (crearCantidad 10m Gramo)
            "Uso interno invalido"
            emp
            DateTime.UtcNow

    match resUsoPositivo with
    | Error (OperacionInvalida msg) -> Assert.Contains("positivo", msg)
    | other -> failwithf "Se esperaba OperacionInvalida pero se obtuvo: %A" other

    let resMermaPositiva =
        SolicitudEgresoInterno.crear
            (nuevaSolicitudId ())
            TipoEgresoInterno.Merma
            SignoEgresoInterno.Positivo
            None
            (Some loteId)
            (crearCantidad 10m Gramo)
            "Merma positiva invalida"
            emp
            DateTime.UtcNow

    match resMermaPositiva with
    | Error (OperacionInvalida msg) -> Assert.Contains("positivo", msg)
    | other -> failwithf "Se esperaba OperacionInvalida pero se obtuvo: %A" other

// ─────────────────────────────────────────────────────────────
// 4. Invariantes de Aprobación y Rechazo (RN11)
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``Invariante RN11: El aprobador no puede ser el mismo usuario que registro la solicitud`` () =
    let usuario = nuevoEmpleado ()
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 50m Gramo)
                "Ajuste por merma física"
                usuario
                DateTime.UtcNow
        )

    let res = SolicitudEgresoInterno.aprobar usuario false DateTime.UtcNow sol
    match res with
    | Error (OperacionInvalida msg) ->
        Assert.Contains("RN11", msg)
        Assert.Contains("mismo usuario", msg)
    | other -> failwithf "FALLO RN11: Se permitió auto-aprobación del solicitante: %A" other

[<Fact>]
let ``RN11 exento: Gerencia y Admin pueden auto-aprobar su propia solicitud`` () =
    let usuario = nuevoEmpleado ()
    let fecha = DateTime.UtcNow
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 50m Gramo)
                "Ajuste por merma física"
                usuario
                DateTime.UtcNow
        )

    match SolicitudEgresoInterno.aprobar usuario true fecha sol with
    | Ok aprobada ->
        Assert.Equal(EstadoSolicitudInterna.Aprobada { Aprobador = usuario; Fecha = fecha; Motivo = None }, aprobada.Estado)
    | Error err ->
        failwithf "Debió permitir auto-aprobación con esExento=true: %A" err

[<Fact>]
let ``Aprobador distinto aprueba exitosamente solicitud pendiente de ajuste o merma`` () =
    let solicitante = nuevoEmpleado ()
    let gerente = nuevoEmpleado ()
    let fechaAprob = DateTime.UtcNow

    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Merma
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 30m Gramo)
                "Semilla atacada por insectos"
                solicitante
                DateTime.UtcNow
        )

    let res = SolicitudEgresoInterno.aprobar gerente false fechaAprob sol
    match res with
    | Ok aprobada ->
        match aprobada.Estado with
        | EstadoSolicitudInterna.Aprobada resolucion ->
            Assert.Equal(gerente, resolucion.Aprobador)
            Assert.Equal(fechaAprob, resolucion.Fecha)
            Assert.True(Option.isNone resolucion.Motivo)
        | other -> failwithf "Se esperaba estado Aprobada pero se obtuvo: %A" other
    | Error err -> failwithf "No debió fallar: %A" err

[<Theory>]
[<InlineData("")>]
[<InlineData("   ")>]
[<InlineData(null)>]
let ``Invariante: rechazo exige obligatoriamente un motivo justificado`` (motivoVacio: string) =
    let solicitante = nuevoEmpleado ()
    let gerente = nuevoEmpleado ()
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Positivo
                None
                (Some (nuevoLote ()))
                (crearCantidad 100m Gramo)
                "Conteo sobrante"
                solicitante
                DateTime.UtcNow
        )

    let res = SolicitudEgresoInterno.rechazar gerente motivoVacio DateTime.UtcNow sol
    match res with
    | Error (ValorRequerido msg) -> Assert.Contains("motivo justificado", msg)
    | other -> failwithf "Se esperaba ValorRequerido pero se obtuvo: %A" other

[<Fact>]
let ``Rechazo con motivo valido cambia estado a Rechazada`` () =
    let solicitante = nuevoEmpleado ()
    let gerente = nuevoEmpleado ()
    let fecha = DateTime.UtcNow
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Positivo
                None
                (Some (nuevoLote ()))
                (crearCantidad 100m Gramo)
                "Conteo sobrante"
                solicitante
                DateTime.UtcNow
        )

    let res = SolicitudEgresoInterno.rechazar gerente "No corresponde ajuste positivo sin pesaje oficial" fecha sol
    match res with
    | Ok rechazada ->
        match rechazada.Estado with
        | EstadoSolicitudInterna.Rechazada resolucion ->
            Assert.Equal(gerente, resolucion.Aprobador)
            Assert.Equal(fecha, resolucion.Fecha)
            Assert.Equal(Some "No corresponde ajuste positivo sin pesaje oficial", resolucion.Motivo)
        | other -> failwithf "Se esperaba estado Rechazada pero se obtuvo: %A" other
    | Error err -> failwithf "No debió fallar: %A" err

// ─────────────────────────────────────────────────────────────
// 5. Invariantes de Transición de Estados
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``No se puede aprobar ni rechazar una solicitud que ya fue aprobada`` () =
    let solicitante = nuevoEmpleado ()
    let gerente1 = nuevoEmpleado ()
    let gerente2 = nuevoEmpleado ()
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 15m Gramo)
                "Ajuste"
                solicitante
                DateTime.UtcNow
        )

    let aprobada = unwrap (SolicitudEgresoInterno.aprobar gerente1 false DateTime.UtcNow sol)

    // Re-aprobar
    match SolicitudEgresoInterno.aprobar gerente2 false DateTime.UtcNow aprobada with
    | Error (OperacionInvalida msg) -> Assert.Contains("ya se encuentra aprobada", msg)
    | other -> failwithf "Fallo: %A" other

    // Rechazar ya aprobada
    match SolicitudEgresoInterno.rechazar gerente2 "Rechazo tardio" DateTime.UtcNow aprobada with
    | Error (OperacionInvalida msg) -> Assert.Contains("ya fue aprobada", msg)
    | other -> failwithf "Fallo: %A" other

[<Fact>]
let ``No se puede aprobar ni rechazar una solicitud que ya fue rechazada`` () =
    let solicitante = nuevoEmpleado ()
    let gerente = nuevoEmpleado ()
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Ajuste
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 15m Gramo)
                "Ajuste"
                solicitante
                DateTime.UtcNow
        )

    let rechazada = unwrap (SolicitudEgresoInterno.rechazar gerente "Rechazado" DateTime.UtcNow sol)

    // Aprobar ya rechazada
    match SolicitudEgresoInterno.aprobar gerente false DateTime.UtcNow rechazada with
    | Error (OperacionInvalida msg) -> Assert.Contains("solicitud rechazada", msg)
    | other -> failwithf "Fallo: %A" other

    // Re-rechazar
    match SolicitudEgresoInterno.rechazar gerente "Otro motivo" DateTime.UtcNow rechazada with
    | Error (OperacionInvalida msg) -> Assert.Contains("ya se encuentra rechazada", msg)
    | other -> failwithf "Fallo: %A" other

[<Fact>]
let ``UsoInterno no requiere aprobacion previa y se confirma directamente al entregar`` () =
    let solicitante = nuevoEmpleado ()
    let almacenero = nuevoEmpleado ()
    let fecha = DateTime.UtcNow
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                (TipoEgresoInterno.UsoInterno DestinoUso.Vivero)
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 200m Gramo)
                "Siembra en canteros vivero central"
                solicitante
                DateTime.UtcNow
        )

    // Aprobar debe fallar (uso interno no requiere aprobación)
    match SolicitudEgresoInterno.aprobar almacenero false fecha sol with
    | Error (OperacionInvalida msg) -> Assert.Contains("confirmación física", msg)
    | other -> failwithf "Se esperaba que UsoInterno no admitiera aprobación: %A" other

    // Confirmar directamente desde Pendiente debe tener éxito
    match SolicitudEgresoInterno.confirmar almacenero fecha sol with
    | Ok confirmada ->
        match confirmada.Estado with
        | EstadoSolicitudInterna.Confirmada entrega ->
            Assert.Equal(almacenero, entrega.EntregadoPor)
            Assert.Equal(fecha, entrega.Fecha)
        | other -> failwithf "Se esperaba estado Confirmada pero se obtuvo: %A" other
    | Error err -> failwithf "No debió fallar confirmación directa: %A" err

[<Fact>]
let ``Ajuste o Merma no puede confirmarse directamente desde Pendiente sin aprobacion previa`` () =
    let solicitante = nuevoEmpleado ()
    let almacenero = nuevoEmpleado ()
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Merma
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 50m Gramo)
                "Merma fisica"
                solicitante
                DateTime.UtcNow
        )

    match SolicitudEgresoInterno.confirmar almacenero DateTime.UtcNow sol with
    | Error (OperacionInvalida msg) -> Assert.Contains("aprobación gerencial previa", msg)
    | other -> failwithf "FALLO: Se confirmó merma sin aprobación previa: %A" other

[<Fact>]
let ``Ajuste o Merma aprobada se confirma exitosamente al ejecutarse`` () =
    let solicitante = nuevoEmpleado ()
    let gerente = nuevoEmpleado ()
    let almacenero = nuevoEmpleado ()
    let fecha = DateTime.UtcNow
    let sol =
        unwrap (
            SolicitudEgresoInterno.crear
                (nuevaSolicitudId ())
                TipoEgresoInterno.Merma
                SignoEgresoInterno.Negativo
                None
                (Some (nuevoLote ()))
                (crearCantidad 50m Gramo)
                "Merma fisica autorizada"
                solicitante
                DateTime.UtcNow
        )

    let aprobada = unwrap (SolicitudEgresoInterno.aprobar gerente false fecha sol)
    let res = SolicitudEgresoInterno.confirmar almacenero fecha aprobada
    match res with
    | Ok confirmada ->
        match confirmada.Estado with
        | EstadoSolicitudInterna.Confirmada entrega ->
            Assert.Equal(almacenero, entrega.EntregadoPor)
            Assert.Equal(fecha, entrega.Fecha)
        | other -> failwithf "Se esperaba estado Confirmada pero se obtuvo: %A" other
    | Error err -> failwithf "No debió fallar confirmación de merma aprobada: %A" err
