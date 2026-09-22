namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Feature F6: Gestión de Clientes (RF08 | CU-17 | RN18)
// ─────────────────────────────────────────────────────────────

/// NIT boliviano: cadena numérica no vacía
type NIT = private NIT of string

module NIT =

    let valor (NIT n) = n

    let crear (raw: string) : Result<NIT, DomainError> =
        let s = if isNull raw then "" else raw.Trim()
        if String.IsNullOrWhiteSpace s then
            Error (ValorRequerido "El NIT no puede estar vacío")
        elif s |> Seq.exists (fun c -> not (Char.IsDigit c)) then
            Error (SimbolosNoPermitidos (sprintf "El NIT debe contener solo dígitos, recibido: '%s'" s))
        else
            Ok (NIT s)

    let reconstruir (s: string) : NIT = NIT s

/// Razón Social: texto no vacío que permite letras, dígitos, espacios y caracteres comunes de nombres corporativos
type RazonSocial = private RazonSocial of string

module RazonSocial =

    let valor (RazonSocial r) = r

    let crear (raw: string) : Result<RazonSocial, DomainError> =
        let s = if isNull raw then "" else raw.Trim()
        if String.IsNullOrWhiteSpace s then
            Error (ValorRequerido "La razón social no puede estar vacía")
        elif s.Length < 2 then
            Error (NombreInvalido (sprintf "La razón social es demasiado corta: '%s'" s))
        elif s.Length > 200 then
            Error (NombreInvalido (sprintf "La razón social excede 200 caracteres"))
        else
            // Permitir letras, dígitos, espacios, puntos, comas, guiones, paréntesis, &
            // Ej: "Agroforestal 2000 S.R.L.", "Viveros del Sur & Cía."
            let esCaracterValido c =
                Char.IsLetterOrDigit c
                || c = ' ' || c = '.' || c = ',' || c = '-'
                || c = '(' || c = ')' || c = '&' || c = '\'' || c = '/'
            if s |> Seq.forall esCaracterValido then
                Ok (RazonSocial s)
            else
                Error (SimbolosNoPermitidos (sprintf "La razón social contiene caracteres no permitidos: '%s'" s))

    let reconstruir (s: string) : RazonSocial = RazonSocial s

// ─────────────────────────────────────────────────────────────
// Tipo de Cliente: Persona Natural vs Jurídica
// ─────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type TipoCliente =
    /// Persona Natural: reutiliza datos de Persona (RN18)
    | Natural of Persona
    /// Persona Jurídica: Razón Social + NIT + representante legal opcional
    | Juridica of razonSocial: RazonSocial * nit: NIT * representante: Persona option

module TipoCliente =

    let aTexto = function
        | TipoCliente.Natural _ -> "Natural"
        | TipoCliente.Juridica _ -> "Juridica"

    /// Nombre visible del cliente para listados
    let nombreVisible = function
        | TipoCliente.Natural persona -> persona.NombreCompleto
        | TipoCliente.Juridica (rs, _, _) -> RazonSocial.valor rs

// ─────────────────────────────────────────────────────────────
// Estado de Cliente
// ─────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type EstadoCliente =
    | Activo
    | Inactivo

module EstadoCliente =

    let aTexto = function
        | EstadoCliente.Activo -> "Activo"
        | EstadoCliente.Inactivo -> "Inactivo"

    let desdeTexto (s: string) : Result<EstadoCliente, DomainError> =
        match (if isNull s then "" else s.Trim().ToLowerInvariant()) with
        | "activo" -> Ok EstadoCliente.Activo
        | "inactivo" -> Ok EstadoCliente.Inactivo
        | otro -> Error (ValorRequerido (sprintf "Estado de cliente inválido: '%s'" otro))

// ─────────────────────────────────────────────────────────────
// Entidad: Cliente
// ─────────────────────────────────────────────────────────────

type Cliente =
    { Id: ClienteId
      Tipo: TipoCliente
      Telefono: string option
      Email: string option
      Direccion: string option
      Estado: EstadoCliente }

module Cliente =

    /// Nombre visible del cliente (delegado al tipo)
    let nombreVisible (c: Cliente) : string =
        TipoCliente.nombreVisible c.Tipo

    /// Verificar si el cliente está activo
    let estaActivo (c: Cliente) : bool =
        c.Estado = EstadoCliente.Activo

    /// Crear un cliente Persona Natural validando inputs (reutiliza Persona / RN18)
    let crearNatural
        (persona: Persona)
        (telefonoRaw: string option)
        (emailRaw: string option)
        (direccionRaw: string option)
        : Result<Cliente, DomainError> =

        let telValidado =
            match telefonoRaw with
            | Some t when not (String.IsNullOrWhiteSpace t) ->
                Validacion.validarTelefono "Teléfono del cliente" t |> Result.map Some
            | _ -> Ok None

        match telValidado with
        | Error err -> Error err
        | Ok tLimpio ->
            let emailLimpio =
                emailRaw
                |> Option.bind (fun e ->
                    let trimmed = e.Trim().ToLowerInvariant()
                    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed)

            let dirLimpia =
                direccionRaw
                |> Option.bind (fun d ->
                    let trimmed = d.Trim()
                    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed)

            Ok { Id = ClienteId (Identidad.nuevo ())
                 Tipo = TipoCliente.Natural persona
                 Telefono = tLimpio
                 Email = emailLimpio
                 Direccion = dirLimpia
                 Estado = EstadoCliente.Activo }

    /// Crear un cliente Persona Jurídica
    let crearJuridica
        (razonSocial: RazonSocial)
        (nit: NIT)
        (representante: Persona option)
        (telefonoRaw: string option)
        (emailRaw: string option)
        (direccionRaw: string option)
        : Result<Cliente, DomainError> =

        let telValidado =
            match telefonoRaw with
            | Some t when not (String.IsNullOrWhiteSpace t) ->
                Validacion.validarTelefono "Teléfono de la empresa" t |> Result.map Some
            | _ -> Ok None

        match telValidado with
        | Error err -> Error err
        | Ok tLimpio ->
            let emailLimpio =
                emailRaw
                |> Option.bind (fun e ->
                    let trimmed = e.Trim().ToLowerInvariant()
                    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed)

            let dirLimpia =
                direccionRaw
                |> Option.bind (fun d ->
                    let trimmed = d.Trim()
                    if String.IsNullOrWhiteSpace trimmed then None else Some trimmed)

            Ok { Id = ClienteId (Identidad.nuevo ())
                 Tipo = TipoCliente.Juridica (razonSocial, nit, representante)
                 Telefono = tLimpio
                 Email = emailLimpio
                 Direccion = dirLimpia
                 Estado = EstadoCliente.Activo }

    /// Reconstruir desde BD (sin re-validar)
    let reconstruir
        (id: Guid)
        (tipo: TipoCliente)
        (telefono: string option)
        (email: string option)
        (direccion: string option)
        (estado: EstadoCliente)
        : Cliente =
        { Id = ClienteId id
          Tipo = tipo
          Telefono = telefono
          Email = email
          Direccion = direccion
          Estado = estado }
