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
    let bloqueado = unwrap (Lote.bloquear "Alerta de plaga por muestra de laboratorio" lote)

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

// ─────────────────────────────────────────────────────────────
// Tests OBS-12: Máquina de Estados de Lote y Validaciones de Precondición
// ─────────────────────────────────────────────────────────────

[<Fact>]
let ``OBS-12: Lote.crearEnCuarentena inicia lote en estado EnCuarentena`` () =
    let codigo = unwrap (CodigoLote.generar "Swietenia" "macrophylla" (DateOnly(2026, 8, 15)) 1)
    let cant = unwrap (Cantidad.reconstruir 30m Kilogramo)
    let lote = unwrap (Lote.crearEnCuarentena loteId codigo prodId (Some "Origen") cant (DateOnly(2026, 8, 15)) None None)

    Assert.Equal(EnCuarentena, lote.Estado)
    Assert.False(Lote.estaActivo lote)
    Assert.False(EstadoLote.esVendible lote.Estado)

[<Fact>]
let ``OBS-12: EstadoLote.esVendible solo es verdadero para Activo`` () =
    Assert.True(EstadoLote.esVendible Activo)
    Assert.False(EstadoLote.esVendible EnCuarentena)
    Assert.False(EstadoLote.esVendible Agotado)
    Assert.False(EstadoLote.esVendible Bloqueado)
    Assert.False(EstadoLote.esVendible Rechazado)
    Assert.False(EstadoLote.esVendible Archivado)

[<Fact>]
let ``OBS-12: EstadoLote.puedeTransicionar valida el grafo de estados correctamente`` () =
    // Transiciones válidas
    Assert.True(EstadoLote.puedeTransicionar EnCuarentena Activo)
    Assert.True(EstadoLote.puedeTransicionar EnCuarentena Rechazado)
    Assert.True(EstadoLote.puedeTransicionar EnCuarentena Bloqueado)
    Assert.True(EstadoLote.puedeTransicionar Activo Bloqueado)
    Assert.True(EstadoLote.puedeTransicionar Activo Agotado)
    Assert.True(EstadoLote.puedeTransicionar Activo EnCuarentena)
    Assert.True(EstadoLote.puedeTransicionar Activo Archivado)
    Assert.True(EstadoLote.puedeTransicionar Bloqueado Activo)
    Assert.True(EstadoLote.puedeTransicionar Bloqueado Rechazado)
    Assert.True(EstadoLote.puedeTransicionar Rechazado Archivado)
    Assert.True(EstadoLote.puedeTransicionar Agotado Activo)
    Assert.True(EstadoLote.puedeTransicionar Agotado Archivado)

    // Transiciones ilegales
    Assert.False(EstadoLote.puedeTransicionar Rechazado Activo)
    Assert.False(EstadoLote.puedeTransicionar Rechazado Bloqueado)
    Assert.False(EstadoLote.puedeTransicionar Archivado EnCuarentena)
    Assert.False(EstadoLote.puedeTransicionar Archivado Bloqueado)

[<Fact>]
let ``OBS-12: Lote.bloquear rechaza motivos vacios y doble bloqueo`` () =
    let lote = crearLoteValido ()

    // Motivo vacío
    match Lote.bloquear "   " lote with
    | Error (ValorRequerido msg) -> Assert.Contains("motivo", msg)
    | res -> failwithf "Debió rechazar motivo vacío: %A" res

    // Bloqueo exitoso
    let bloqueado = unwrap (Lote.bloquear "Sospecha de hongos" lote)
    Assert.Equal(Bloqueado, bloqueado.Estado)

    // Intento de re-bloqueo
    match Lote.bloquear "Otro motivo" bloqueado with
    | Error (OperacionInvalida msg) -> Assert.Contains("ya se encuentra en estado Bloqueado", msg)
    | res -> failwithf "Debió impedir doble bloqueo: %A" res

