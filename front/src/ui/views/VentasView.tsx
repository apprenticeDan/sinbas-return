import { Component, createSignal, For, Show, createMemo, onMount } from 'solid-js';
import { ventaStore } from '../store/ventaStore';
import type { OrdenVentaDto } from '../../infrastructure/api/ApiVentaGateway';

export const VentasView: Component = () => {
  const [filtroEstado, setFiltroEstado] = createSignal('');
  const [filtroBusqueda, setFiltroBusqueda] = createSignal('');
  const [ventaDetalle, setVentaDetalle] = createSignal<OrdenVentaDto | null>(null);
  const [modalDetalleOpen, setModalDetalleOpen] = createSignal(false);

  onMount(async () => {
    await ventaStore.cargarVentas();
  });

  const ventasFiltradas = createMemo(() => {
    const estado = filtroEstado().trim();
    const q = filtroBusqueda().trim().toLowerCase();
    return ventaStore.ventas().filter((v) => {
      const matchEstado = !estado || v.estado === estado;
      const matchQ =
        !q ||
        v.codigo.toLowerCase().includes(q) ||
        v.clienteNombre.toLowerCase().includes(q) ||
        v.proformaOrigenId.toLowerCase().includes(q);
      return matchEstado && matchQ;
    });
  });

  const kpis = createMemo(() => {
    const lista = ventaStore.ventas();
    const total = lista.length;
    const confirmadas = lista.filter((v) => v.estado === 'Confirmada').length;
    const despachadas = lista.filter((v) => v.estado === 'Despachada').length;
    const montoTotal = lista
      .filter((v) => v.estado !== 'Anulada')
      .reduce((sum, v) => sum + Number(v.total || 0), 0);
    return { total, confirmadas, despachadas, montoTotal };
  });

  const estadoBadgeClass = (estado: string) => {
    switch (estado) {
      case 'Confirmada':
        return 'pill pill-amber';
      case 'Despachada':
        return 'pill pill-green';
      case 'Anulada':
        return 'pill pill-rust';
      default:
        return 'pill';
    }
  };

  const verDetalle = (venta: OrdenVentaDto) => {
    setVentaDetalle(venta);
    setModalDetalleOpen(true);
  };

  return (
    <section class="panel">
      {/* ─── Encabezado de Vista ─── */}
      <div class="panel-head">
        <div>
          <p class="panel-eyebrow">Gestión Comercial & Facturación</p>
          <h1 class="panel-title">Órdenes de Venta (F8)</h1>
          <p class="panel-desc">
            Seguimiento de órdenes confirmadas desde proformas vigentes, trazabilidad de stock congelado y estado de despacho físico en almacén.
          </p>
        </div>
      </div>

      {/* ─── KPIs Resumen ─── */}
      <div style={{ display: 'grid', 'grid-template-columns': 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', 'margin-bottom': '20px' }}>
        <div class="card" style={{ padding: '16px', display: 'flex', 'flex-direction': 'column', gap: '4px' }}>
          <span style={{ 'font-size': '12px', color: 'var(--ink-soft)', 'font-weight': '600', 'text-transform': 'uppercase' }}>
            Total Ventas
          </span>
          <span style={{ 'font-size': '24px', 'font-weight': '700', color: 'var(--ink)' }}>
            {kpis().total}
          </span>
          <span style={{ 'font-size': '11px', color: 'var(--ink-faint)' }}>Registros históricos</span>
        </div>

        <div class="card" style={{ padding: '16px', display: 'flex', 'flex-direction': 'column', gap: '4px', 'border-left': '4px solid var(--amber)' }}>
          <span style={{ 'font-size': '12px', color: 'var(--amber)', 'font-weight': '600', 'text-transform': 'uppercase' }}>
            Pendientes de Despacho
          </span>
          <span style={{ 'font-size': '24px', 'font-weight': '700', color: 'var(--amber)' }}>
            {kpis().confirmadas}
          </span>
          <span style={{ 'font-size': '11px', color: 'var(--ink-faint)' }}>Esperando salida física en almacén</span>
        </div>

        <div class="card" style={{ padding: '16px', display: 'flex', 'flex-direction': 'column', gap: '4px', 'border-left': '4px solid var(--green)' }}>
          <span style={{ 'font-size': '12px', color: 'var(--green)', 'font-weight': '600', 'text-transform': 'uppercase' }}>
            Despachadas
          </span>
          <span style={{ 'font-size': '24px', 'font-weight': '700', color: 'var(--green)' }}>
            {kpis().despachadas}
          </span>
          <span style={{ 'font-size': '11px', color: 'var(--ink-faint)' }}>Completadas y kárdex descontado</span>
        </div>

        <div class="card" style={{ padding: '16px', display: 'flex', 'flex-direction': 'column', gap: '4px' }}>
          <span style={{ 'font-size': '12px', color: 'var(--ink-soft)', 'font-weight': '600', 'text-transform': 'uppercase' }}>
            Monto Total Facturado
          </span>
          <span style={{ 'font-size': '24px', 'font-weight': '700', color: 'var(--forest)' }}>
            Bs. {kpis().montoTotal.toLocaleString('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </span>
          <span style={{ 'font-size': '11px', color: 'var(--ink-faint)' }}>En moneda nacional (BOB)</span>
        </div>
      </div>

      {/* ─── Barra de Filtros ─── */}
      <div class="card" style={{ 'margin-bottom': '20px', padding: '12px 16px' }}>
        <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '16px', 'align-items': 'center' }}>
          <div style={{ flex: '1', 'min-width': '220px' }}>
            <input
              type="text"
              placeholder="Buscar por código, cliente o proforma..."
              value={filtroBusqueda()}
              onInput={(e) => setFiltroBusqueda(e.currentTarget.value)}
              style={{ width: '100%', padding: '8px 12px' }}
            />
          </div>

          <div style={{ display: 'flex', gap: '8px', 'align-items': 'center' }}>
            <label style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', 'font-weight': '600' }}>Estado:</label>
            <select
              value={filtroEstado()}
              onChange={(e) => setFiltroEstado(e.currentTarget.value)}
              style={{ padding: '6px 12px' }}
            >
              <option value="">— Todos —</option>
              <option value="Confirmada">Confirmada (Pendiente Despacho)</option>
              <option value="Despachada">Despachada</option>
              <option value="Anulada">Anulada</option>
            </select>
          </div>

          <button
            class="btn btn-ghost"
            onClick={() => ventaStore.cargarVentas()}
            title="Recargar listado"
          >
            🔄 Actualizar
          </button>
        </div>
      </div>

      {/* ─── Mensaje de Error si existiera ─── */}
      <Show when={ventaStore.error()}>
        <div class="banner banner-error" style={{ 'margin-bottom': '16px' }}>
          {ventaStore.error()}
        </div>
      </Show>

      {/* ─── Tabla Principal ─── */}
      <div class="card" style={{ padding: '0', overflow: 'hidden' }}>
        <div style={{ 'overflow-x': 'auto' }}>
          <table class="data-table" style={{ width: '100%', 'border-collapse': 'collapse' }}>
            <thead>
              <tr style={{ 'border-bottom': '1px solid var(--border-color)', 'text-align': 'left' }}>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase' }}>Código</th>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase' }}>Fecha</th>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase' }}>Cliente</th>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase', 'text-align': 'right' }}>Total (BOB)</th>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase', 'text-align': 'center' }}>Estado</th>
                <th style={{ padding: '12px 16px', 'font-size': '12px', 'text-transform': 'uppercase', 'text-align': 'center' }}>Acciones</th>
              </tr>
            </thead>
            <tbody>
              <Show
                when={ventasFiltradas().length > 0}
                fallback={
                  <tr>
                    <td colspan="6" style={{ padding: '32px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
                      <Show when={ventaStore.loadingVentas()} fallback="No se encontraron órdenes de venta registradas.">
                        Cargando ventas...
                      </Show>
                    </td>
                  </tr>
                }
              >
                <For each={ventasFiltradas()}>
                  {(venta) => (
                    <tr style={{ 'border-bottom': '1px solid var(--border-soft)' }}>
                      <td style={{ padding: '12px 16px', 'font-family': 'monospace', 'font-weight': '700', color: 'var(--forest)' }}>
                        {venta.codigo}
                      </td>
                      <td style={{ padding: '12px 16px', 'font-size': '13px', color: 'var(--ink-soft)' }}>
                        {venta.fecha}
                      </td>
                      <td style={{ padding: '12px 16px', 'font-weight': '600', color: 'var(--ink)' }}>
                        {venta.clienteNombre}
                      </td>
                      <td style={{ padding: '12px 16px', 'text-align': 'right', 'font-weight': '700', 'font-family': 'monospace' }}>
                        Bs. {Number(venta.total).toLocaleString('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                      </td>
                      <td style={{ padding: '12px 16px', 'text-align': 'center' }}>
                        <span class={estadoBadgeClass(venta.estado)}>{venta.estado}</span>
                      </td>
                      <td style={{ padding: '12px 16px', 'text-align': 'center' }}>
                        <button
                          class="btn btn-ghost"
                          style={{ 'font-size': '12px', padding: '4px 10px' }}
                          onClick={() => verDetalle(venta)}
                        >
                          👁️ Ver Detalle
                        </button>
                      </td>
                    </tr>
                  )}
                </For>
              </Show>
            </tbody>
          </table>
        </div>
      </div>

      {/* ─── Modal de Detalle de Venta ─── */}
      <Show when={modalDetalleOpen() && ventaDetalle()}>
        <div class="modal-backdrop" onClick={() => setModalDetalleOpen(false)}>
          <div class="modal card" style={{ 'max-width': '700px', width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <div class="modal-header" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-bottom': '1px solid var(--border-color)', 'padding-bottom': '12px' }}>
              <div>
                <h3 style={{ margin: '0', 'font-size': '18px', color: 'var(--ink)' }}>
                  Orden de Venta: {ventaDetalle()?.codigo}
                </h3>
                <span class={estadoBadgeClass(ventaDetalle()?.estado || '')}>
                  {ventaDetalle()?.estado}
                </span>
              </div>
              <button class="btn btn-ghost" onClick={() => setModalDetalleOpen(false)}>✕</button>
            </div>

            <div class="modal-body" style={{ 'margin-top': '16px', display: 'flex', 'flex-direction': 'column', gap: '16px' }}>
              <div style={{ display: 'grid', 'grid-template-columns': 'repeat(2, 1fr)', gap: '12px', background: 'var(--panel-bg-soft)', padding: '12px', 'border-radius': '6px' }}>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Cliente</span>
                  <strong style={{ 'font-size': '14px' }}>{ventaDetalle()?.clienteNombre}</strong>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Fecha de Emisión</span>
                  <strong style={{ 'font-size': '14px' }}>{ventaDetalle()?.fecha}</strong>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>ID Proforma Origen</span>
                  <span style={{ 'font-size': '12px', 'font-family': 'monospace', color: 'var(--ink-soft)' }}>
                    {ventaDetalle()?.proformaOrigenId}
                  </span>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Total de la Venta</span>
                  <strong style={{ 'font-size': '16px', color: 'var(--forest)' }}>
                    Bs. {Number(ventaDetalle()?.total || 0).toLocaleString('es-BO', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </strong>
                </div>
              </div>

              <div>
                <h4 style={{ margin: '0 0 8px 0', 'font-size': '14px', color: 'var(--ink)' }}>
                  Productos Comprometidos
                </h4>
                <table style={{ width: '100%', 'border-collapse': 'collapse' }}>
                  <thead>
                    <tr style={{ 'border-bottom': '1px solid var(--border-color)', 'font-size': '12px', color: 'var(--ink-soft)' }}>
                      <th style={{ 'text-align': 'left', padding: '6px 0' }}>Producto</th>
                      <th style={{ 'text-align': 'right', padding: '6px 8px' }}>Cantidad</th>
                      <th style={{ 'text-align': 'right', padding: '6px 8px' }}>P. Unitario</th>
                      <th style={{ 'text-align': 'right', padding: '6px 0' }}>Subtotal</th>
                    </tr>
                  </thead>
                  <tbody>
                    <For each={ventaDetalle()?.lineas}>
                      {(linea) => (
                        <tr style={{ 'border-bottom': '1px solid var(--border-soft)', 'font-size': '13px' }}>
                          <td style={{ padding: '8px 0', 'font-weight': '600' }}>{linea.nombreProducto}</td>
                          <td style={{ 'text-align': 'right', padding: '8px', 'font-family': 'monospace' }}>
                            {linea.cantidad} {linea.unidad}
                          </td>
                          <td style={{ 'text-align': 'right', padding: '8px', 'font-family': 'monospace' }}>
                            Bs. {Number(linea.precioUnitario).toFixed(2)}
                          </td>
                          <td style={{ 'text-align': 'right', padding: '8px 0', 'font-weight': '700', 'font-family': 'monospace' }}>
                            Bs. {Number(linea.subtotal).toFixed(2)}
                          </td>
                        </tr>
                      )}
                    </For>
                  </tbody>
                </table>
              </div>

              <Show when={ventaDetalle()?.estado === 'Confirmada'}>
                <div class="banner banner-info" style={{ 'font-size': '12.5px' }}>
                  📦 La orden de despacho asociada ha sido generada automáticamente para Almacén con stock congelado.
                </div>
              </Show>
            </div>

            <div class="modal-footer" style={{ 'margin-top': '20px', display: 'flex', 'justify-content': 'flex-end' }}>
              <button class="btn btn-secondary" onClick={() => setModalDetalleOpen(false)}>
                Cerrar
              </button>
            </div>
          </div>
        </div>
      </Show>
    </section>
  );
};
