namespace Sinbas.Domain

open System

type EstadoLote =
    | Activo
    | Agotado
    | Bloqueado
    | Archivado

type Lote =
    { Id: LoteId
      Codigo: CodigoLote
      ProductoId: ProductoId

      FechaIngreso: DateOnly

      UbicacionId: UbicacionId option

      Estado: EstadoLote

      Observaciones: string option }

module Lote =

    let estaActivo lote = lote.Estado = Activo

    let marcarAgotado lote = { lote with Estado = Agotado }

    let bloquear motivo lote =

        let observaciones =
            match lote.Observaciones with
            | None -> Some motivo

            | Some prev -> Some(sprintf "%s | %s" prev motivo)

        { lote with
            Estado = Bloqueado
            Observaciones = observaciones }

    let archivar lote = { lote with Estado = Archivado }
