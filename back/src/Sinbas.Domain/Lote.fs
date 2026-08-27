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
      Procedencia: string option
      CantidadInicial: Cantidad
      CantidadActual: Cantidad
      FechaIngreso: DateOnly
      Ubicacion: string option
      Estado: EstadoLote
      Observaciones: string option }

module Lote =

    let estaActivo lote = lote.Estado = Activo

    let crear
        (id: LoteId)
        (codigo: CodigoLote)
        (productoId: ProductoId)
        (procedencia: string option)
        (cantidadInicial: Cantidad)
        (fechaIngreso: DateOnly)
        (ubicacion: string option)
        (observaciones: string option)
        : Result<Lote, DomainError> =
        if cantidadInicial.Valor <= 0m then
            Error(CantidadInvalida "La cantidad inicial de ingreso al lote debe ser mayor a cero")
        else
            Ok
                { Id = id
                  Codigo = codigo
                  ProductoId = productoId
                  Procedencia = procedencia
                  CantidadInicial = cantidadInicial
                  CantidadActual = cantidadInicial
                  FechaIngreso = fechaIngreso
                  Ubicacion = ubicacion
                  Estado = Activo
                  Observaciones = observaciones }

    let descontarStock (cantidadADescontar: Cantidad) (lote: Lote) : Result<Lote, DomainError> =
        match Cantidad.esSuficiente lote.CantidadActual cantidadADescontar with
        | Error err -> Error err
        | Ok false ->
            Error(
                StockInsuficiente(
                    sprintf
                        "Stock insuficiente en lote '%s'. Disponible: %s, Solicitado: %s"
                        (CodigoLote.valor lote.Codigo)
                        (Cantidad.formatear lote.CantidadActual)
                        (Cantidad.formatear cantidadADescontar)
                )
            )
        | Ok true ->
            let disponibleGramos = Cantidad.enGramos lote.CantidadActual
            let aDescontarGramos = Cantidad.enGramos cantidadADescontar
            let restanteGramos = disponibleGramos - aDescontarGramos

            let nuevaCantidadActual =
                { Valor = restanteGramos
                  Unidad = Gramo }

            let nuevoEstado =
                if restanteGramos <= 0m then
                    Agotado
                else
                    lote.Estado

            Ok
                { lote with
                    CantidadActual = nuevaCantidadActual
                    Estado = nuevoEstado }

    let marcarAgotado lote =
        { lote with
            CantidadActual = { lote.CantidadActual with Valor = 0m }
            Estado = Agotado }

    let bloquear motivo lote =
        let observaciones =
            match lote.Observaciones with
            | None -> Some(sprintf "[BLOQUEADO]: %s" motivo)
            | Some prev -> Some(sprintf "%s | [BLOQUEADO]: %s" prev motivo)

        { lote with
            Estado = Bloqueado
            Observaciones = observaciones }

    let archivar lote = { lote with Estado = Archivado }
