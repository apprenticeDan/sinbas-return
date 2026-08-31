module ValidacionTests

open System
open Xunit
open Sinbas.Domain

[<Fact>]
let ``validarTextoNombre acepta nombres validos con acentos, espacios y guiones`` () =
    let casosValidos = [ "Juan Pérez"; "María José"; "René-Gómez"; "O'Connor"; "Álvaro NÚÑEZ" ]
    for nombre in casosValidos do
        match Validacion.validarTextoNombre "Nombre" nombre with
        | Ok res -> Assert.False(String.IsNullOrWhiteSpace(res))
        | Error err -> failwithf "Debería haber sido válido '%s': %A" nombre err

[<Fact>]
let ``validarTextoNombre rechaza nombres con numeros`` () =
    let casosInvalidos = [ "Juan123"; "Pedro 4"; "007 Bond"; "Ana 2026" ]
    for nombre in casosInvalidos do
        match Validacion.validarTextoNombre "Nombre" nombre with
        | Error (SimbolosNoPermitidos msg) -> Assert.Contains("números", msg)
        | res -> failwithf "Debería haber fallado por contener números '%s', obtuvo: %A" nombre res

[<Fact>]
let ``validarTextoNombre rechaza nombres con simbolos raros o etiquetas maliciosas`` () =
    let casosInvalidos = [ "Carlos#$"; "Ana@Dev"; "<script>alert('x')</script>"; "Pedro; DROP TABLE;" ]
    for nombre in casosInvalidos do
        match Validacion.validarTextoNombre "Nombre" nombre with
        | Error (SimbolosNoPermitidos msg) -> Assert.Contains("especiales no válidos", msg)
        | res -> failwithf "Debería haber fallado por símbolos raros '%s', obtuvo: %A" nombre res

[<Fact>]
let ``validarTelefono acepta formatos de telefono numericos validos`` () =
    let telefonosValidos = [ "+591 71234567"; "2441122"; "(591) 4-442211"; "+1-800-555-0199" ]
    for tel in telefonosValidos do
        match Validacion.validarTelefono "Teléfono" tel with
        | Ok res -> Assert.False(String.IsNullOrWhiteSpace(res))
        | Error err -> failwithf "Debería haber sido válido '%s': %A" tel err

[<Fact>]
let ``validarTelefono rechaza letras en campos de telefono`` () =
    let telefonosInvalidos = [ "71234567a"; "Teléfono 123"; "N/A"; "abc12345" ]
    for tel in telefonosInvalidos do
        match Validacion.validarTelefono "Teléfono" tel with
        | Error (LetrasNoPermitidas msg) -> Assert.Contains("letras", msg)
        | res -> failwithf "Debería haber fallado por contener letras '%s', obtuvo: %A" tel res

[<Fact>]
let ``validarNombreUsuario acepta usernames alfanumericos sin espacios`` () =
    let usernamesValidos = [ "admin"; "user_123"; "juan.perez"; "dev_user_01" ]
    for usr in usernamesValidos do
        match Validacion.validarNombreUsuario usr with
        | Ok res -> Assert.Equal(usr.ToLowerInvariant(), res)
        | Error err -> failwithf "Debería ser válido '%s': %A" usr err

[<Fact>]
let ``validarNombreUsuario rechaza espacios, caracteres especiales o longitud invalida`` () =
    let usernamesInvalidos = [ "user name"; "admin!"; "a@b.com"; "ab"; "usuario_demasiado_largo_para_el_sistema_123" ]
    for usr in usernamesInvalidos do
        match Validacion.validarNombreUsuario usr with
        | Error (NombreInvalido _) -> ()
        | Error (ValorRequerido _) -> ()
        | res -> failwithf "Debería haber fallado '%s', obtuvo: %A" usr res

[<Fact>]
let ``validarPassword exige al menos 8 caracteres y rechaza passwords vacias`` () =
    match Validacion.validarPassword "SuperClave123" with
    | Ok pass -> Assert.Equal("SuperClave123", pass)
    | Error e -> failwithf "Falló password válida: %A" e

    match Validacion.validarPassword "short" with
    | Error (ValorRequerido msg) -> Assert.Contains("8 caracteres", msg)
    | res -> failwithf "Debería rechazar clave corta, obtuvo: %A" res

[<Fact>]
let ``validarMayorEdad comprueba correctamente limite de 18 años y fecha exacta`` () =
    let hoy = DateOnly(2026, 8, 31)
    
    // Cumple 18 hoy exacto: Nacido 2008-08-31
    let exacto18 = DateOnly(2008, 8, 31)
    Assert.True(Validacion.validarMayorEdad exacto18 hoy 18 |> Result.isOk)

    // Mayor de edad: Nacido 2000-01-01
    let mayor = DateOnly(2000, 1, 1)
    Assert.True(Validacion.validarMayorEdad mayor hoy 18 |> Result.isOk)

    // Menor por 1 día: Nacido 2008-09-01 (mañana cumple 18)
    let menorPorUnDia = DateOnly(2008, 9, 1)
    match Validacion.validarMayorEdad menorPorUnDia hoy 18 with
    | Error (EdadInsuficiente msg) -> Assert.Contains("18 años", msg)
    | res -> failwithf "Debería haber fallado por menor de edad, obtuvo: %A" res

[<Fact>]
let ``sanitizarInput rechaza caracteres de control no permitidos como nulos`` () =
    let textoMalicioso = "input\u0000malicioso"
    match Validacion.sanitizarInput textoMalicioso with
    | Error (SimbolosNoPermitidos msg) -> Assert.Contains("caracteres de control", msg)
    | res -> failwithf "Debería haber fallado sanitización, obtuvo: %A" res
