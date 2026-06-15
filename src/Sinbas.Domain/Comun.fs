namespace Sinbas.Domain

open System
open System.Globalization
open System.Text

// ─────────────────────────────────────────────────────────────
// Errores de dominio
// ─────────────────────────────────────────────────────────────

type DomainError =
    | CantidadInvalida of string
    | UnidadIncompatible of string
    | CodigoLoteInvalido of string
    | NombreInvalido of string
    | CIInvalido of string
    | ValorRequerido of string
    | SecuenciaInvalida of string

// ─────────────────────────────────────────────────────────────
// IDs técnicos internos — el compilador impide mezclarlos
// ─────────────────────────────────────────────────────────────

type ProductoId = ProductoId of int
type LoteId = LoteId of int
type OrdenId = OrdenId of int
type PersonaId = PersonaId of int
type EmpleadoId = EmpleadoId of int
type ClienteId = ClienteId of int
type ProveedorId = ProveedorId of int
type LaboratorioId = LaboratorioId of int
type ProformaId = ProformaId of int
type UbicacionId = UbicacionId of int
type UsuarioId = UsuarioId of int
type RolId = RolId of int

type TruequeId = TruequeId of int
type MovimientoId = MovimientoId of int
// ─────────────────────────────────────────────────────────────
// CI boliviana — número + complemento opcional
// ─────────────────────────────────────────────────────────────

type CI =
    { Numero: string
      Complemento: string option }

module CI =
    let formatear (ci: CI) =
        match ci.Complemento with
        | None -> ci.Numero
        | Some c -> sprintf "%s-%s" ci.Numero c

    let crear (numero: string) (complemento: string option) : Result<CI, DomainError> =
        let n = numero.Trim()

        if String.IsNullOrWhiteSpace(n) then
            Error(CIInvalido "El número de CI no puede estar vacío")
        elif n |> Seq.exists (fun c -> not (Char.IsDigit(c))) then
            Error(CIInvalido(sprintf "CI inválido: '%s'" n))
        else
            Ok
                { Numero = n
                  Complemento = complemento |> Option.map (fun s -> s.Trim().ToUpperInvariant()) }

// ─────────────────────────────────────────────────────────────
// Unidades de medida
// ─────────────────────────────────────────────────────────────

type Unidad =
    | Gramo
    | Kilogramo
    | Unidad_ // sufijo para no chocar con la keyword 'unit' de F#
    | Bolsa of gramosNominales: decimal

module Unidad =
    let etiqueta =
        function
        | Gramo -> "g"
        | Kilogramo -> "kg"
        | Unidad_ -> "u"
        | Bolsa g -> sprintf "bolsa(%.0fg)" g

    /// Convierte cualquier unidad a gramos para comparaciones
    let aGramos (u: Unidad) (cantidad: decimal) : decimal =
        match u with
        | Gramo -> cantidad
        | Kilogramo -> cantidad * 1000m
        | Unidad_ -> cantidad
        | Bolsa g -> cantidad * g

    /// Dos unidades son compatibles si pueden sumarse (misma familia)
    let sonCompatibles a b =
        match a, b with
        | Gramo, Gramo -> true
        | Gramo, Kilogramo -> true
        | Kilogramo, Gramo -> true
        | Kilogramo, Kilogramo -> true
        | Bolsa _, Bolsa _ -> true
        | Unidad_, Unidad_ -> true
        | _, _ -> false

// ─────────────────────────────────────────────────────────────
// Cantidad con unidad — tipo central del inventario
// ─────────────────────────────────────────────────────────────

type Cantidad = { Valor: decimal; Unidad: Unidad }

