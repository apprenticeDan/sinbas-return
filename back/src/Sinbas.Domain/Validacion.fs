namespace Sinbas.Domain

open System
open System.Text.RegularExpressions

module Validacion =

    /// Valida que el texto sea un nombre o apellido válido (solo letras, espacios, guiones y apóstrofos; sin dígitos 0-9 ni símbolos raros).
    let validarTextoNombre (campo: string) (raw: string) : Result<string, DomainError> =
        let texto = (raw |> Option.ofObj |> Option.defaultValue "").Trim()
        if String.IsNullOrWhiteSpace texto then
            Error (ValorRequerido $"El campo '{campo}' no puede estar vacío")
        else
            if texto |> Seq.exists Char.IsDigit then
                Error (SimbolosNoPermitidos $"El campo '{campo}' no debe contener números, recibido: '{texto}'")
            else
                let regexNombre = Regex(@"^[\p{L}\s'\-]+$")
                if regexNombre.IsMatch texto then
                    Ok texto
                else
                    Error (SimbolosNoPermitidos $"El campo '{campo}' contiene caracteres especiales no válidos: '{texto}'")

    /// Valida que un campo numérico como teléfono no contenga letras (solo dígitos, espacios, guiones, paréntesis o prefijo '+').
    let validarTelefono (campo: string) (raw: string) : Result<string, DomainError> =
        let texto = (raw |> Option.ofObj |> Option.defaultValue "").Trim()
        if String.IsNullOrWhiteSpace texto then
            Error (ValorRequerido $"El campo '{campo}' no puede estar vacío")
        else
            if texto |> Seq.exists Char.IsLetter then
                Error (LetrasNoPermitidas $"El campo '{campo}' no debe contener letras, recibido: '{texto}'")
            else
                let regexTelefono = Regex(@"^\+?[0-9\s\-\(\)]{6,20}$")
                if regexTelefono.IsMatch texto then
                    Ok texto
                else
                    Error (SimbolosNoPermitidos $"El campo '{campo}' tiene un formato telefónico no válido: '{texto}'")

    /// Valida un nombre de usuario para login/creación (alfanumérico, punto o guion bajo, entre 3 y 20 caracteres).
    let validarNombreUsuario (raw: string) : Result<string, DomainError> =
        let texto = (raw |> Option.ofObj |> Option.defaultValue "").Trim()
        if String.IsNullOrWhiteSpace texto then
            Error (ValorRequerido "El nombre de usuario no puede estar vacío")
        else
            let regexUsuario = Regex(@"^[a-zA-Z0-9_.]{3,20}$")
            if regexUsuario.IsMatch texto then
                Ok (texto.ToLowerInvariant())
            else
                Error (NombreInvalido $"Nombre de usuario no válido: '{raw}'. Solo se permiten entre 3 y 20 caracteres alfanuméricos, puntos o guiones bajos sin espacios ni símbolos raros.")

    /// Valida una contraseña antes del hashing (mínimo 8 caracteres, no vacía).
    let validarPassword (raw: string) : Result<string, DomainError> =
        if String.IsNullOrEmpty raw then
            Error (ValorRequerido "La contraseña no puede estar vacía")
        elif raw.Length < 8 then
            Error (ValorRequerido "La contraseña debe tener al menos 8 caracteres")
        else
            Ok raw

    /// Valida edad mínima (ej. 18 años) dada la fecha de nacimiento y la fecha de referencia (o fecha actual).
    let validarMayorEdad (fechaNacimiento: DateOnly) (fechaReferencia: DateOnly) (edadMinima: int) : Result<unit, DomainError> =
        let mutable edad = fechaReferencia.Year - fechaNacimiento.Year
        if fechaReferencia < fechaNacimiento.AddYears(edad) then
            edad <- edad - 1
        if edad < edadMinima then
            Error (EdadInsuficiente $"Se requiere una edad mínima de {edadMinima} años (edad calculada: {edad} años)")
        else
            Ok ()

    /// Sanitiza / valida entradas generales contra secuencias de inyección básicas o caracteres de control de forma proactiva.
    let sanitizarInput (raw: string) : Result<string, DomainError> =
        let texto = (raw |> Option.ofObj |> Option.defaultValue "").Trim()
        if texto |> Seq.exists (fun c -> Char.IsControl c && c <> '\r' && c <> '\n' && c <> '\t') then
            Error (SimbolosNoPermitidos "La entrada contiene caracteres de control no permitidos")
        else
            Ok texto
