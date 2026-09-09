module EmpleadoTests

open System
open Xunit
open Sinbas.Domain

[<Fact>]
let ``Creacion de empleado con datos validos y ambos apellidos se ejecuta correctamente`` () =
    let ci = match CI.crear "1234567" (Some "LP") with Ok c -> c | Error e -> failwithf "%A" e
    
    match Empleado.crear "Juan Carlos" (Some "Pérez") (Some "Martínez") ci (Some "+591 71234567") (Some "juan.perez@empresa.com") with
    | Ok emp ->
        Assert.Equal("Juan Carlos", emp.Nombres)
        Assert.Equal(Some "Pérez", emp.ApellidoPaterno)
        Assert.Equal(Some "Martínez", emp.ApellidoMaterno)
        Assert.Equal("Pérez Martínez, Juan Carlos", emp.NombreCompleto)
        Assert.Equal("1234567", emp.CI.Numero)
        Assert.Equal(Some "LP", emp.CI.Complemento)
        Assert.Equal(Some "+591 71234567", emp.Telefono)
        Assert.Equal(Some "juan.perez@empresa.com", emp.Email)
        Assert.Equal(EstadoEmpleado.Activo, emp.Estado)
    | Error e -> failwithf "Falló creación de empleado válido: %A" e

[<Fact>]
let ``Creacion de empleado con solo apellido materno (caso docente) es permitida y formatea correctamente`` () =
    let ci = match CI.crear "9876543" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "Gabriel" None (Some "Torrico") ci None None with
    | Ok emp ->
        Assert.Equal("Gabriel", emp.Nombres)
        Assert.Null(emp.ApellidoPaterno |> Option.toObj)
        Assert.Equal(Some "Torrico", emp.ApellidoMaterno)
        Assert.Equal("Torrico, Gabriel", emp.NombreCompleto)
        Assert.Equal(EstadoEmpleado.Activo, emp.Estado)
    | Error e -> failwithf "Falló creación con solo apellido materno: %A" e

[<Fact>]
let ``Creacion de empleado con solo apellido paterno es permitida y formatea correctamente`` () =
    let ci = match CI.crear "4567890" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "María Elena" (Some "Rivas") None ci None None with
    | Ok emp ->
        Assert.Equal("María Elena", emp.Nombres)
        Assert.Equal(Some "Rivas", emp.ApellidoPaterno)
        Assert.Null(emp.ApellidoMaterno |> Option.toObj)
        Assert.Equal("Rivas, María Elena", emp.NombreCompleto)
    | Error e -> failwithf "Falló creación con solo apellido paterno: %A" e

[<Fact>]
let ``Creacion de empleado sin ningun apellido retorna error ValorRequerido`` () =
    let ci = match CI.crear "1122334" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "Juan Daniel" None None ci None None with
    | Error (ValorRequerido msg) ->
        Assert.Contains("al menos un apellido", msg)
    | res -> failwithf "Debería haber fallado por falta de apellidos, obtuvo: %A" res

[<Fact>]
let ``Creacion de empleado con numeros en nombres o apellidos retorna error SimbolosNoPermitidos`` () =
    let ci = match CI.crear "8765432" None with Ok c -> c | Error e -> failwithf "%A" e

    // Número en nombre
    match Empleado.crear "Carlos123" (Some "Pérez") None ci None None with
    | Error (SimbolosNoPermitidos msg) -> Assert.Contains("números", msg)
    | res -> failwithf "Debería haber fallado por nombre con números, obtuvo: %A" res

    // Número en apellido paterno
    match Empleado.crear "Carlos" (Some "Perez99") None ci None None with
    | Error (SimbolosNoPermitidos msg) -> Assert.Contains("números", msg)
    | res -> failwithf "Debería haber fallado por paterno con números, obtuvo: %A" res

    // Número en apellido materno
    match Empleado.crear "Carlos" None (Some "Gomez2") ci None None with
    | Error (SimbolosNoPermitidos msg) -> Assert.Contains("números", msg)
    | res -> failwithf "Debería haber fallado por materno con números, obtuvo: %A" res

[<Fact>]
let ``Creacion de empleado con letras en el telefono retorna error LetrasNoPermitidas`` () =
    let ci = match CI.crear "8765432" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "Carlos" (Some "Mendoza") None ci (Some "71234567abc") None with
    | Error (LetrasNoPermitidas msg) -> Assert.Contains("letras", msg)
    | res -> failwithf "Debería haber fallado por teléfono con letras, obtuvo: %A" res

[<Fact>]
let ``Inactivar y activar empleado actualiza su estado inmutablemente`` () =
    let ci = match CI.crear "5544332" None with Ok c -> c | Error e -> failwithf "%A" e
    let emp = match Empleado.crear "Ana María" (Some "Ríos") None ci None None with Ok e -> e | Error err -> failwithf "%A" err
    
    let inactivo = Empleado.inactivar emp
    Assert.Equal(EstadoEmpleado.Inactivo, inactivo.Estado)
    Assert.Equal(EstadoEmpleado.Activo, emp.Estado)

    let reactivado = Empleado.activar inactivo
    Assert.Equal(EstadoEmpleado.Activo, reactivado.Estado)

[<Fact>]
let ``Actualizar empleado modifica sus datos preservando identidad y estado`` () =
    let ci = match CI.crear "1234567" None with Ok c -> c | Error e -> failwithf "%A" e
    let ciNueva = match CI.crear "7654321" (Some "CB") with Ok c -> c | Error e -> failwithf "%A" e
    let original = match Empleado.crear "Pedro" (Some "Ramos") None ci None None with Ok e -> e | Error err -> failwithf "%A" err

    match Empleado.actualizar "Pedro Pablo" (Some "Ramos") (Some "Suárez") ciNueva (Some "79998888") (Some "pedro@empresa.com") original with
    | Ok actualizado ->
        Assert.Equal(original.Id, actualizado.Id)
        Assert.Equal(original.Estado, actualizado.Estado)
        Assert.Equal("Pedro Pablo", actualizado.Nombres)
        Assert.Equal(Some "Suárez", actualizado.ApellidoMaterno)
        Assert.Equal("Ramos Suárez, Pedro Pablo", actualizado.NombreCompleto)
        Assert.Equal("7654321", actualizado.CI.Numero)
        Assert.Equal(Some "CB", actualizado.CI.Complemento)
    | Error e -> failwithf "Falló actualización de empleado: %A" e

