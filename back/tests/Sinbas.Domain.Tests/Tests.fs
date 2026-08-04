module Tests

open System
open Xunit
open Sinbas.Domain

let makeLote id prodId fecha estado =
    let codigo =
        match CodigoLote.desdeString (sprintf "TEST_%03d-02601-01" id) with
        | Ok c -> c
        | Error e -> failwithf "Error generando codigo: %A" e
    { Id = LoteId id
      Codigo = codigo
      ProductoId = ProductoId prodId
      FechaIngreso = fecha
      UbicacionId = None
      Estado = estado
      Observaciones = None }

let makeMovimiento id fecha tipo lineas =
    { Id = MovimientoId id
      Fecha = fecha
      Responsable = EmpleadoId 1
      Tipo = tipo
      OrdenOrigen = None
      Lineas = lineas
      Observaciones = None }

let makeLinea loteId gramos =
    { Referencia = LoteId loteId
      Cantidad = { Valor = gramos; Unidad = Gramo } }

[<Fact>]
let ``Calculo de stock para lote individual calcula entradas y salidas correctamente`` () =
    let loteId = 10
    let prodId = 1
    
    let movimientos = [
        makeMovimiento 101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [makeLinea loteId 100m]
        makeMovimiento 102 DateTime.Now (Salida (Merma "Deterioro")) [makeLinea loteId 30m]
    ]
    
    let stock = Stock.cantidadLote (LoteId loteId) movimientos
    Assert.Equal(70m, stock)

[<Fact>]
let ``Stock total del producto suma solo lotes activos`` () =
    let prodId = 1
    let loteActivo = makeLote 10 prodId (DateOnly(2026, 1, 1)) Activo
    let loteAgotado = makeLote 11 prodId (DateOnly(2026, 1, 5)) Agotado
    let loteBloqueado = makeLote 12 prodId (DateOnly(2026, 1, 10)) Bloqueado
    
    let lotes = [loteActivo; loteAgotado; loteBloqueado]
    
    let movimientos = [
        makeMovimiento 101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [
            makeLinea 10 100m
            makeLinea 11 50m
            makeLinea 12 80m
        ]
    ]
    
    // stockProducto solo debe sumar el lote activo (lote 10, con 100g)
    let stockTotal = Stock.stockProducto (ProductoId prodId) lotes movimientos
    Assert.Equal(100m, stockTotal)

[<Fact>]
let ``Resolver FIFO asigna lotes en orden cronologico de ingreso`` () =
    let prodId = 1
    
    // Lotes activos con diferentes fechas de ingreso: A (1-Ene), C (5-Ene), B (10-Ene)
    let loteA = makeLote 10 prodId (DateOnly(2026, 1, 1)) Activo
    let loteB = makeLote 11 prodId (DateOnly(2026, 1, 10)) Activo
    let loteC = makeLote 12 prodId (DateOnly(2026, 1, 5)) Activo
    
    let lotes = [loteB; loteA; loteC] // desordenado intencionalmente
    
    let movimientos = [
        makeMovimiento 101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [
            makeLinea 10 100m
            makeLinea 12 150m
            makeLinea 11 200m
        ]
        makeMovimiento 102 DateTime.Now (Salida (Merma "Deterioro")) [
            makeLinea 10 30m // Stock Lote 10 (A): 70g
        ]
    ]
    
    // Test 1: Consumo parcial del primer lote (A)
    let req1 = { Valor = 50m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId prodId) req1 lotes movimientos with
    | Error err -> failwithf "Debería haber resuelto: %A" err
    | Ok lineas ->
        Assert.Single(lineas) |> ignore
        let l1 = lineas.[0]
        Assert.Equal(LoteId 10, l1.Referencia)
        Assert.Equal(50m, l1.Cantidad.Valor)

    // Test 2: Consumo que agota lote A y consume parte de lote C
    let req2 = { Valor = 150m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId prodId) req2 lotes movimientos with
    | Error err -> failwithf "Debería haber resuelto: %A" err
    | Ok lineas ->
        Assert.Equal(2, lineas.Length)
        
        let l1 = lineas.[0]
        Assert.Equal(LoteId 10, l1.Referencia)
        Assert.Equal(70m, l1.Cantidad.Valor)
        
        let l2 = lineas.[1]
        Assert.Equal(LoteId 12, l2.Referencia)
        Assert.Equal(80m, l2.Cantidad.Valor)

    // Test 3: Consumo que supera todo el stock disponible
    let req3 = { Valor = 450m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId prodId) req3 lotes movimientos with
    | Error (StockInsuficiente _) -> ()
    | res -> failwithf "Debería haber fallado por stock insuficiente, obtuvo: %A" res

[<Fact>]
let ``Usuario updates are immutable and return updated copy`` () =
    let empId = EmpleadoId 1
    let username =
        match Usuario.validarNombreUsuario "test.user" with
        | Ok u -> u
        | Error e -> failwithf "Invalid username: %A" e
    let hash = PasswordHash "some-hash"
    let roles = Set.ofList [Administrador; Almacen]
    
    // Create initial user
    let user =
        match Usuario.crear empId username hash roles with
        | Ok u -> u
        | Error e -> failwithf "Failed to create user: %A" e
    
    Assert.True(Usuario.activo user)
    Assert.True((roles = Usuario.roles user))
    
    // Desactivar
    let inactiveUser = Usuario.desactivar user
    Assert.False(Usuario.activo inactiveUser)
    Assert.True(Usuario.activo user) // original remains active
    
    // Assign new roles
    let newRolesList = [Laboratorio; Comercial]
    let updatedRolesUser = Usuario.asignarRoles newRolesList user
    Assert.True(Set.ofList newRolesList = Usuario.roles updatedRolesUser)
    Assert.True((roles = Usuario.roles user)) // original remains unchanged
    
    // Check tieneRol and helper properties
    Assert.True(Usuario.tieneRol Administrador user)
    Assert.True(Usuario.esAdministrador user)
    Assert.True(Usuario.puedeGestionarUsuarios user)
    Assert.False(Usuario.tieneRol Laboratorio user)
    Assert.True(Usuario.puedeRegistrarAnalisis updatedRolesUser)
