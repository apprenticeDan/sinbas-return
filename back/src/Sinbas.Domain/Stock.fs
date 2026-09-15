namespace Sinbas.Domain

module Stock =

    /// Stock disponible de un lote según su saldo actual proyectado en gramos
    let stockLote (lote: Lote) : decimal =
        if lote.Estado = Activo then
            Cantidad.enGramos lote.CantidadActual
        else
            0m

    /// Stock acumulado histórico a partir de movimientos de inventario (auditoría / conciliación)
    let cantidadLote (loteId: LoteId) (movimientos: MovimientoInventario list) : decimal =
        movimientos
        |> List.sumBy (fun mov ->
            let signo = TipoMovimiento.signo mov.Tipo
            mov.Lineas
            |> List.filter (fun l -> l.Referencia = loteId)
            |> List.sumBy (fun l -> signo * Cantidad.enGramos l.Cantidad))

    /// Stock total de un producto sumando el saldo de todos sus lotes activos
    let stockProducto (productoId: ProductoId) (lotes: Lote list) : decimal =
        lotes
        |> List.filter (fun l -> l.ProductoId = productoId && Lote.estaActivo l)
        |> List.sumBy (fun l -> Cantidad.enGramos l.CantidadActual)

    /// Stock disponible para venta (lotes únicamente en estado Activo)
    let disponibleParaVenta (productoId: ProductoId) (lotes: Lote list) : decimal =
        stockProducto productoId lotes

    /// Obtiene lotes activos ordenados por FechaIngreso (FIFO) que poseen stock mayor a cero
    let lotesDisponibles (productoId: ProductoId) (lotes: Lote list) : (Lote * decimal) list =
        lotes
        |> List.filter (fun l -> l.ProductoId = productoId && Lote.estaActivo l)
        |> List.map (fun l -> l, Cantidad.enGramos l.CantidadActual)
        |> List.filter (fun (_, stock) -> stock > 0m)
        |> List.sortBy (fun (l, _) -> l.FechaIngreso)

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

    /// Reconstruye el historial de movimientos (Kardex) recalculando saldos cronológicamente
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
        (umbralMinimoDefaultGramos: decimal)
        : StockConsolidadoProducto list =

        productos
        |> List.map (fun prod ->
            let prodId = prod.Base.Id
            let totalG = stockProducto prodId lotes
            let dispVentaG = disponibleParaVenta prodId lotes
            let alerta = evaluarAlertaStock dispVentaG umbralMinimoDefaultGramos
            let lotesDisp = lotesDisponibles prodId lotes
            { ProductoId = prodId
              StockTotalGramos = totalG
              StockDisponibleVentaGramos = dispVentaG
              Alerta = alerta
              LotesStock = lotesDisp })