namespace Sinbas.Infrastructure

open System
open System.Text
open Sinbas.Domain

module PdfEtiquetaService =

    let private escapePdf (s: string) =
        if isNull s then ""
        else s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)")

    /// Genera un búfer de bytes en formato PDF 1.4 estándar con la etiqueta oficial del lote (RF07 / RN01)
    let generarPdfEtiqueta (datos: DatosEtiqueta) : byte[] =
        let procedenciaStr = defaultArg datos.Procedencia "No especificada"
        let obsStr = defaultArg datos.Observaciones "Sin observaciones"

        let streamContent =
            sprintf "BT\n/F1 16 Tf\n50 770 Td\n(SISTEMA DE INFORMACION BASFOR - SINBAS) Tj\n0 -25 Td\n/F1 13 Tf\n(ETIQUETA OFICIAL DE CALIDAD DE SEMILLAS) Tj\n0 -30 Td\n/F1 10 Tf\n(Codigo de Lote: %s) Tj\n0 -18 Td\n(Especie / Producto: %s) Tj\n0 -18 Td\n(Procedencia: %s) Tj\n0 -18 Td\n(Fecha de Ingreso: %s) Tj\n0 -18 Td\n(Fecha de Analisis: %s) Tj\n0 -28 Td\n/F1 12 Tf\n(RESULTADOS DE LABORATORIO:) Tj\n0 -20 Td\n/F1 10 Tf\n(  - Germinacion: %.2f %%) Tj\n0 -16 Td\n(  - Pureza Fisica: %.2f %%) Tj\n0 -16 Td\n(  - Contenido de Humedad: %.2f %%) Tj\n0 -16 Td\n(  - Viabilidad: %.2f %%) Tj\n0 -16 Td\n(  - Semillas Puras / kg: %d) Tj\n0 -28 Td\n/F1 12 Tf\n(DICTAMEN OFICIAL: %s) Tj\n0 -20 Td\n/F1 9 Tf\n(Observaciones: %s) Tj\n0 -30 Td\n(Certificado oficial emitido conforme a RN01 / RF07 - Centro de Semillas Forestales BASFOR) Tj\nET"
                (escapePdf datos.CodigoLote)
                (escapePdf datos.NombreProducto)
                (escapePdf procedenciaStr)
                (datos.FechaIngreso.ToString("yyyy-MM-dd"))
                (datos.FechaAnalisis.ToString("yyyy-MM-dd"))
                datos.GerminacionPorcentaje
                datos.PurezaPorcentaje
                datos.HumedadPorcentaje
                datos.ViabilidadPorcentaje
                datos.SemillasPurasKg
                (escapePdf datos.Dictamen)
                (escapePdf obsStr)

        let streamBytes = Encoding.UTF8.GetBytes(streamContent)
        let streamLen = streamBytes.Length

        let pdf = sprintf "%%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>\nendobj\n4 0 obj\n<< /Length %d >>\nstream\n%s\nendstream\nendobj\n5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\nxref\n0 6\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \n0000000234 00000 n \n0000000300 00000 n \ntrailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n370\n%%%%EOF" streamLen streamContent

        Encoding.UTF8.GetBytes(pdf)
