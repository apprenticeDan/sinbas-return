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
    | StockInsuficiente of string
    | SimbolosNoPermitidos of string
    | LetrasNoPermitidas of string
    | EmpleadoDuplicado of string
    | SinAnalisisLaboratorio of string
    | LoteRechazado of string
    | PorcentajeInvalido of string
    | FechaInvalida of string


// ─────────────────────────────────────────────────────────────
// Generador centralizado de identidades técnicas (UUID v7)
// ─────────────────────────────────────────────────────────────

module Identidad =
    let nuevo () : Guid = Guid.CreateVersion7()

// ─────────────────────────────────────────────────────────────
// IDs técnicos internos — el compilador impide mezclarlos
// ─────────────────────────────────────────────────────────────

type ProductoId = ProductoId of Guid
type LoteId = LoteId of Guid
type OrdenId = OrdenId of Guid

type PersonaId = PersonaId of Guid
type EmpleadoId = EmpleadoId of Guid
type ClienteId = ClienteId of Guid
type ProveedorId = ProveedorId of Guid

type UsuarioId = UsuarioId of Guid

type LaboratorioId = LaboratorioId of Guid
type ProformaId = ProformaId of Guid
type UbicacionId = UbicacionId of Guid

type TruequeId = TruequeId of Guid
type MovimientoId = MovimientoId of Guid
// ─────────────────────────────────────────────────────────────
// CI boliviana — número + complemento opcional + extensión
// ─────────────────────────────────────────────────────────────

type DepartamentoExpedicion =
    | LP // La Paz
    | CB // Cochabamba
    | SC // Santa Cruz
    | OR // Oruro
    | PT // Potosí
    | TJ // Tarija
    | CH // Chuquisaca
    | BE // Beni
    | PD // Pando
    | Extranjero

module DepartamentoExpedicion =
    let aTexto = function
        | LP -> "LP"
        | CB -> "CB"
        | SC -> "SC"
        | OR -> "OR"
        | PT -> "PT"
        | TJ -> "TJ"
        | CH -> "CH"
        | BE -> "BE"
        | PD -> "PD"
        | Extranjero -> "Extranjero"

    let desdeTexto (s: string) : DepartamentoExpedicion option =
        if String.IsNullOrWhiteSpace s then None
        else
            match s.Trim().ToUpperInvariant() with
            | "LP" | "LA PAZ" -> Some LP
            | "CB" | "COCHABAMBA" -> Some CB
            | "SC" | "SANTA CRUZ" -> Some SC
            | "OR" | "ORURO" -> Some OR
            | "PT" | "POTOSI" | "POTOSÍ" -> Some PT
            | "TJ" | "TARIJA" -> Some TJ
            | "CH" | "CHUQUISACA" -> Some CH
            | "BE" | "BENI" -> Some BE
            | "PD" | "PANDO" -> Some PD
            | "EXTRANJERO" | "EXT" -> Some Extranjero
            | _ -> None

type CI =
    private
        { _Numero: string
          _Complemento: string option
          _Extension: DepartamentoExpedicion option }
    member this.Numero = this._Numero
    member this.Complemento = this._Complemento
    member this.Extension = this._Extension

module CI =
    // ── Accessors ────────────────────────────────────────────────────────────
    let numero (ci: CI) : string = ci.Numero
    let complemento (ci: CI) : string option = ci.Complemento
    let extension (ci: CI) : DepartamentoExpedicion option = ci.Extension

    let formatear (ci: CI) =
        let baseNum =
            match ci.Complemento with
            | None -> ci.Numero
            | Some c -> sprintf "%s-%s" ci.Numero c

        match ci.Extension with
        | None -> baseNum
        | Some ext -> sprintf "%s %s" baseNum (DepartamentoExpedicion.aTexto ext)

    let crear (numero: string) (complemento: string option) (extension: DepartamentoExpedicion option) : Result<CI, DomainError> =
        let n = if isNull numero then "" else numero.Trim()

        if String.IsNullOrWhiteSpace(n) then
            Error(CIInvalido "El número de CI no puede estar vacío")
        elif n |> Seq.exists (fun c -> not (Char.IsDigit(c))) then
            Error(CIInvalido(sprintf "El número base de CI debe contener solo dígitos: '%s'" n))
        else
            let compLimpio =
                complemento
                |> Option.map (fun s -> s.Trim().ToUpperInvariant())
                |> Option.bind (fun s -> if String.IsNullOrWhiteSpace(s) then None else Some s)

            Ok
                { _Numero = n
                  _Complemento = compLimpio
                  _Extension = extension }

    /// Reconstruye una instancia de CI para la capa de persistencia/infraestructura
    let reconstruir (numero: string) (complemento: string option) (extension: DepartamentoExpedicion option) : CI =
        let n = if isNull numero || String.IsNullOrWhiteSpace(numero) then "0" else numero.Trim()
        let compLimpio =
            complemento
            |> Option.map (fun s -> s.Trim().ToUpperInvariant())
            |> Option.bind (fun s -> if String.IsNullOrWhiteSpace(s) then None else Some s)
        { _Numero = n
          _Complemento = compLimpio
          _Extension = extension }

