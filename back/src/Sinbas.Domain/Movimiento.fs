namespace Sinbas.Domain

open System

type TipoMovimiento =
    | Entrada of MotivoIngreso
    | Salida of MotivoEgreso

module TipoMovimiento =
    let signo =
        function
        | Entrada _ -> 1m
        | Salida _ -> -1m

    let aTexto =
        function
        | Entrada _ -> "Entrada"
        | Salida _ -> "Salida"

    let resolver
        (tipoStr: string)
        (motivoStr: string)
        (contraparteRef: Guid option)
        (contraparteNombre: string option)
        (departamento: string option)
        (solicitante: string option)
        (observaciones: string option)
        : Result<TipoMovimiento, DomainError> =
        match (if isNull tipoStr then "" else tipoStr.Trim()) with
        | "Entrada" ->
            MotivoIngreso.desdeTexto motivoStr contraparteRef contraparteNombre
            |> Result.map Entrada
        | "Salida" ->
            MotivoEgreso.desdeTexto motivoStr contraparteRef contraparteNombre departamento solicitante observaciones
            |> Result.map Salida
        | desconocido ->
            Error (ValorRequerido (sprintf "Tipo de movimiento desconocido o inválido: '%s'" desconocido))

type MovimientoInventario =
    { Id: MovimientoId

      Fecha: DateTime

      Responsable: EmpleadoId

      Tipo: TipoMovimiento

      OrdenOrigen: OrdenId option

      Lineas: LineaMovimiento list

      Observaciones: string option }
