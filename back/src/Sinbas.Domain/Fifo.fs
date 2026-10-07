namespace Sinbas.Domain

module Fifo =

    /// Resuelve la asignación de lotes para una cantidad requerida usando FIFO (First In, First Out).
    /// Retorna una lista de LineaMovimiento (asocia LoteId y Cantidad consumida de cada lote).
    /// Desacoplado de la lista histórica de movimientos: consulta directamente el saldo de lotes activos.
    let resolverFIFO
        (productoId: ProductoId)
        (cantidadRequerida: Cantidad)
        (lotes: Lote list)
        : Result<LineaMovimiento list, DomainError> =
        if cantidadRequerida.Valor <= 0m then
            Error (CantidadInvalida (sprintf "La cantidad requerida debe ser mayor a cero, recibido: %M" cantidadRequerida.Valor))
        else
            let disponibles = Stock.lotesDisponibles productoId lotes
            let totalDisponibleBase = disponibles |> List.sumBy snd
            let reqBase = Cantidad.aUnidadBase cantidadRequerida

            if totalDisponibleBase < reqBase then
                let msg =
                    sprintf "Stock insuficiente para el producto %A. Requerido: %s, Disponible: %g %s"
                        productoId
                        (Cantidad.formatear cantidadRequerida)
                        (float totalDisponibleBase)
                        (UnidadMedida.etiqueta (UnidadMedida.unidadBase (Cantidad.unidad cantidadRequerida)))
                Error (StockInsuficiente msg)
            else
            let rec consumir (restante: decimal) (lotesRestantes: (Lote * decimal) list) (acc: LineaMovimiento list) =
                if restante <= 0m then
                    Ok (List.rev acc)
                else
                    match lotesRestantes with
                    | [] ->
                        Ok (List.rev acc)
                    | (lote, stockBase) :: tail ->
                        let aTomarBase = min restante stockBase
                        let targetUnit = lote.CantidadActual.Unidad
                        let uBase = UnidadMedida.unidadBase targetUnit
                        match UnidadMedida.convertir uBase targetUnit aTomarBase with
                        | Error err -> Error err
                        | Ok valorEnUnidadLote ->
                            match Cantidad.reconstruir valorEnUnidadLote targetUnit with
                            | Error err -> Error err
                            | Ok cantidadATomar ->
                                let linea = { Referencia = lote.Id; Cantidad = cantidadATomar }
                                consumir (restante - aTomarBase) tail (linea :: acc)

            consumir reqBase disponibles []
