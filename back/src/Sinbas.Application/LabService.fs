namespace Sinbas.Application

open System
open Sinbas.Domain

[<CLIMutable>]
type RegistrarAnalisisRequest =
    { Germinacion: decimal
      Pureza: decimal
      Humedad: decimal
      Viabilidad: decimal
      SemillasPurasKg: int
      SemillasImpurezasKg: int
      DictamenManual: string
      FechaAnalisis: string
      Observaciones: string }

[<CLIMutable>]
type AnalisisDto =
    { Id: string
      LoteId: string
      FechaAnalisis: string
      Germinacion: decimal
      Pureza: decimal
      Humedad: decimal
      Viabilidad: decimal
      SemillasPurasKg: int
      SemillasImpurezasKg: int
      Dictamen: string
      Observaciones: string option }

[<CLIMutable>]
type DatosEtiquetaDto =
    { LoteId: string
      CodigoLote: string
      ProductoId: string
      NombreProducto: string
      Procedencia: string option
      FechaIngreso: string
      FechaAnalisis: string
      GerminacionPorcentaje: decimal
      PurezaPorcentaje: decimal
      HumedadPorcentaje: decimal
      ViabilidadPorcentaje: decimal
      SemillasPurasKg: int
      Dictamen: string
      Observaciones: string option }

