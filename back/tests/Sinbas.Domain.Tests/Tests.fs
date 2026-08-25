module Tests

open System
open Xunit
open Sinbas.Domain

let g1 = Guid.Parse("01917f3a-0000-7000-8000-000000000001")
let g2 = Guid.Parse("01917f3a-0000-7000-8000-000000000002")
let g10 = Guid.Parse("01917f3a-0000-7000-8000-000000000010")
let g11 = Guid.Parse("01917f3a-0000-7000-8000-000000000011")
let g12 = Guid.Parse("01917f3a-0000-7000-8000-000000000012")
let g101 = Guid.Parse("01917f3a-0000-7000-8000-000000000101")
let g102 = Guid.Parse("01917f3a-0000-7000-8000-000000000102")

let makeLote (loteGuid: Guid) (prodGuid: Guid) (idNum: int) fecha estado =
    let codigo =
        match CodigoLote.desdeString (sprintf "TEST_%03d-02601-01" idNum) with
        | Ok c -> c
        | Error e -> failwithf "Error generando codigo: %A" e
    { Id = LoteId loteGuid
      Codigo = codigo
      ProductoId = ProductoId prodGuid
      FechaIngreso = fecha
      UbicacionId = None
      Estado = estado
      Observaciones = None }

let makeMovimiento (movGuid: Guid) fecha tipo lineas =
    { Id = MovimientoId movGuid
      Fecha = fecha
      Responsable = EmpleadoId g1
      Tipo = tipo
      OrdenOrigen = None
      Lineas = lineas
      Observaciones = None }

let makeLinea (loteGuid: Guid) gramos =
    { Referencia = LoteId loteGuid
      Cantidad = { Valor = gramos; Unidad = Gramo } }

[<Fact>]
let ``Generacion de Identidad con UUID v7 produce IDs secuenciales ordenados en el tiempo`` () =
    let count = 10
    let uuids = 
        [ for _ in 1 .. count do
            System.Threading.Thread.Sleep(2)
            yield Identidad.nuevo () ]
    let sortedByString = uuids |> List.sortBy (fun g -> g.ToString())
    Assert.Equal<Guid list>(sortedByString, uuids)

[<Fact>]
let ``Calculo de stock para lote individual calcula entradas y salidas correctamente`` () =
    let movimientos = [
        makeMovimiento g101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [makeLinea g10 100m]
        makeMovimiento g102 DateTime.Now (Salida (Merma "Deterioro")) [makeLinea g10 30m]
    ]
    
    let stock = Stock.cantidadLote (LoteId g10) movimientos
    Assert.Equal(70m, stock)

[<Fact>]
let ``Stock total del producto suma solo lotes activos`` () =
    let loteActivo = makeLote g10 g1 10 (DateOnly(2026, 1, 1)) Activo
    let loteAgotado = makeLote g11 g1 11 (DateOnly(2026, 1, 5)) Agotado
    let loteBloqueado = makeLote g12 g1 12 (DateOnly(2026, 1, 10)) Bloqueado
    
    let lotes = [loteActivo; loteAgotado; loteBloqueado]
    
    let movimientos = [
        makeMovimiento g101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [
            makeLinea g10 100m
            makeLinea g11 50m
            makeLinea g12 80m
        ]
    ]
    
    // stockProducto solo debe sumar el lote activo (lote 10, con 100g)
    let stockTotal = Stock.stockProducto (ProductoId g1) lotes movimientos
    Assert.Equal(100m, stockTotal)

[<Fact>]
let ``Resolver FIFO asigna lotes en orden cronologico de ingreso`` () =
    // Lotes activos con diferentes fechas de ingreso: A (1-Ene), C (5-Ene), B (10-Ene)
    let loteA = makeLote g10 g1 10 (DateOnly(2026, 1, 1)) Activo
    let loteB = makeLote g11 g1 11 (DateOnly(2026, 1, 10)) Activo
    let loteC = makeLote g12 g1 12 (DateOnly(2026, 1, 5)) Activo
    
    let lotes = [loteB; loteA; loteC] // desordenado intencionalmente
    
    let movimientos = [
        makeMovimiento g101 DateTime.Now (Entrada (Recoleccion "Campaña A")) [
            makeLinea g10 100m
            makeLinea g12 150m
            makeLinea g11 200m
        ]
        makeMovimiento g102 DateTime.Now (Salida (Merma "Deterioro")) [
            makeLinea g10 30m // Stock Lote 10 (A): 70g
        ]
    ]
    
    // Test 1: Consumo parcial del primer lote (A)
    let req1 = { Valor = 50m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId g1) req1 lotes movimientos with
    | Error err -> failwithf "Debería haber resuelto: %A" err
    | Ok lineas ->
        Assert.Single(lineas) |> ignore
        let l1 = lineas.[0]
        Assert.Equal(LoteId g10, l1.Referencia)
        Assert.Equal(50m, l1.Cantidad.Valor)

    // Test 2: Consumo que agota lote A y consume parte de lote C
    let req2 = { Valor = 150m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId g1) req2 lotes movimientos with
    | Error err -> failwithf "Debería haber resuelto: %A" err
    | Ok lineas ->
        Assert.Equal(2, lineas.Length)
        
        let l1 = lineas.[0]
        Assert.Equal(LoteId g10, l1.Referencia)
        Assert.Equal(70m, l1.Cantidad.Valor)
        
        let l2 = lineas.[1]
        Assert.Equal(LoteId g12, l2.Referencia)
        Assert.Equal(80m, l2.Cantidad.Valor)

    // Test 3: Consumo que supera todo el stock disponible
    let req3 = { Valor = 450m; Unidad = Gramo }
    match Fifo.resolverFIFO (ProductoId g1) req3 lotes movimientos with
    | Error (StockInsuficiente _) -> ()
    | res -> failwithf "Debería haber fallado por stock insuficiente, obtuvo: %A" res

[<Fact>]
let ``Usuario updates are immutable and return updated copy`` () =
    let empId = EmpleadoId g1
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
