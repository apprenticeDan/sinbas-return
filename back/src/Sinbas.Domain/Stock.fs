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

    // ─────────────────────────────────────────────────────────────
    // MF-05-02 & MF-05-03: Kardex Digital y Alertas de Stock
    // ─────────────────────────────────────────────────────────────

    type NivelAlertaStock =
        | SinStock
        | BajoStock of umbralMinimoGramos: decimal
        | StockNormal

    let evaluarAlertaStock (stockDisponibleGramos: decimal) (umbralMinimoGramos: decimal) : NivelAlertaStock =
        if stockDisponibleGramos <= 0m then
            SinStock
        elif stockDisponibleGramos <= umbralMinimoGramos then
            BajoStock umbralMinimoGramos
        else
            StockNormal

    type KardexLinea =
        { MovimientoId: MovimientoId
          Fecha: System.DateTime
          Tipo: TipoMovimiento
          LoteId: LoteId
          Cantidad: Cantidad
          SaldoResultanteGramos: decimal
          Responsable: EmpleadoId
          Observaciones: string option }

    let calcularKardexProducto
        (productoId: ProductoId)
        (lotes: Lote list)
        (movimientos: MovimientoInventario list)
        : KardexLinea list =

        let lotesIds =
            lotes
            |> List.filter (fun l -> l.ProductoId = productoId)
            |> List.map (fun l -> l.Id)
            |> Set.ofList

        let movsOrdenados =
            movimientos
            |> List.sortBy (fun m -> m.Fecha)

        let (_saldoFinal, lineasKardexInvertidas) =
            movsOrdenados
            |> List.fold (fun (saldoAcum, acc) mov ->
                let signo = TipoMovimiento.signo mov.Tipo
                let lineasProducto =
                    mov.Lineas
                    |> List.filter (fun l -> Set.contains l.Referencia lotesIds)

                let (nuevoSaldo, lineasGeneradas) =
                    lineasProducto
                    |> List.fold (fun (s, accLineas) linea ->
                        let cantGramos = Cantidad.enGramos linea.Cantidad
                        let sActual = s + (signo * cantGramos)
                        let kl =
                            { MovimientoId = mov.Id
                              Fecha = mov.Fecha
                              Tipo = mov.Tipo
                              LoteId = linea.Referencia
                              Cantidad = linea.Cantidad
                              SaldoResultanteGramos = sActual
                              Responsable = mov.Responsable
                              Observaciones = mov.Observaciones }
                        (sActual, kl :: accLineas)
                    ) (saldoAcum, [])

                (nuevoSaldo, lineasGeneradas @ acc)
            ) (0m, [])

        List.rev lineasKardexInvertidas

    type StockConsolidadoProducto =
        { ProductoId: ProductoId
          StockTotalGramos: decimal
          StockDisponibleVentaGramos: decimal
          Alerta: NivelAlertaStock
          LotesStock: (Lote * decimal) list }

    let proyectarStockConsolidado
        (productos: Producto list)
        (lotes: Lote list)
        (movimientos: MovimientoInventario list)
        (umbralMinimoDefaultGramos: decimal)
        : StockConsolidadoProducto list =

        productos
        |> List.map (fun prod ->
            let prodId = prod.Base.Id
            let totalG = stockProducto prodId lotes movimientos
            let dispVentaG = disponibleParaVenta prodId lotes movimientos
            let alerta = evaluarAlertaStock dispVentaG umbralMinimoDefaultGramos
            let lotesDisp = lotesDisponibles prodId lotes movimientos
            { ProductoId = prodId
              StockTotalGramos = totalG
              StockDisponibleVentaGramos = dispVentaG
              Alerta = alerta
              LotesStock = lotesDisp })