module LabService =

    let private aAnalisisDto (a: AnalisisLaboratorio) : AnalisisDto =
        let (LaboratorioId id) = a.Id
        let (LoteId lId) = a.LoteId
        { Id = id.ToString()
          LoteId = lId.ToString()
          FechaAnalisis = a.FechaAnalisis.ToString("yyyy-MM-dd")
          Germinacion = PorcentajeCalidad.valor a.Germinacion
          Pureza = PorcentajeCalidad.valor a.Pureza
          Humedad = PorcentajeCalidad.valor a.Humedad
          Viabilidad = PorcentajeCalidad.valor a.Viabilidad
          SemillasPurasKg = a.SemillasPurasKg
          SemillasImpurezasKg = a.SemillasImpurezasKg
          Dictamen = DictamenCalidad.aTexto a.Dictamen
          Observaciones = a.Observaciones }

    let aDatosEtiquetaDto (e: DatosEtiqueta) : DatosEtiquetaDto =
        let (LoteId lId) = e.LoteId
        let (ProductoId pId) = e.ProductoId
        { LoteId = lId.ToString()
          CodigoLote = e.CodigoLote
          ProductoId = pId.ToString()
          NombreProducto = e.NombreProducto
          Procedencia = e.Procedencia
          FechaIngreso = e.FechaIngreso.ToString("yyyy-MM-dd")
          FechaAnalisis = e.FechaAnalisis.ToString("yyyy-MM-dd")
          GerminacionPorcentaje = e.GerminacionPorcentaje
          PurezaPorcentaje = e.PurezaPorcentaje
          HumedadPorcentaje = e.HumedadPorcentaje
          ViabilidadPorcentaje = e.ViabilidadPorcentaje
          SemillasPurasKg = e.SemillasPurasKg
          Dictamen = e.Dictamen
          Observaciones = e.Observaciones }

    /// F-LAB-02 / CU-05: Registra los resultados de laboratorio para un lote y actualiza su estado (RN12)
    let registrarAnalisis
        (obtenerLotePorId: LoteId -> Async<Lote option>)
        (actualizarLoteStockYEstado: Lote -> Async<unit>)
        (insertarAnalisis: AnalisisLaboratorio -> Async<unit>)
        (loteIdRaw: string)
        (req: RegistrarAnalisisRequest)
        : Async<Result<AnalisisDto, string>> =
        async {
            match Guid.TryParse(loteIdRaw) with
            | false, _ -> return Error "Identificador de lote inválido (formato UUID requerido)"
            | true, lGuid ->
                let loteId = LoteId lGuid
                let! loteOpt = obtenerLotePorId loteId
                match loteOpt with
                | None -> return Error (sprintf "No se encontró el lote con ID '%s'" loteIdRaw)
                | Some lote ->
                    // Validar porcentajes de calidad
                    let resGerm = PorcentajeCalidad.crear req.Germinacion
                    let resPureza = PorcentajeCalidad.crear req.Pureza
                    let resHum = PorcentajeCalidad.crear req.Humedad
                    let resViab = PorcentajeCalidad.crear req.Viabilidad

                    match resGerm, resPureza, resHum, resViab with
                    | Error (PorcentajeInvalido msg), _, _, _ -> return Error (sprintf "Germinación inválida: %s" msg)
                    | Error err, _, _, _ -> return Error (sprintf "Germinación inválida: %A" err)
                    | _, Error (PorcentajeInvalido msg), _, _ -> return Error (sprintf "Pureza inválida: %s" msg)
                    | _, Error err, _, _ -> return Error (sprintf "Pureza inválida: %A" err)
                    | _, _, Error (PorcentajeInvalido msg), _ -> return Error (sprintf "Humedad inválida: %s" msg)
                    | _, _, Error err, _ -> return Error (sprintf "Humedad inválida: %A" err)
                    | _, _, _, Error (PorcentajeInvalido msg) -> return Error (sprintf "Viabilidad inválida: %s" msg)
                    | _, _, _, Error err -> return Error (sprintf "Viabilidad inválida: %A" err)
                    | Ok germ, Ok pureza, Ok hum, Ok viab ->
                        // Validar fechas
                        let hoy = DateOnly.FromDateTime(DateTime.UtcNow)
                        let fechaAnalisis =
                            if not (String.IsNullOrWhiteSpace req.FechaAnalisis) then
                                match DateOnly.TryParse(req.FechaAnalisis) with
                                | true, d -> d
                                | false, _ -> hoy
                            else hoy

                        if fechaAnalisis > hoy then
                            return Error "La fecha del análisis no puede ser futura"
                        elif fechaAnalisis < lote.FechaIngreso then
                            return Error (sprintf "La fecha del análisis (%s) no puede ser anterior a la recepción del lote (%s)"
                                            (fechaAnalisis.ToString("yyyy-MM-dd"))
                                            (lote.FechaIngreso.ToString("yyyy-MM-dd")))
                        else
                            // Resolver dictamen: sugerido o manual
                            let dictamen =
                                if not (String.IsNullOrWhiteSpace req.DictamenManual) then
                                    match DictamenCalidad.desdeTexto req.DictamenManual with
                                    | Ok d -> d
                                    | Error _ -> AnalisisLaboratorio.evaluarDictamenCalidad germ pureza hum viab
                                else
                                    AnalisisLaboratorio.evaluarDictamenCalidad germ pureza hum viab

                            let labId = LaboratorioId (Identidad.nuevo ())
                            let obsOpt = if String.IsNullOrWhiteSpace req.Observaciones then None else Some(req.Observaciones.Trim())

                            match AnalisisLaboratorio.crear labId loteId fechaAnalisis germ pureza hum viab req.SemillasPurasKg req.SemillasImpurezasKg dictamen obsOpt with
                            | Error (CantidadInvalida msg) -> return Error msg
                            | Error err -> return Error (sprintf "Error al construir análisis: %A" err)
                            | Ok nuevoAnalisis ->
                                do! insertarAnalisis nuevoAnalisis

                                // RN12: Si el dictamen es Rechazado, actualizar estado del lote en almacén
                                let loteActualizado = Lote.aplicarDictamenLaboratorio dictamen lote
                                if loteActualizado.Estado <> lote.Estado then
                                    do! actualizarLoteStockYEstado loteActualizado

                                return Ok (aAnalisisDto nuevoAnalisis)
        }

    /// F-LAB-03 / CU-06 / RN01: Genera el archivo PDF con la etiqueta oficial del lote
    let generarEtiquetaPdf
        (obtenerLotePorId: LoteId -> Async<Lote option>)
        (obtenerProductoPorId: ProductoId -> Async<Producto option>)
        (obtenerUltimoAnalisisPorLoteId: LoteId -> Async<AnalisisLaboratorio option>)
        (generadorPdf: DatosEtiqueta -> byte[])
        (loteIdRaw: string)
        : Async<Result<byte[] * string, string>> =
        async {
            match Guid.TryParse(loteIdRaw) with
            | false, _ -> return Error "Identificador de lote inválido"
            | true, lGuid ->
                let loteId = LoteId lGuid
                let! loteOpt = obtenerLotePorId loteId
                match loteOpt with
                | None -> return Error (sprintf "No se encontró el lote '%s'" loteIdRaw)
                | Some lote ->
                    let! prodOpt = obtenerProductoPorId lote.ProductoId
                    let nombreProducto =
                        match prodOpt with
                        | Some p -> Producto.nombreVisible p
                        | None -> "Semilla Forestal"

                    let! analisisOpt = obtenerUltimoAnalisisPorLoteId loteId

                    match Etiqueta.construirDatosEtiqueta lote nombreProducto analisisOpt with
                    | Error (SinAnalisisLaboratorio msg) ->
                        return Error (sprintf "ERR_SIN_ANALISIS_LABORATORIO: %s" msg)
                    | Error err ->
                        return Error (sprintf "Error al generar etiqueta: %A" err)
                    | Ok datosEtiqueta ->
                        let pdfBytes = generadorPdf datosEtiqueta
                        let fileName = sprintf "etiqueta-%s.pdf" (CodigoLote.valor lote.Codigo)
                        return Ok (pdfBytes, fileName)
        }

    /// Obtiene la lista cronológica de análisis para un lote
    let listarAnalisisPorLote
        (listarPorLote: LoteId -> Async<AnalisisLaboratorio list>)
        (loteIdRaw: string)
        : Async<Result<AnalisisDto list, string>> =
        async {
            match Guid.TryParse(loteIdRaw) with
            | false, _ -> return Error "Identificador de lote inválido"
            | true, lGuid ->
                let! lista = listarPorLote (LoteId lGuid)
                return Ok (lista |> List.map aAnalisisDto)
        }