module Cantidad =
    let crear (valor: decimal) (unidad: Unidad) : Result<Cantidad, DomainError> =
        if valor <= 0m then
            Error(CantidadInvalida(sprintf "El valor debe ser mayor a cero, recibido: %M" valor))
        else
            Ok { Valor = valor; Unidad = unidad }

    let enGramos (c: Cantidad) : decimal = Unidad.aGramos c.Unidad c.Valor

    /// Suma dos cantidades — convierte todo a gramos si las unidades difieren
    let sumar (a: Cantidad) (b: Cantidad) : Result<Cantidad, DomainError> =
        if not (Unidad.sonCompatibles a.Unidad b.Unidad) then
            Error(
                UnidadIncompatible(
                    sprintf "No se puede sumar %s con %s" (Unidad.etiqueta a.Unidad) (Unidad.etiqueta b.Unidad)
                )
            )
        else
            // normaliza ambos a gramos
            let totalGramos = enGramos a + enGramos b
            Ok { Valor = totalGramos; Unidad = Gramo }

    /// ¿Hay suficiente stock para cubrir el pedido?
    let esSuficiente (disponible: Cantidad) (pedido: Cantidad) : Result<bool, DomainError> =
        if not (Unidad.sonCompatibles disponible.Unidad pedido.Unidad) then
            Error(UnidadIncompatible "Unidades incompatibles para comparación")
        else
            Ok(enGramos disponible >= enGramos pedido)

    let formatear (c: Cantidad) : string =
        sprintf "%g %s" (float c.Valor) (Unidad.etiqueta c.Unidad)

// ─────────────────────────────────────────────────────────────
// Código visible de negocio para lotes
//
// Formato: [GGGGG][EEE]-[S][AA][MM]-[SS]
//   GGGGG = 5 letras del género     (rellenado con _ si es corto)
//   EEE   = 3 letras del epíteto    (rellenado con _ si es corto)
//   S     = siglo (0 = 2000s, 1 = 2100s)
//   AA    = año de 2 dígitos
//   MM    = mes de 2 dígitos
//   SS    = secuencia de 2 dígitos (01-99)
//
// Ejemplo: IPOMO_ANA-02601-03  (Ipomoea anatina, enero 2026, lote 3)
// ─────────────────────────────────────────────────────────────

type CodigoLote = private CodigoLote of string

module CodigoLote =

    let valor (CodigoLote c) = c

    let private removerAcentos (texto: string) =
        texto.Normalize(NormalizationForm.FormD)
        |> Seq.filter (fun c -> CharUnicodeInfo.GetUnicodeCategory(c) <> UnicodeCategory.NonSpacingMark)
        |> Seq.toArray
        |> String
        |> fun s -> s.Normalize(NormalizationForm.FormC)

    let private normalizar (s: string) =
        s.Trim().ToUpperInvariant() |> removerAcentos

    let private segmentoGenero (s: string) : string =
        let limpio = normalizar s |> fun t -> t.Replace(" ", "")

        if limpio.Length >= 5 then
            limpio.Substring(0, 5)
        else
            limpio // sin relleno, el '_' vendrá como separador

    let private segmentoEpiteto (s: string) : string =
        let limpio = normalizar s |> fun t -> t.Replace(" ", "")

        if limpio.Length >= 3 then
            limpio.Substring(0, 3)
        else
            limpio

    let generar
        (genero: string)
        (epiteto: string)
        (fecha: DateOnly)
        (secuencia: int)
        : Result<CodigoLote, DomainError> =

        if secuencia < 1 || secuencia > 99 then
            Error(SecuenciaInvalida(sprintf "Secuencia debe ser 1-99, recibido: %d" secuencia))
        elif String.IsNullOrWhiteSpace(genero) then
            Error(CodigoLoteInvalido "El género no puede estar vacío")
        elif String.IsNullOrWhiteSpace(epiteto) then
            Error(CodigoLoteInvalido "El epíteto no puede estar vacío")
        else
            let g = segmentoGenero genero
            let e = segmentoEpiteto epiteto
            let siglo = (fecha.Year - 2000) / 100
            let anio = fecha.Year % 100
            let mes = fecha.Month
            // let sep = if g.Length < 5 then "_" else ""
            let codigo = sprintf "%s%s-%d%02d%02d-%02d" g e siglo anio mes secuencia
            Ok(CodigoLote codigo)

    /// Parsea un código ya existente en la BD sin re-validar la lógica
    let desdeString (raw: string) : Result<CodigoLote, DomainError> =
        let s = raw.Trim()
        // formato mínimo: 8 chars + "-" + 5 chars + "-" + 2 chars = 17 chars
        if s.Length < 17 then
            Error(CodigoLoteInvalido(sprintf "Código demasiado corto: '%s'" s))
        else
            Ok(CodigoLote s)