// ─────────────────────────────────────────────────────────────
// Unidades de medida estrictas por dimensión física
// ─────────────────────────────────────────────────────────────

type UnidadMedida =
    // Masa / Peso (Semillas)
    | Gramo
    | Kilogramo
    // Volumen (Insumos líquidos)
    | Mililitro
    | Litro
    // Conteo Discreto (Plantines, piezas)
    | UnidadDiscreta

module UnidadMedida =
    let etiqueta = function
        | Gramo -> "g"
        | Kilogramo -> "kg"
        | Mililitro -> "ml"
        | Litro -> "l"
        | UnidadDiscreta -> "u"

    let aTexto = function
        | Gramo -> "Gramo"
        | Kilogramo -> "Kilogramo"
        | Mililitro -> "Mililitro"
        | Litro -> "Litro"
        | UnidadDiscreta -> "UnidadDiscreta"

    let desdeTexto (s: string) : Result<UnidadMedida, DomainError> =
        match (if isNull s then "" else s.Trim().ToLowerInvariant()) with
        | "gramo" | "g" -> Ok Gramo
        | "kilogramo" | "kg" -> Ok Kilogramo
        | "mililitro" | "ml" -> Ok Mililitro
        | "litro" | "l" -> Ok Litro
        | "unidad" | "unidad_" | "u" | "unidaddiscreta" -> Ok UnidadDiscreta
        | otro -> Error (UnidadIncompatible (sprintf "Unidad desconocida: '%s'" otro))

    /// Factor de conversión a la unidad base de su misma dimensión física
    /// (Masa -> Gramos, Volumen -> Mililitros, Conteo -> Unidades)
    let aUnidadBase (unidad: UnidadMedida) (cantidad: decimal) : decimal =
        match unidad with
        | Gramo -> cantidad
        | Kilogramo -> cantidad * 1000m
        | Mililitro -> cantidad
        | Litro -> cantidad * 1000m
        | UnidadDiscreta -> cantidad

    /// Valida si dos unidades pertenecen a la misma dimensión física y pueden operarse
    let sonCompatibles (u1: UnidadMedida) (u2: UnidadMedida) : bool =
        match u1, u2 with
        | (Gramo | Kilogramo), (Gramo | Kilogramo) -> true
        | (Mililitro | Litro), (Mililitro | Litro) -> true
        | UnidadDiscreta, UnidadDiscreta -> true
        | _ -> false

type Unidad = UnidadMedida

module Unidad =
    let etiqueta = UnidadMedida.etiqueta
    let aTexto = UnidadMedida.aTexto
    let desdeTexto = UnidadMedida.desdeTexto
    let aGramos = UnidadMedida.aUnidadBase
    let sonCompatibles = UnidadMedida.sonCompatibles

// ─────────────────────────────────────────────────────────────
// Presentación de producto
// ─────────────────────────────────────────────────────────────

/// Define el empaque y el contenido nominal de un producto
/// Ej: Empaque = "Bolsa", ContenidoNominal = 500m, Unidad = Gramo -> "Bolsa 500 g"
type Presentacion =
    private
        { _Empaque: string
          _ContenidoNominal: decimal
          _Unidad: UnidadMedida }
    member this.Empaque = this._Empaque
    member this.ContenidoNominal = this._ContenidoNominal
    member this.Unidad = this._Unidad

