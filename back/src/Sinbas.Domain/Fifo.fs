namespace Sinbas.Domain

module Fifo =

    /// Resuelve la asignación de lotes para una cantidad requerida usando FIFO (First In, First Out).
    /// Retorna una lista de LineaMovimiento (que asocia LoteId y Cantidad consumida de cada lote).
    let resolverFIFO
        (productoId: ProductoId)
        (cantidadRequerida: Cantidad)
        (lotes: Lote list)
        (movimientos: MovimientoInventario list)
        : Result<LineaMovimiento list, DomainError> =

        let disponibles = Stock.lotesDisponibles productoId lotes movimientos
        let totalDisponibleGramos = disponibles |> List.sumBy snd
        let reqGramos = Cantidad.enGramos cantidadRequerida

        if totalDisponibleGramos < reqGramos then
            let msg = sprintf "Stock insuficiente para el producto %A. Requerido: %M g, Disponible: %M g" productoId reqGramos totalDisponibleGramos
            Error (StockInsuficiente msg)
        else
            let rec consumir (restante: decimal) (lotesRestantes: (Lote * decimal) list) (acc: LineaMovimiento list) =
                if restante <= 0m then
                    Ok (List.rev acc)
                else
                    match lotesRestantes with
                    | [] ->
                        Ok (List.rev acc)
                    | (lote, stockGramos) :: tail ->
                        let aTomar = min restante stockGramos
                        let cantidadATomar = { Valor = aTomar; Unidad = Gramo }
                        let linea = { Referencia = lote.Id; Cantidad = cantidadATomar }
                        consumir (restante - aTomar) tail (linea :: acc)

            consumir reqGramos disponibles []
