namespace Sinbas.Application

open System
open Sinbas.Domain

/// Representa una anomalía o inconsistencia detectada al leer una fila persistida en base de datos.
/// El repositorio captura el DomainError del dominio puro y lo enriquece con metadatos técnicos.
[<CLIMutable>]
type ErrorIntegridad =
    { Entidad: string           // ej. "producto", "lote", "movimiento_inventario", "analisis_laboratorio"
      RegistroId: string        // UUID o clave primaria en formato texto
      Campo: string             // ej. "estado", "tipo", "cantidad", "gramos_nominales"
      ValorCrudo: string option // ej. Some "-5.00", Some "EnRevisionDesconocido"
      ErrorDominio: DomainError }

/// Estructura de partición que separa registros íntegros de registros corruptos (patrón cuarentena).
/// Permite degradar listados informativos de forma transparente o bloquear transacciones críticas.
type LecturaColeccion<'T> =
    { Validos: 'T list
      Inconsistencias: ErrorIntegridad list }

module LecturaColeccion =

    let vacia : LecturaColeccion<'T> =
        { Validos = []
          Inconsistencias = [] }

    let crear (validos: 'T list) (inconsistencias: ErrorIntegridad list) : LecturaColeccion<'T> =
        { Validos = validos
          Inconsistencias = inconsistencias }

    let tieneInconsistencias (col: LecturaColeccion<'T>) : bool =
        not (List.isEmpty col.Inconsistencias)

    let totalFilas (col: LecturaColeccion<'T>) : int =
        col.Validos.Length + col.Inconsistencias.Length

    /// Itera una secuencia de filas, ejecutando la función de mapeo.
    /// Registra en log estructurado con prefijo [INTEGRIDAD_CRITICA] cada fila corrupta
    /// y retorna la partición de elementos válidos e inconsistencias.
    let particionar (mapear: 'Row -> Result<'T, ErrorIntegridad>) (filas: seq<'Row>) : LecturaColeccion<'T> =
        let valids = ResizeArray<'T>()
        let errors = ResizeArray<ErrorIntegridad>()
        for r in filas do
            match mapear r with
            | Ok item -> valids.Add item
            | Error err ->
                eprintfn "[INTEGRIDAD_CRITICA] Inconsistencia en entidad '%s' id=%s campo='%s' valor=%A: %A"
                    err.Entidad err.RegistroId err.Campo err.ValorCrudo err.ErrorDominio
                errors.Add err
        { Validos = Seq.toList valids
          Inconsistencias = Seq.toList errors }