[<Fact>]
let ``OBS-12: Lote.bloquear impide bloquear lotes rechazados o archivados`` () =
    let lote = crearLoteValido ()
    let gerente = EmpleadoId (Guid.NewGuid())
    let loteCuarentena = Lote.aplicarDictamenLaboratorio (DictamenCalidad.Observado "Falla") lote
    let rechazado = unwrap (Lote.rechazarDefinitivamente "Descarte definitivo" gerente loteCuarentena)
    let archivado = unwrap (Lote.archivar None rechazado)

    match Lote.bloquear "Alerta" rechazado with
    | Error (OperacionInvalida msg) -> Assert.Contains("rechazado definitivamente", msg)
    | res -> failwithf "Debió impedir bloquear lote rechazado: %A" res

    match Lote.bloquear "Alerta" archivado with
    | Error (OperacionInvalida msg) -> Assert.Contains("archivado", msg)
    | res -> failwithf "Debió impedir bloquear lote archivado: %A" res

[<Fact>]
let ``OBS-12: Lote.desbloquear reactiva a Activo si hay saldo o Agotado si saldo es cero`` () =
    let lote = crearLoteValido ()
    let bloqueadoConSaldo = unwrap (Lote.bloquear "Verificación técnica" lote)

    // Desbloqueo de lote con saldo -> pasa a Activo
    let desbloqueado = unwrap (Lote.desbloquear "Análisis fitosanitario limpio" bloqueadoConSaldo)
    Assert.Equal(Activo, desbloqueado.Estado)
    Assert.True(Lote.estaActivo desbloqueado)
    Assert.Contains("[DESBLOQUEADO]: Análisis fitosanitario limpio", desbloqueado.Observaciones.Value)

    // Desbloqueo de lote sin saldo -> pasa a Agotado
    let loteSinSaldo = unwrap (Lote.actualizarSaldo 0m lote)
    let bloqueadoSinSaldo = unwrap (Lote.bloquear "Revisión física" loteSinSaldo)
    let desbloqueadoAgotado = unwrap (Lote.desbloquear "Conteo conforme" bloqueadoSinSaldo)
    Assert.Equal(Agotado, desbloqueadoAgotado.Estado)

    // Error si se intenta desbloquear un lote que no estaba bloqueado
    match Lote.desbloquear "Intento inválido" lote with
    | Error (OperacionInvalida msg) -> Assert.Contains("Solo un lote en estado 'Bloqueado'", msg)
    | res -> failwithf "Debió rechazar desbloqueo de lote no bloqueado: %A" res

[<Fact>]
let ``OBS-12: Lote.archivar permite archivar lotes agotados o rechazados`` () =
    let lote = crearLoteValido ()
    let gerente = EmpleadoId (Guid.NewGuid())
    let loteCuarentena = Lote.aplicarDictamenLaboratorio (DictamenCalidad.Observado "Falla") lote
    let rechazado = unwrap (Lote.rechazarDefinitivamente "Descarte final" gerente loteCuarentena)

    // Archivar lote rechazado
    let archivadoRechazado = unwrap (Lote.archivar (Some "Cierre de expediente") rechazado)
    Assert.Equal(Archivado, archivadoRechazado.Estado)
    Assert.Contains("[ARCHIVADO]: Cierre de expediente", archivadoRechazado.Observaciones.Value)

    // Archivar lote agotado
    let agotado = unwrap (Lote.actualizarSaldo 0m lote)
    let archivadoAgotado = unwrap (Lote.archivar None agotado)
    Assert.Equal(Archivado, archivadoAgotado.Estado)

    // Rechaza archivar lote en cuarentena o bloqueado
    match Lote.archivar None loteCuarentena with
    | Error (OperacionInvalida msg) -> Assert.Contains("EnCuarentena", msg)
    | res -> failwithf "Debió impedir archivar lote en cuarentena: %A" res

[<Fact>]
let ``OBS-12: Lote.archivar exige justificacion formal si el lote esta Activo con saldo`` () =
    let lote = crearLoteValido ()

    // Sin justificación en lote activo con saldo
    match Lote.archivar None lote with
    | Error (OperacionInvalida msg) -> Assert.Contains("justificación formal", msg)
    | res -> failwithf "Debió exigir justificación para archivar lote activo: %A" res

    // Con justificación formal (ej. muestra testigo)
    let archivadoTestigo = unwrap (Lote.archivar (Some "Muestra testigo permanente de colección biológica") lote)
    Assert.Equal(Archivado, archivadoTestigo.Estado)
    Assert.Contains("ARCHIVADO (Muestra Testigo)", archivadoTestigo.Observaciones.Value)
