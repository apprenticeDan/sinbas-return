module EmpleadoTests

open System
open Xunit
open Sinbas.Domain

[<Fact>]
let ``Creacion de empleado con datos validos se ejecuta correctamente`` () =
    let ci = match CI.crear "1234567" (Some "LP") with Ok c -> c | Error e -> failwithf "%A" e
    
    match Empleado.crear "Juan Carlos Pérez" ci (Some "+591 71234567") (Some "juan.perez@empresa.com") with
    | Ok emp ->
        Assert.Equal("Juan Carlos Pérez", emp.NombreCompleto)
        Assert.Equal("1234567", emp.CI.Numero)
        Assert.Equal(Some "LP", emp.CI.Complemento)
        Assert.Equal(Some "+591 71234567", emp.Telefono)
        Assert.Equal(Some "juan.perez@empresa.com", emp.Email)
        Assert.Equal(EstadoEmpleado.Activo, emp.Estado)
    | Error e -> failwithf "Falló creación de empleado válido: %A" e

[<Fact>]
let ``Creacion de empleado con numeros en el nombre retorna error SimbolosNoPermitidos`` () =
    let ci = match CI.crear "8765432" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "Carlos123" ci None None with
    | Error (SimbolosNoPermitidos msg) -> Assert.Contains("números", msg)
    | res -> failwithf "Debería haber fallado por nombre con números, obtuvo: %A" res

[<Fact>]
let ``Creacion de empleado con letras en el telefono retorna error LetrasNoPermitidas`` () =
    let ci = match CI.crear "8765432" None with Ok c -> c | Error e -> failwithf "%A" e

    match Empleado.crear "Carlos Mendoza" ci (Some "71234567abc") None with
    | Error (LetrasNoPermitidas msg) -> Assert.Contains("letras", msg)
    | res -> failwithf "Debería haber fallado por teléfono con letras, obtuvo: %A" res

[<Fact>]
let ``Inactivar y activar empleado actualiza su estado inmutablemente`` () =
    let ci = match CI.crear "5544332" None with Ok c -> c | Error e -> failwithf "%A" e
    let emp = match Empleado.crear "Ana María Ríos" ci None None with Ok e -> e | Error err -> failwithf "%A" err
    
    let inactivo = Empleado.inactivar emp
    Assert.Equal(EstadoEmpleado.Inactivo, inactivo.Estado)
    Assert.Equal(EstadoEmpleado.Activo, emp.Estado) // El original permanece inalterado

    let reactivado = Empleado.activar inactivo
    Assert.Equal(EstadoEmpleado.Activo, reactivado.Estado)
