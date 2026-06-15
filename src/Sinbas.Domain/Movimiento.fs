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

type MovimientoInventario =
    { Id: MovimientoId

      Fecha: DateTime

      Responsable: EmpleadoId

      Tipo: TipoMovimiento

      OrdenOrigen: OrdenId option

      Lineas: LineaMovimiento list

      Observaciones: string option }