module Presentacion =
    // ── Accessors ────────────────────────────────────────────────────────────
    let empaque (p: Presentacion) : string = p.Empaque
    let contenidoNominal (p: Presentacion) : decimal = p.ContenidoNominal
    let unidad (p: Presentacion) : UnidadMedida = p.Unidad

    // ── Constructores ────────────────────────────────────────────────────────
    let crear (empaque: string) (contenido: decimal) (unidad: UnidadMedida) : Result<Presentacion, DomainError> =
        let empLimpio = if String.IsNullOrWhiteSpace(empaque) then "Unidad" else empaque.Trim()
        if contenido <= 0m then
            Error(CantidadInvalida "El contenido nominal de la presentación debe ser mayor a cero")
        else
            Ok { _Empaque = empLimpio
                 _ContenidoNominal = contenido
                 _Unidad = unidad }

    /// Reconstruye una Presentación desde persistencia garantizando invariantes mínimos
    let reconstruir (empaque: string) (contenido: decimal) (unidad: UnidadMedida) : Presentacion =
        let empLimpio = if String.IsNullOrWhiteSpace(empaque) then "Unidad" else empaque.Trim()
        if contenido <= 0m then
            failwithf "Dato corrupto en BD: Contenido nominal de presentación no positivo %M" contenido
        { _Empaque = empLimpio
          _ContenidoNominal = contenido
          _Unidad = unidad }

    let aTexto (p: Presentacion) : string =
        sprintf "%s %g %s" p.Empaque (float p.ContenidoNominal) (UnidadMedida.etiqueta p.Unidad)

// ─────────────────────────────────────────────────────────────
// Cantidad con unidad estricta por dimensión física
// ─────────────────────────────────────────────────────────────

type Cantidad =
    private
        { _Valor: decimal
          _Unidad: UnidadMedida }
    member this.Valor = this._Valor
    member this.Unidad = this._Unidad

module Cantidad =
    // ── Accessors (necesarios porque el record es privado) ───────────────────
    let valor (c: Cantidad) : decimal      = c.Valor
    let unidad (c: Cantidad) : UnidadMedida = c.Unidad

    // ── Constructores ────────────────────────────────────────────────────────

    /// Para lógica de negocio: acepta >= 0.
    /// La restricción > 0 vive en quien la necesite (Lote.crear, Fifo, etc.).
    let crear (valor: decimal) (unidad: UnidadMedida) : Result<Cantidad, DomainError> =
        if valor < 0m then
            Error(CantidadInvalida(sprintf "La cantidad no puede ser negativa, recibido: %M" valor))
        else
            Ok { _Valor = valor; _Unidad = unidad }

    /// Para Infrastructure al leer de BD: omite re-validación de negocio,
    /// pero falla rápido si el dato en BD está corrupto (negativo).
    let reconstruir (valor: decimal) (unidad: UnidadMedida) : Cantidad =
        if valor < 0m then
            failwithf "Dato corrupto en BD: Cantidad negativa %M" valor
        { _Valor = valor; _Unidad = unidad }

    // ── Operaciones ──────────────────────────────────────────────────────────

    let enGramos (c: Cantidad) : decimal =
        match c.Unidad with
        | Gramo | Kilogramo -> UnidadMedida.aUnidadBase c.Unidad c.Valor
        | _ -> c.Valor

    let aUnidadBase (c: Cantidad) : decimal =
        UnidadMedida.aUnidadBase c.Unidad c.Valor

    let sonCompatibles (a: Cantidad) (b: Cantidad) : bool =
        UnidadMedida.sonCompatibles a.Unidad b.Unidad

    /// Suma dos cantidades asegurando que pertenezcan a la misma dimensión física.
    /// Ambos sumandos deben ser > 0 (no tiene sentido sumar un saldo vacío).
    let sumar (a: Cantidad) (b: Cantidad) : Result<Cantidad, DomainError> =
        if a.Valor <= 0m || b.Valor <= 0m then
            Error (CantidadInvalida "Las cantidades a sumar deben ser mayores a cero")
        elif not (UnidadMedida.sonCompatibles a.Unidad b.Unidad) then
            Error(
                UnidadIncompatible(
                    sprintf "No se puede sumar %s con %s"
                        (UnidadMedida.etiqueta a.Unidad)
                        (UnidadMedida.etiqueta b.Unidad)
                )
            )
        else
            let baseA = aUnidadBase a
            let baseB = aUnidadBase b
            match a.Unidad with
            | Kilogramo -> Ok { _Valor = (baseA + baseB) / 1000m; _Unidad = Kilogramo }
            | Litro     -> Ok { _Valor = (baseA + baseB) / 1000m; _Unidad = Litro }
            | otraUnidad -> Ok { _Valor = baseA + baseB; _Unidad = otraUnidad }

    /// ¿Hay suficiente stock para cubrir el pedido?
    let esSuficiente (disponible: Cantidad) (pedido: Cantidad) : Result<bool, DomainError> =
        if not (UnidadMedida.sonCompatibles disponible.Unidad pedido.Unidad) then
            Error(UnidadIncompatible "Unidades incompatibles para comparación")
        else
            Ok(aUnidadBase disponible >= aUnidadBase pedido)

    let formatear (c: Cantidad) : string =
        sprintf "%g %s" (float c.Valor) (UnidadMedida.etiqueta c.Unidad)

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
