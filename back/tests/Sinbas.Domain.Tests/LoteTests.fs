module LoteTests

open System
open Xunit
open Sinbas.Domain

let prodGuid = Guid.Parse("01917f3a-0003-7000-8000-000000000001")
let prodId = ProductoId prodGuid

let sampleLoteGuid = Guid.Parse("01917f3a-0004-7000-8000-000000000001")
let loteId = LoteId sampleLoteGuid

let crearLoteValido () =
    let codigo =
        match CodigoLote.generar "Swietenia" "macrophylla" (DateOnly(2026, 8, 15)) 1 with
        | Ok c -> c
        | Error e -> failwithf "Error generando código de lote: %A" e

    let cantInicial = unwrap (Cantidad.reconstruir 50m Kilogramo)

    match Lote.crear loteId codigo prodId (Some "Bosque Chiquitano") cantInicial (DateOnly(2026, 8, 15)) (Some "Almacén Central") (Some "Observación inicial") with
    | Ok l -> l
    | Error e -> failwithf "Error creando lote: %A" e

[<Fact>]
let ``Creacion de Lote valido inicia en estado Activo con CantidadInicial y CantidadActual identicas`` () =
    let lote = crearLoteValido ()
    Assert.Equal(Activo, lote.Estado)
    Assert.True(Lote.estaActivo lote)
    Assert.Equal(50m, Cantidad.valor lote.CantidadInicial)
    Assert.Equal(50m, Cantidad.valor lote.CantidadActual)
    Assert.Equal(Kilogramo, Cantidad.unidad lote.CantidadInicial)
    Assert.Equal(Some "Bosque Chiquitano", lote.Procedencia)

[<Fact>]
let ``Descuento parcial de stock preserva estado Activo`` () =
    let lote = crearLoteValido ()
    let aDescontar = unwrap (Cantidad.reconstruir 10m Kilogramo)

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteActualizado ->
        Assert.Equal(Activo, loteActualizado.Estado)
        Assert.Equal(40m, Cantidad.valor loteActualizado.CantidadActual)
        Assert.Equal(Kilogramo, Cantidad.unidad loteActualizado.CantidadActual)

[<Fact>]
let ``Descuento total de stock cambia automaticamente estado a Agotado`` () =
    let lote = crearLoteValido ()
    let aDescontar = unwrap (Cantidad.reconstruir 50m Kilogramo)

    match Lote.descontarStock aDescontar lote with
    | Error e -> failwithf "Falló descuento: %A" e
    | Ok loteAgotado ->
        Assert.Equal(Agotado, loteAgotado.Estado)
        Assert.Equal(0m, Cantidad.valor loteAgotado.CantidadActual)
        Assert.False(Lote.estaActivo loteAgotado)

[<Fact>]
let ``Intento de descontar mas stock del disponible retorna error StockInsuficiente`` () =
    let lote = crearLoteValido ()
    let aDescontar = unwrap (Cantidad.reconstruir 60m Kilogramo)

    match Lote.descontarStock aDescontar lote with
    | Error (StockInsuficiente msg) -> Assert.Contains("insuficiente", msg)
    | res -> failwithf "Debería haber fallado por stock insuficiente: %A" res

[<Fact>]
let ``Lote.actualizarSaldo actualiza saldo y transiciona bidireccionalmente entre Activo y Agotado`` () =
    let lote = crearLoteValido ()
    let agotado = unwrap (Lote.actualizarSaldo 0m lote)
    Assert.Equal(Agotado, agotado.Estado)
    Assert.Equal(0m, Cantidad.valor agotado.CantidadActual)

    let reactivado = unwrap (Lote.actualizarSaldo 1500m agotado)
    Assert.Equal(Activo, reactivado.Estado)
    Assert.Equal(1.5m, Cantidad.valor reactivado.CantidadActual)
    Assert.Equal(Kilogramo, Cantidad.unidad reactivado.CantidadActual)

[<Fact>]
let ``Bloqueo de lote acumula observaciones de justificacion inmutablemente`` () =
    let lote = crearLoteValido ()
    let bloqueado = Lote.bloquear "Alerta de plaga por muestra de laboratorio" lote

    Assert.Equal(Bloqueado, bloqueado.Estado)
    Assert.False(Lote.estaActivo bloqueado)
    Assert.Contains("[BLOQUEADO]: Alerta de plaga", bloqueado.Observaciones.Value)

