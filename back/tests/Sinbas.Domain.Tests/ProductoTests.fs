module ProductoTests

open System
open Xunit
open Sinbas.Domain

let sampleGuid = Guid.Parse("01917f3a-0003-7000-8000-000000000001")
let prodId = ProductoId sampleGuid
let usrGuid = Guid.Parse("01917f3a-0002-7000-8000-000000000002")
let usrId = UsuarioId usrGuid

let crearSemillaValida () =
    match NombreCientifico.crear "Swietenia" "macrophylla" None with
    | Error e -> failwithf "Error creando NC: %A" e
    | Ok nc ->
        let nomComun = match NombreComun.crear "Caoba" with Ok c -> c | Error e -> failwithf "%A" e
        let cat = Semilla(nc, [nomComun])
        Producto.crearBorrador prodId Kilogramo PorLote cat false (Some "Observación inicial")

[<Fact>]
let ``Creacion de Producto Semilla en borrador sin precio queda en estado PendientePrecioBorrador y no es apto para venta`` () =
    let prod = crearSemillaValida ()
    Assert.Equal(PendientePrecioBorrador, prod.EstadoComercial)
    Assert.True(Option.isNone prod.PrecioOficial)
    Assert.False(Producto.esAptoParaVenta prod)
    Assert.Equal("Swietenia macrophylla", Producto.nombreVisible prod)

[<Fact>]
let ``Validacion de precio negativo o cero al asignar precio oficial retorna error CantidadInvalida`` () =
    let prod = crearSemillaValida ()
    
    match Producto.asignarPrecio 0m (Some "BOB") (Some usrId) prod with
    | Error (CantidadInvalida msg) -> Assert.Contains("mayor a cero", msg)
    | res -> failwithf "Debería haber fallado por precio cero, obtuvo: %A" res

    match Producto.asignarPrecio -50m (Some "BOB") (Some usrId) prod with
    | Error (CantidadInvalida _) -> ()
    | res -> failwithf "Debería haber fallado por precio negativo, obtuvo: %A" res

[<Fact>]
let ``Asignacion de precio oficial a un producto borrador cambia inmutablemente su estado comercial a ActivoParaVenta`` () =
    let borrador = crearSemillaValida ()
    
    match Producto.asignarPrecio 150m (Some "BOB") (Some usrId) borrador with
    | Error e -> failwithf "Falló asignación de precio: %A" e
    | Ok prodConPrecio ->
        Assert.Equal(ActivoParaVenta, prodConPrecio.EstadoComercial)
        Assert.True(Producto.esAptoParaVenta prodConPrecio)
        Assert.True(Option.isSome prodConPrecio.PrecioOficial)
        Assert.Equal(150m, prodConPrecio.PrecioOficial.Value.Valor)

[<Fact>]
let ``Producto sin nombre cientifico genero o epiteto vacio en Semilla/Plantin falla con NombreInvalido`` () =
    match NombreCientifico.crear "" "macrophylla" None with
    | Error (NombreInvalido msg) -> Assert.Contains("género", msg)
    | res -> failwithf "Debería haber fallado por género vacío: %A" res

    match NombreCientifico.crear "Swietenia" "  " None with
    | Error (NombreInvalido msg) -> Assert.Contains("epíteto", msg)
    | res -> failwithf "Debería haber fallado por epíteto vacío: %A" res

[<Fact>]
let ``Verificacion de inmutabilidad: asignar precio retorna una nueva instancia de Producto sin alterar la anterior`` () =
    let original = crearSemillaValida ()
    
    match Producto.asignarPrecio 200m (Some "BOB") (Some usrId) original with
    | Error e -> failwithf "Falló: %A" e
    | Ok actualizado ->
        // Original permanece como borrador sin precio
        Assert.Equal(PendientePrecioBorrador, original.EstadoComercial)
        Assert.True(Option.isNone original.PrecioOficial)
        Assert.False(Producto.esAptoParaVenta original)

        // Actualizado tiene precio y estado activo
        Assert.Equal(ActivoParaVenta, actualizado.EstadoComercial)
        Assert.Equal(200m, actualizado.PrecioOficial.Value.Valor)
        Assert.True(Producto.esAptoParaVenta actualizado)

[<Fact>]
let ``Filtro de catalogo: funcion pura filtra correctamente productos comercialmente activos vs borradores`` () =
    let p1 = crearSemillaValida ()
    let p2 = 
        match Producto.asignarPrecio 100m (Some "BOB") (Some usrId) (crearSemillaValida ()) with
        | Ok p -> p
        | Error e -> failwithf "%A" e
    let p3 = Producto.desactivar p2

    let productos = [p1; p2; p3]

    let aptosParaVenta = productos |> List.filter Producto.esAptoParaVenta
    Assert.Single(aptosParaVenta) |> ignore
    Assert.Equal(p2.Base.Id, aptosParaVenta.[0].Base.Id)
