module VentaRestringidaTests

open System
open Xunit
open Sinbas.Domain

let prodIdRestringido = ProductoId (Guid.Parse("01917f3a-0010-7000-8000-000000000001"))
let prodIdNormal = ProductoId (Guid.Parse("01917f3a-0020-7000-8000-000000000002"))

let crearCigarrilloRestringido () =
    let cat = Insumo("Cigarrillos Marlboro", Some "Marlboro", Some "Cajetilla de cigarrillos")
    Producto.crearBorrador prodIdRestringido Unidad_ Simple cat true (Some "Producto con restricción de edad (Cigarrillos)")

let crearSemillaNormal () =
    match NombreCientifico.crear "Ipomoea" "purpurea" None with
    | Error e -> failwithf "NC error: %A" e
    | Ok nc ->
        let nomComun = match NombreComun.crear "Don Diego de día" with Ok c -> c | Error e -> failwithf "%A" e
        let cat = Semilla(nc, [nomComun])
        Producto.crearBorrador prodIdNormal Gramo PorLote cat false (Some "Semillas de uso general")

[<Fact>]
let ``Venta de producto sin restriccion de edad a cliente menor de edad es permitida`` () =
    let hoy = DateOnly(2026, 8, 31)
    let fechaNacimientoMenor = DateOnly(2010, 5, 15) // 16 años
    let productoNormal = crearSemillaNormal ()

    let res = OperacionVenta.validarVentaProductosRestringidos hoy (Some fechaNacimientoMenor) [productoNormal]
    Assert.True(Result.isOk res)

[<Fact>]
let ``Venta de cigarrillos o alcohol a cliente de 18 años o mas es aprobada`` () =
    let hoy = DateOnly(2026, 8, 31)
    let fechaNacimientoAdulto = DateOnly(2000, 1, 10) // 26 años
    let cigarrillo = crearCigarrilloRestringido ()

    let res = OperacionVenta.validarVentaProductosRestringidos hoy (Some fechaNacimientoAdulto) [cigarrillo]
    Assert.True(Result.isOk res)

[<Fact>]
let ``Venta de cigarrillos o alcohol a menor de 18 años es denegada con VentaRestringida error`` () =
    let hoy = DateOnly(2026, 8, 31)
    let fechaNacimientoMenor = DateOnly(2010, 5, 15) // 16 años
    let cigarrillo = crearCigarrilloRestringido ()

    match OperacionVenta.validarVentaProductosRestringidos hoy (Some fechaNacimientoMenor) [cigarrillo] with
    | Error (VentaRestringida msg) ->
        Assert.Contains("menores de 18 años", msg)
    | res -> failwithf "Debería haber sido rechazada la venta a menor de edad, obtuvo: %A" res

[<Fact>]
let ``Venta de producto restringido sin fecha de nacimiento registrada del cliente es denegada`` () =
    let hoy = DateOnly(2026, 8, 31)
    let cigarrillo = crearCigarrilloRestringido ()

    match OperacionVenta.validarVentaProductosRestringidos hoy None [cigarrillo] with
    | Error (VentaRestringida msg) ->
        Assert.Contains("No se especificó la fecha de nacimiento", msg)
    | res -> failwithf "Debería rechazar venta sin fecha de nacimiento, obtuvo: %A" res
