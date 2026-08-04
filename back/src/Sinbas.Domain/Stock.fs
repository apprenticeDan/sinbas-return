namespace Sinbas.Domain

module Stock =

    /// Stock disponible de un lote específico en gramos
    let cantidadLote (loteId: LoteId) (movimientos: MovimientoInventario list) : decimal =
        movimientos
        |> List.sumBy (fun mov ->
            let signo = TipoMovimiento.signo mov.Tipo

            mov.Lineas
            |> List.filter (fun l -> l.Referencia = loteId)
            |> List.sumBy (fun l -> signo * Cantidad.enGramos l.Cantidad))

    /// Stock total de un producto sumando todos sus lotes activos
    let stockProducto (productoId: ProductoId) (lotes: Lote list) (movimientos: MovimientoInventario list) : decimal =
        lotes
        |> List.filter (fun l -> l.ProductoId = productoId && Lote.estaActivo l)
        |> List.sumBy (fun l -> cantidadLote l.Id movimientos)


    /// Stock disponible filtrando lotes bloqueados o archivados
    let disponibleParaVenta
        (productoId: ProductoId)
        (lotes: Lote list)
        (movimientos: MovimientoInventario list)
        : decimal =
        lotes
        |> List.filter (fun l ->
            l.ProductoId = productoId
            && match l.Estado with
               | Activo -> true
               | _ -> false)
        |> List.sumBy (fun l -> cantidadLote l.Id movimientos)

    let stockLote (lote: Lote) (movimientos: MovimientoInventario list) : decimal =
        cantidadLote lote.Id movimientos

    let lotesDisponibles
        (productoId : ProductoId)
        (lotes : Lote list)
        (movimientos : MovimientoInventario list)
        : (Lote * decimal) list =

        lotes
        |> List.filter (fun l ->
            l.ProductoId = productoId
            && l.Estado = Activo)
        |> List.map (fun l ->
            l,
            cantidadLote l.Id movimientos)
        |> List.filter (fun (_, stock) ->
            stock > 0m)
        |> List.sortBy (fun (l, _) ->
            l.FechaIngreso)