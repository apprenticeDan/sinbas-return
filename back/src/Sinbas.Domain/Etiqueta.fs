namespace Sinbas.Domain

open System

// ─────────────────────────────────────────────────────────────
// Estructura de Datos para Etiqueta Oficial de Lote
// RF07 | RN01 | CU-06 | F-LAB-03
// ─────────────────────────────────────────────────────────────

type DatosEtiqueta =
    { LoteId: LoteId
      CodigoLote: string
      ProductoId: ProductoId
      NombreProducto: string
      Procedencia: string option
      FechaIngreso: DateOnly
      FechaAnalisis: DateOnly
      GerminacionPorcentaje: decimal
      PurezaPorcentaje: decimal
      HumedadPorcentaje: decimal
      ViabilidadPorcentaje: decimal
      SemillasPurasKg: int
      Dictamen: string
      Observaciones: string option }

module Etiqueta =

    /// F-LAB-03 / RN01: Construye la estructura de datos requerida para generar la etiqueta oficial.
    /// Exige obligatoriamente la presencia de un análisis de laboratorio registrado.
    let construirDatosEtiqueta
        (lote: Lote)
        (nombreProducto: string)
        (analisisOpt: AnalisisLaboratorio option)
        : Result<DatosEtiqueta, DomainError> =

        match analisisOpt with
        | None ->
            Error (SinAnalisisLaboratorio (sprintf "El lote '%s' no cuenta con análisis de laboratorio registrado. No se puede generar la etiqueta oficial." (CodigoLote.valor lote.Codigo)))
        | Some analisis ->
            let (ProductoId pId) = lote.ProductoId
            let nombre = if String.IsNullOrWhiteSpace nombreProducto then "Semilla Forestal" else nombreProducto.Trim()

            Ok
                { LoteId = lote.Id
                  CodigoLote = CodigoLote.valor lote.Codigo
                  ProductoId = lote.ProductoId
                  NombreProducto = nombre
                  Procedencia = lote.Procedencia
                  FechaIngreso = lote.FechaIngreso
                  FechaAnalisis = analisis.FechaAnalisis
                  GerminacionPorcentaje = PorcentajeCalidad.valor analisis.Germinacion
                  PurezaPorcentaje = PorcentajeCalidad.valor analisis.Pureza
                  HumedadPorcentaje = PorcentajeCalidad.valor analisis.Humedad
                  ViabilidadPorcentaje = PorcentajeCalidad.valor analisis.Viabilidad
                  SemillasPurasKg = analisis.SemillasPurasKg
                  Dictamen = DictamenCalidad.aTexto analisis.Dictamen
                  Observaciones = analisis.Observaciones }