[<Fact>]
let ``Lote.solicitarNuevoAnalisis registra instruccion gerencial y preserva EnCuarentena`` () =
    let lote = crearLoteValido ()
    let loteCuarentena = Lote.aplicarDictamenLaboratorio (DictamenCalidad.Observado "Humedad alta") lote
    let gerente = EmpleadoId (Guid.NewGuid())

    match Lote.solicitarNuevoAnalisis "Repetir prueba tras 5 días de secado en cámara" gerente loteCuarentena with
    | Error err -> failwithf "Fallo solicitarNuevoAnalisis: %A" err
    | Ok loteActualizado ->
        Assert.Equal(EnCuarentena, loteActualizado.Estado)
        Assert.Contains("REANÁLISIS SOLICITADO", loteActualizado.Observaciones.Value)
        Assert.Contains("secado en cámara", loteActualizado.Observaciones.Value)

[<Fact>]
let ``Lote.liberarCuarentena rechaza operacion si el lote no esta EnCuarentena o justificacion esta vacia`` () =
    let lote = crearLoteValido ()
    let gerente = EmpleadoId (Guid.NewGuid())

    // Caso 1: Lote en estado Activo no puede ser liberado
    match Lote.liberarCuarentena "Justificación válida" gerente lote with
    | Error (OperacionInvalida msg) -> Assert.Contains("Solo un lote en estado 'EnCuarentena'", msg)
    | res -> failwithf "Debió rechazar liberación de lote Activo: %A" res

    // Caso 2: Justificación vacía en lote EnCuarentena
    let loteCuarentena = Lote.aplicarDictamenLaboratorio (DictamenCalidad.Observado "Falla") lote
    match Lote.liberarCuarentena "  " gerente loteCuarentena with
    | Error (ValorRequerido msg) -> Assert.Contains("justificación formal", msg)
    | res -> failwithf "Debió exigir justificación no vacía: %A" res

[<Fact>]
let ``Lote.rechazarDefinitivamente pasa a Rechazado y no permite doble rechazo`` () =
    let lote = crearLoteValido ()
    let loteCuarentena = Lote.aplicarDictamenLaboratorio (DictamenCalidad.Observado "Falla") lote
    let gerente = EmpleadoId (Guid.NewGuid())

    match Lote.rechazarDefinitivamente "Semillas con viabilidad nula" gerente loteCuarentena with
    | Error err -> failwithf "Fallo rechazo: %A" err
    | Ok loteRechazado ->
        Assert.Equal(Rechazado, loteRechazado.Estado)

        // Intento de volver a rechazar
        match Lote.rechazarDefinitivamente "Otro motivo" gerente loteRechazado with
        | Error (OperacionInvalida msg) -> Assert.Contains("ya se encuentra en estado Rechazado", msg)
        | res -> failwithf "Debió impedir doble rechazo: %A" res

[<Fact>]
let ``EstadoLote.desdeTexto reconoce todos los estados validos`` () =
    Assert.Equal(Ok Activo, EstadoLote.desdeTexto "Activo")
    Assert.Equal(Ok EnCuarentena, EstadoLote.desdeTexto "EnCuarentena")
    Assert.Equal(Ok Agotado, EstadoLote.desdeTexto "Agotado")
    Assert.Equal(Ok Bloqueado, EstadoLote.desdeTexto "Bloqueado")
    Assert.Equal(Ok Rechazado, EstadoLote.desdeTexto "Rechazado")
    Assert.Equal(Ok Archivado, EstadoLote.desdeTexto "Archivado")

[<Theory>]
[<InlineData("Inexistente")>]
[<InlineData("activo")>] // case sensitivity o sin trim
[<InlineData("")>]
[<InlineData("  ")>]
[<InlineData(null)>]
let ``EstadoLote.desdeTexto rechaza valores desconocidos o nulos y nunca inventa Activo`` (input: string) =
    match EstadoLote.desdeTexto input with
    | Error (ValorRequerido _) -> () // Éxito: el dominio rechaza
    | Ok estado -> failwithf "FALLO DE SEGURIDAD: EstadoLote.desdeTexto fabricó el estado %A para entrada '%s'" estado input
    | Error otroError -> failwithf "Retornó error no esperado: %A" otroError
