import { Component, createSignal, For, Show, onMount } from 'solid-js';
import { stockStore } from '../store/stockStore';
import { authStore } from '../store/authStore';

export const StockView: Component = () => {
  const [tabActiva, setTabActiva] = createSignal<'existencias' | 'kardex'>('existencias');

  onMount(() => {
    stockStore.cargarStock();
    stockStore.cargarKardex();
  });

  const irAKardexProducto = (productoId: string) => {
    stockStore.setProductoKardexId(productoId);
    stockStore.cargarKardex(productoId);
    setTabActiva('kardex');
  };

  return (
    <div class="view-container">
      {/* ─── Cabecera Principal ─── */}
      <div class="view-header">
        <div>
          <div style={{ display: 'flex', 'align-items': 'center', gap: '8px' }}>
            <h1 class="view-title">Existencias & Kardex Digital</h1>
            <span class="pill pill-green" style={{ 'font-size': '11px' }}>F5 / MF-05-01..03</span>
          </div>
          <p class="view-subtitle">
            Control de inventario en tiempo real, desglose por lotes y auditoría cronológica de movimientos.
          </p>
        </div>

        {/* Selector de Pestañas */}
        <div style={{ display: 'flex', gap: '6px', background: 'var(--surface-alt)', padding: '4px', 'border-radius': 'var(--radius-m)', border: '1px solid var(--border)' }}>
          <button
            class={`btn ${tabActiva() === 'existencias' ? 'btn-primary' : 'btn-ghost'}`}
            style={{ padding: '7px 16px', 'font-size': '13px' }}
            onClick={() => setTabActiva('existencias')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '15px', height: '15px', 'margin-right': '6px' }}>
              <path d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
            </svg>
            Existencias & Lotes
          </button>
          <button
            class={`btn ${tabActiva() === 'kardex' ? 'btn-primary' : 'btn-ghost'}`}
            style={{ padding: '7px 16px', 'font-size': '13px' }}
            onClick={() => {
              setTabActiva('kardex');
              stockStore.cargarKardex();
            }}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '15px', height: '15px', 'margin-right': '6px' }}>
              <path d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            Kardex Digital
          </button>
        </div>
      </div>

      {/* ─── Tarjetas de Resumen (KPIs) ─── */}
      <div style={{ display: 'grid', 'grid-template-columns': 'repeat(auto-fit, minmax(220px, 1fr))', gap: '14px', 'margin-bottom': '20px' }}>
        <div class="card" style={{ padding: '16px', display: 'flex', 'align-items': 'center', gap: '14px' }}>
          <div style={{ width: '42px', height: '42px', 'border-radius': '10px', background: 'var(--green-tint)', color: 'var(--green-deep)', display: 'flex', 'align-items': 'center', 'justify-content': 'center' }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '22px', height: '22px' }}>
              <path d="M5 8h14M5 8a2 2 0 110-4h14a2 2 0 110 4M5 8v10a2 2 0 002 2h10a2 2 0 002-2V8m-9 4h4" />
            </svg>
          </div>
          <div>
            <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'letter-spacing': '0.05em' }}>Productos con Stock</div>
            <div style={{ 'font-family': 'var(--font-display)', 'font-size': '22px', 'font-weight': '700', color: 'var(--ink)' }}>
              {stockStore.totalProductosConStock()}
            </div>
          </div>
        </div>

        <div class="card" style={{ padding: '16px', display: 'flex', 'align-items': 'center', gap: '14px' }}>
          <div style={{ width: '42px', height: '42px', 'border-radius': '10px', background: '#E0F0FA', color: '#1B659D', display: 'flex', 'align-items': 'center', 'justify-content': 'center' }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '22px', height: '22px' }}>
              <path d="M3 6l9-4 9 4v12l-9 4-9-4V6z" />
            </svg>
          </div>
          <div>
            <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'letter-spacing': '0.05em' }}>Volumen Total Almacén</div>
            <div style={{ 'font-family': 'var(--font-display)', 'font-size': '22px', 'font-weight': '700', color: 'var(--ink)' }}>
              {stockStore.stockTotalAlmacenKg().toLocaleString()} <span style={{ 'font-size': '13px', 'font-weight': '500', color: 'var(--ink-soft)' }}>kg</span>
            </div>
          </div>
        </div>

        <div class="card" style={{ padding: '16px', display: 'flex', 'align-items': 'center', gap: '14px' }}>
          <div style={{
            width: '42px',
            height: '42px',
            'border-radius': '10px',
            background: stockStore.totalAlertasStock() > 0 ? 'var(--rust-tint)' : 'var(--green-tint)',
            color: stockStore.totalAlertasStock() > 0 ? 'var(--rust)' : 'var(--green-deep)',
            display: 'flex',
            'align-items': 'center',
            'justify-content': 'center'
          }}>
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '22px', height: '22px' }}>
              <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
          </div>
          <div>
            <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'letter-spacing': '0.05em' }}>Alertas de Reabastecimiento</div>
            <div style={{ 'font-family': 'var(--font-display)', 'font-size': '22px', 'font-weight': '700', color: stockStore.totalAlertasStock() > 0 ? 'var(--rust)' : 'var(--green)' }}>
              {stockStore.totalAlertasStock()} <span style={{ 'font-size': '12px', 'font-weight': 'normal' }}>críticas</span>
            </div>
          </div>
        </div>
      </div>

      {/* ─── Mensaje de Error / Notificación ─── */}
      <Show when={stockStore.error()}>
        <div class="alert-error" style={{ 'margin-bottom': '16px' }}>
          {stockStore.error()}
        </div>
      </Show>

      {/* ─── PESTAÑA 1: EXISTENCIAS & LOTES ─── */}
      <Show when={tabActiva() === 'existencias'}>
        {/* Barra de Filtros */}
        <div class="card" style={{ padding: '12px 16px', 'margin-bottom': '16px', display: 'flex', 'flex-wrap': 'wrap', gap: '12px', 'align-items': 'center', 'justify-content': 'space-between' }}>
          <div style={{ display: 'flex', 'align-items': 'center', gap: '10px', flex: '1', 'min-width': '260px' }}>
            <div style={{ position: 'relative', width: '100%', 'max-width': '340px' }}>
              <input
                type="text"
                class="form-input"
                style={{ 'padding-left': '32px', 'font-size': '13px' }}
                placeholder="Buscar por producto, especie o lote..."
                value={stockStore.filtroBusqueda()}
                onInput={(e) => stockStore.setFiltroBusqueda(e.currentTarget.value)}
              />
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ position: 'absolute', left: '9px', top: '50%', transform: 'translateY(-50%)', width: '15px', height: '15px', color: 'var(--ink-faint)' }}>
                <circle cx="11" cy="11" r="8" />
                <path d="M21 21l-4.35-4.35" />
              </svg>
            </div>

            {/* Filtro de Categoría */}
            <select
              class="form-input"
              style={{ width: '140px', 'font-size': '13px' }}
              value={stockStore.filtroCategoria()}
              onChange={(e) => stockStore.setFiltroCategoria(e.currentTarget.value)}
            >
              <option value="TODAS">Categorías: Todas</option>
              <option value="Semilla">Semillas</option>
              <option value="Plantin">Plantines</option>
              <option value="Insumo">Insumos</option>
              <option value="Otro">Otros</option>
            </select>

            {/* Filtro de Alerta */}
            <select
              class="form-input"
              style={{ width: '140px', 'font-size': '13px' }}
              value={stockStore.filtroAlerta()}
              onChange={(e) => stockStore.setFiltroAlerta(e.currentTarget.value)}
            >
              <option value="TODAS">Estado: Todos</option>
              <option value="ALERTA">Bajo Stock / Sin Stock</option>
              <option value="NORMAL">Stock Normal</option>
            </select>
          </div>

          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              class="btn btn-ghost"
              style={{ 'font-size': '12.5px', padding: '6px 12px' }}
              onClick={() => stockStore.cargarStock()}
              disabled={stockStore.isLoading()}
              title="Refrescar existencias"
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '14px', height: '14px', 'margin-right': '5px' }}>
                <path d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
              </svg>
              Actualizar
            </button>
          </div>
        </div>

        {/* Tabla de Existencias y Lotes */}
        <div class="card" style={{ padding: '0', overflow: 'hidden' }}>
          <Show when={stockStore.isLoading()}>
            <div style={{ padding: '30px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              Cargando existencias consolidadas...
            </div>
          </Show>

          <Show when={!stockStore.isLoading() && stockStore.productosFiltrados().length === 0}>
            <div style={{ padding: '40px 20px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              No se encontraron productos o especies con los filtros aplicados.
            </div>
          </Show>

          <Show when={!stockStore.isLoading() && stockStore.productosFiltrados().length > 0}>
            <div class="table-container" style={{ margin: '0' }}>
              <table class="table">
                <thead>
                  <tr>
                    <th style={{ width: '40px', 'text-align': 'center' }}></th>
                    <th>Producto / Especie</th>
                    <th style={{ width: '110px' }}>Categoría</th>
                    <th style={{ width: '130px', 'text-align': 'right' }}>Stock Total</th>
                    <th style={{ width: '140px', 'text-align': 'right' }}>Disp. Venta</th>
                    <th style={{ width: '120px', 'text-align': 'center' }}>Alerta Stock</th>
                    <th style={{ width: '110px', 'text-align': 'center' }}>Acciones</th>
                  </tr>
                </thead>
                <tbody>
                  <For each={stockStore.productosFiltrados()}>
                    {(p) => {
                      const isExpanded = () => stockStore.productoExpandidoId() === p.productoId;
                      return (
                        <>
                          <tr class={isExpanded() ? 'bg-surface-alt' : ''} style={{ cursor: 'pointer' }}>
                            <td style={{ 'text-align': 'center' }} onClick={() => stockStore.toggleExpandirLotes(p.productoId)}>
                              <button class="btn btn-ghost" style={{ padding: '4px', 'min-width': 'unset' }}>
                                <svg
                                  viewBox="0 0 24 24"
                                  fill="none"
                                  stroke="currentColor"
                                  stroke-width="2"
                                  style={{
                                    width: '14px',
                                    height: '14px',
                                    transform: isExpanded() ? 'rotate(90deg)' : 'rotate(0deg)',
                                    transition: 'transform 0.15s ease',
                                  }}
                                >
                                  <path d="M9 5l7 7-7 7" />
                                </svg>
                              </button>
                            </td>
                            <td onClick={() => stockStore.toggleExpandirLotes(p.productoId)}>
                              <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>{p.nombreProducto}</div>
                              <div style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>
                                {p.lotes.length} {p.lotes.length === 1 ? 'lote registrado' : 'lotes registrados'}
                              </div>
                            </td>
                            <td onClick={() => stockStore.toggleExpandirLotes(p.productoId)}>
                              <span class="pill pill-muted" style={{ 'font-size': '11px' }}>{p.categoria}</span>
                            </td>
                            <td style={{ 'text-align': 'right', 'font-family': 'var(--font-display)', 'font-weight': '600' }} onClick={() => stockStore.toggleExpandirLotes(p.productoId)}>
                              {p.stockTotalDisplay.toLocaleString()} {p.unidadMedida}
                            </td>
                            <td style={{ 'text-align': 'right', 'font-family': 'var(--font-display)', 'font-weight': '600', color: 'var(--green-deep)' }} onClick={() => stockStore.toggleExpandirLotes(p.productoId)}>
                              {p.stockDisponibleVentaDisplay.toLocaleString()} {p.unidadMedida}
                            </td>
                            <td style={{ 'text-align': 'center' }}>
                              <Show when={p.alerta === 'StockNormal'}>
                                <span class="pill pill-green" style={{ 'font-size': '11px' }}>Normal</span>
                              </Show>
                              <Show when={p.alerta === 'BajoStock'}>
                                <span class="pill pill-amber" style={{ 'font-size': '11px' }}>Bajo Stock</span>
                              </Show>
                              <Show when={p.alerta === 'SinStock'}>
                                <span class="pill pill-rust" style={{ 'font-size': '11px' }}>Sin Stock</span>
                              </Show>
                            </td>
                            <td style={{ 'text-align': 'center' }}>
                              <button
                                class="btn btn-ghost"
                                style={{ padding: '4px 8px', 'font-size': '11.5px', color: 'var(--ink-soft)' }}
                                onClick={() => irAKardexProducto(p.productoId)}
                                title="Ver historial de movimientos en Kardex"
                              >
                                Ver Kardex
                              </button>
                            </td>
                          </tr>

                          {/* Fila expandible con detalle de lotes */}
                          <Show when={isExpanded()}>
                            <tr>
                              <td colspan="7" style={{ padding: '0', background: 'var(--surface-alt)' }}>
                                <div style={{ padding: '14px 24px', 'border-bottom': '1px solid var(--border)' }}>
                                  <div style={{ 'font-size': '12px', 'font-weight': '700', color: 'var(--ink-soft)', 'margin-bottom': '8px', 'text-transform': 'uppercase', 'letter-spacing': '0.05em' }}>
                                    Lotes Físicos Asociados ({p.lotes.length})
                                  </div>

                                  <Show when={p.lotes.length === 0}>
                                    <div style={{ 'font-size': '12px', color: 'var(--ink-faint)', padding: '6px 0' }}>
                                      No existen lotes físicos activos registrados para este producto.
                                    </div>
                                  </Show>

                                  <Show when={p.lotes.length > 0}>
                                    <table style={{ width: '100%', 'font-size': '12px', 'border-collapse': 'collapse' }}>
                                      <thead>
                                        <tr style={{ color: 'var(--ink-soft)', 'border-bottom': '1px solid var(--border-soft)' }}>
                                          <th style={{ 'text-align': 'left', padding: '6px' }}>Código Estándar Lote</th>
                                          <th style={{ 'text-align': 'left', padding: '6px' }}>Fecha Ingreso</th>
                                          <th style={{ 'text-align': 'left', padding: '6px' }}>Procedencia</th>
                                          <th style={{ 'text-align': 'center', padding: '6px' }}>Estado</th>
                                          <th style={{ 'text-align': 'right', padding: '6px' }}>Existencia Lote</th>
                                          <th style={{ 'text-align': 'center', padding: '6px' }}>Filtro Kardex</th>
                                        </tr>
                                      </thead>
                                      <tbody>
                                        <For each={p.lotes}>
                                          {(lote) => (
                                            <tr style={{ 'border-bottom': '1px dotted var(--border-soft)' }}>
                                              <td style={{ padding: '6px', 'font-family': 'monospace', 'font-weight': '600' }}>
                                                {lote.codigo}
                                              </td>
                                              <td style={{ padding: '6px', color: 'var(--ink-soft)' }}>
                                                {lote.fechaIngreso}
                                              </td>
                                              <td style={{ padding: '6px', color: 'var(--ink-soft)' }}>
                                                {lote.procedencia || '—'}
                                              </td>
                                              <td style={{ padding: '6px', 'text-align': 'center' }}>
                                                <span class={`pill ${lote.estado === 'Activo' ? 'pill-green' : lote.estado === 'Rechazado' ? 'pill-rust' : 'pill-muted'}`} style={{ 'font-size': '10px' }}>
                                                  {lote.estado}
                                                </span>
                                              </td>
                                              <td style={{ padding: '6px', 'text-align': 'right', 'font-family': 'var(--font-display)', 'font-weight': '600' }}>
                                                {lote.stockDisplay.toLocaleString()} {lote.unidad}
                                              </td>
                                              <td style={{ padding: '6px', 'text-align': 'center' }}>
                                                <button
                                                  class="btn btn-ghost"
                                                  style={{ padding: '2px 6px', 'font-size': '10.5px' }}
                                                  onClick={() => {
                                                    stockStore.setProductoKardexId(p.productoId);
                                                    stockStore.setLoteKardexId(lote.loteId);
                                                    stockStore.cargarKardex(p.productoId, lote.loteId);
                                                    setTabActiva('kardex');
                                                  }}
                                                >
                                                  Filtrar Lote
                                                </button>
                                              </td>
                                            </tr>
                                          )}
                                        </For>
                                      </tbody>
                                    </table>
                                  </Show>
                                </div>
                              </td>
                            </tr>
                          </Show>
                        </>
                      );
                    }}
                  </For>
                </tbody>
              </table>
            </div>
          </Show>
        </div>
      </Show>

      {/* ─── PESTAÑA 2: KARDEX DIGITAL ─── */}
      <Show when={tabActiva() === 'kardex'}>
        <div class="card" style={{ padding: '12px 16px', 'margin-bottom': '16px', display: 'flex', 'flex-wrap': 'wrap', gap: '12px', 'align-items': 'center', 'justify-content': 'space-between' }}>
          <div style={{ display: 'flex', 'align-items': 'center', gap: '10px', flex: '1', 'min-width': '280px' }}>
            <label style={{ 'font-size': '12.5px', 'font-weight': '600', color: 'var(--ink-soft)' }}>
              Producto:
            </label>
            <select
              class="form-input"
              style={{ 'min-width': '220px', 'font-size': '13px' }}
              value={stockStore.productoKardexId()}
              onChange={(e) => {
                const val = e.currentTarget.value;
                stockStore.setProductoKardexId(val);
                stockStore.cargarKardex(val, stockStore.loteKardexId() || undefined);
              }}
            >
              <option value="">Todos los Productos (Consolidado)</option>
              <For each={stockStore.productosStock()}>
                {(prod) => (
                  <option value={prod.productoId}>{prod.nombreProducto}</option>
                )}
              </For>
            </select>

            <Show when={stockStore.productoKardexId() || stockStore.loteKardexId()}>
              <button
                class="btn btn-ghost"
                style={{ 'font-size': '11.5px', padding: '6px 10px', color: 'var(--ink-soft)' }}
                onClick={() => {
                  stockStore.setProductoKardexId('');
                  stockStore.setLoteKardexId('');
                  stockStore.cargarKardex();
                }}
              >
                Limpiar filtro
              </button>
            </Show>
          </div>

          <button
            class="btn btn-ghost"
            style={{ 'font-size': '12.5px', padding: '6px 12px' }}
            onClick={() => stockStore.cargarKardex()}
            disabled={stockStore.isLoading()}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '14px', height: '14px', 'margin-right': '5px' }}>
              <path d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            Refrescar Kardex
          </button>
        </div>

        {/* Tabla Kardex */}
        <div class="card" style={{ padding: '0', overflow: 'hidden' }}>
          <Show when={stockStore.isLoading()}>
            <div style={{ padding: '30px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              Cargando historial cronológico de movimientos...
            </div>
          </Show>

          <Show when={!stockStore.isLoading() && stockStore.kardexItems().length === 0}>
            <div style={{ padding: '40px 20px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              No se registran movimientos de inventario en el Kardex para los criterios seleccionados.
            </div>
          </Show>

          <Show when={!stockStore.isLoading() && stockStore.kardexItems().length > 0}>
            <div class="table-container" style={{ margin: '0' }}>
              <table class="table">
                <thead>
                  <tr>
                    <th style={{ width: '130px' }}>Fecha</th>
                    <th style={{ width: '100px', 'text-align': 'center' }}>Tipo</th>
                    <th>Motivo / Destino</th>
                    <th>Producto & Lote</th>
                    <th style={{ width: '110px', 'text-align': 'right' }}>Cantidad</th>
                    <th style={{ width: '140px', 'text-align': 'right', background: 'var(--surface-alt)' }}>
                      Saldo Resultante
                    </th>
                    <th style={{ width: '150px' }}>Observaciones</th>
                  </tr>
                </thead>
                <tbody>
                  <For each={stockStore.kardexItems()}>
                    {(item) => (
                      <tr>
                        <td style={{ 'font-size': '12px', color: 'var(--ink-soft)' }}>
                          {item.fecha}
                        </td>
                        <td style={{ 'text-align': 'center' }}>
                          <span
                            class={`pill ${item.tipo === 'Entrada' ? 'pill-green' : 'pill-rust'}`}
                            style={{ 'font-size': '11px' }}
                          >
                            {item.tipo}
                          </span>
                        </td>
                        <td>
                          <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>{item.motivo}</div>
                        </td>
                        <td>
                          <div style={{ 'font-weight': '500', color: 'var(--ink)' }}>{item.nombreProducto}</div>
                          <div style={{ 'font-family': 'monospace', 'font-size': '11px', color: 'var(--ink-soft)' }}>
                            Lote: {item.codigoLote}
                          </div>
                        </td>
                        <td style={{ 'text-align': 'right', 'font-family': 'var(--font-display)', 'font-weight': '600', color: item.tipo === 'Entrada' ? 'var(--green-deep)' : 'var(--rust)' }}>
                          {item.tipo === 'Entrada' ? '+' : '-'}{item.cantidad.toLocaleString()} {item.unidad}
                        </td>
                        <td style={{ 'text-align': 'right', 'font-family': 'var(--font-display)', 'font-weight': '700', background: 'var(--surface-alt)', color: 'var(--ink)' }}>
                          {item.saldoResultanteDisplay.toLocaleString()} {item.unidad}
                        </td>
                        <td style={{ 'font-size': '12px', color: 'var(--ink-soft)' }}>
                          {item.observaciones || '—'}
                        </td>
                      </tr>
                    )}
                  </For>
                </tbody>
              </table>
            </div>
          </Show>
        </div>
      </Show>
    </div>
  );
};
export default StockView;
