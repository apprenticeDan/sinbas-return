namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Value Object: PorcentajeCalidad (0.00% a 100.00%)
// RF06 | RN12 | CU-05
// ─────────────────────────────────────────────────────────────

type PorcentajeCalidad = private { Valor: decimal }

module PorcentajeCalidad =

    let valor (p: PorcentajeCalidad) : decimal = p.Valor

    let crear (v: decimal) : Result<PorcentajeCalidad, DomainError> =
        if v < 0.0m || v > 100.0m then
            Error (PorcentajeInvalido (sprintf "El porcentaje de calidad debe estar entre 0%% y 100%%, recibido: %M%%" v))
        else
            Ok { Valor = v }

    let reconstruir (v: decimal) : PorcentajeCalidad =
        { Valor = v }

// ─────────────────────────────────────────────────────────────
// Dictamen técnico oficial de laboratorio (DU cerrada)
// RF06 | RN12
// ─────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type DictamenCalidad =
    | Aprobado
    | Rechazado

module DictamenCalidad =

    let aTexto =
        function
        | DictamenCalidad.Aprobado -> "Aprobado"
        | DictamenCalidad.Rechazado -> "Rechazado"

    let desdeTexto (s: string) : Result<DictamenCalidad, DomainError> =
        match (if isNull s then "" else s.Trim()) with
        | "Aprobado" | "aprobado" -> Ok DictamenCalidad.Aprobado
        | "Rechazado" | "rechazado" -> Ok DictamenCalidad.Rechazado
        | otro -> Error (ValorRequerido (sprintf "Dictamen de calidad inválido: '%s'" otro))

// ─────────────────────────────────────────────────────────────
// Entidad: AnalisisLaboratorio
// RF06 | RF14 | CU-05 | CU-14
// ─────────────────────────────────────────────────────────────

type AnalisisLaboratorio =
    { Id: LaboratorioId
      LoteId: LoteId
      FechaAnalisis: DateOnly
      Germinacion: PorcentajeCalidad
      Pureza: PorcentajeCalidad
      Humedad: PorcentajeCalidad
      Viabilidad: PorcentajeCalidad
      SemillasPurasKg: int
      SemillasImpurezasKg: int
      Dictamen: DictamenCalidad
      Observaciones: string option }

module AnalisisLaboratorio =

    /// F-LAB-05: Evalúa dictamen automático de calidad según estándares técnicos forestales
    /// Criterios estándar: Germinación >= 60%, Pureza >= 70%, Humedad <= 15%, Viabilidad >= 60%
    let evaluarDictamenCalidad
        (germinacion: PorcentajeCalidad)
        (pureza: PorcentajeCalidad)
        (humedad: PorcentajeCalidad)
        (viabilidad: PorcentajeCalidad)
        : DictamenCalidad =
        let g = PorcentajeCalidad.valor germinacion
        let p = PorcentajeCalidad.valor pureza
        let h = PorcentajeCalidad.valor humedad
        let v = PorcentajeCalidad.valor viabilidad

        if g >= 60.0m && p >= 70.0m && h <= 15.0m && v >= 60.0m then
            DictamenCalidad.Aprobado
        else
            DictamenCalidad.Rechazado

    let crear
        (id: LaboratorioId)
        (loteId: LoteId)
        (fechaAnalisis: DateOnly)
        (germinacion: PorcentajeCalidad)
        (pureza: PorcentajeCalidad)
        (humedad: PorcentajeCalidad)
        (viabilidad: PorcentajeCalidad)
        (semillasPurasKg: int)
        (semillasImpurezasKg: int)
        (dictamen: DictamenCalidad)
        (observaciones: string option)
        : Result<AnalisisLaboratorio, DomainError> =

        if semillasPurasKg < 0 then
            Error (CantidadInvalida (sprintf "La cantidad de semillas puras por kg no puede ser negativa, recibido: %d" semillasPurasKg))
        elif semillasImpurezasKg < 0 then
            Error (CantidadInvalida (sprintf "La cantidad de impurezas por kg no puede ser negativa, recibido: %d" semillasImpurezasKg))
        else
            let obsLimpio = observaciones |> Option.bind (fun s -> let t = s.Trim() in if String.IsNullOrWhiteSpace t then None else Some t)
            Ok
                { Id = id
                  LoteId = loteId
                  FechaAnalisis = fechaAnalisis
                  Germinacion = germinacion
                  Pureza = pureza
                  Humedad = humedad
                  Viabilidad = viabilidad
                  SemillasPurasKg = semillasPurasKg
                  SemillasImpurezasKg = semillasImpurezasKg
                  Dictamen = dictamen
                  Observaciones = obsLimpio }

    let reconstruir
        (id: Guid)
        (loteId: Guid)
        (fechaAnalisis: DateOnly)
        (germinacion: decimal)
        (pureza: decimal)
        (humedad: decimal)
        (viabilidad: decimal)
        (semillasPurasKg: int)
        (semillasImpurezasKg: int)
        (dictamen: string)
        (observaciones: string option)
        : Result<AnalisisLaboratorio, DomainError> =
        match DictamenCalidad.desdeTexto dictamen with
        | Error err -> Error err
        | Ok dict ->
            Ok
                { Id = LaboratorioId id
                  LoteId = LoteId loteId
                  FechaAnalisis = fechaAnalisis
                  Germinacion = PorcentajeCalidad.reconstruir germinacion
                  Pureza = PorcentajeCalidad.reconstruir pureza
                  Humedad = PorcentajeCalidad.reconstruir humedad
                  Viabilidad = PorcentajeCalidad.reconstruir viabilidad
                  SemillasPurasKg = semillasPurasKg
                  SemillasImpurezasKg = semillasImpurezasKg
                  Dictamen = dict
                  Observaciones = observaciones }
