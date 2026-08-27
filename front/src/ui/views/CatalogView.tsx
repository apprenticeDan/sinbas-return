import { onMount, Show, For, createMemo } from 'solid-js';
import { catalogStore } from '../store/catalogStore';
import { authStore } from '../store/authStore';
import { Product } from '../../domain/models/Product';
import { DataTable, Column } from '../components/DataTable';
import { ProductFormModal } from '../components/ProductFormModal';
import { AssignPriceModal } from '../components/AssignPriceModal';

export function CatalogView() {
  onMount(() => {
    catalogStore.loadCatalog();
  });

  const canManagePrices = createMemo(() => authStore.hasAnyRole(['Administrador', 'Gerencia']));

  const stats = createMemo(() => {
    const list = catalogStore.products();
    const total = list.length;
    const activos = list.filter((p) => p.estadoComercial === 'ActivoParaVenta').length;
    const borradores = list.filter((p) => p.estadoComercial === 'PendientePrecioBorrador').length;
    return { total, activos, borradores };
  });

  const columns: Column<Product>[] = [
    {
      header: 'Producto / Nombre Científico',
      cell: (p) => (
        <div>
          <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>
            <Show when={p.genero && p.epiteto} fallback={<span>{p.nombreVisible}</span>}>
              <span style={{ 'font-style': 'italic', 'font-family': 'var(--font-display)', color: 'var(--green-deep)', 'font-size': '15px' }}>
                {p.nombreVisible}
              </span>
            </Show>
          </div>
          <Show when={p.nombresComunes && p.nombresComunes.length > 0}>
            <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '4px', 'margin-top': '4px' }}>
              <For each={p.nombresComunes}>
                {(nc) => (
                  <span class="pill pill-green" style={{ 'font-size': '10.5px' }}>
                    {nc}
                  </span>
                )}
              </For>
            </div>
          </Show>
        </div>
      ),
    },
    {
      header: 'Categoría',
      cell: (p) => (
        <span class="pill pill-green" style={{ 'font-weight': '600' }}>
          {p.categoria}
        </span>
      ),
    },
    {
      header: 'Unidad / Trazabilidad',
      cell: (p) => (
        <div style={{ display: 'flex', 'flex-direction': 'column', gap: '2px' }}>
          <div style={{ 'font-weight': '500', color: 'var(--ink)' }}>{p.unidadManejo}</div>
          <div>
            <span
              class={p.trazabilidad === 'PorLote' ? 'pill pill-amber' : 'pill'}
              style={{ 'font-size': '10px' }}
            >
              {p.trazabilidad === 'PorLote' ? 'Lote Obligatorio' : 'Simple'}
            </span>
          </div>
        </div>
      ),
    },
    {
      header: 'Precio Oficial',
      cell: (p) => (
        <div>
          <Show
            when={p.precioOficial !== undefined && p.precioOficial !== null}
            fallback={<span class="pill pill-amber">⏳ Sin precio</span>}
          >
            <span style={{ 'font-family': 'monospace', 'font-size': '15px', 'font-weight': '700', color: 'var(--green-deep)' }}>
              {p.moneda || 'BOB'} {p.precioOficial?.toFixed(2)}
            </span>
          </Show>
        </div>
      ),
    },
    {
      header: 'Estado Comercial',
      cell: (p) => {
        if (p.estadoComercial === 'ActivoParaVenta') {
          return <span class="pill pill-green">● Activo para Venta</span>;
        } else if (p.estadoComercial === 'PendientePrecioBorrador') {
          return <span class="pill pill-amber">● Borrador Sin Precio</span>;
        }
        return <span class="pill pill-rust">● Inactivo</span>;
      },
    },
    {
      header: 'Acciones',
      align: 'right',
      cell: (p) => (
        <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '6px' }}>
          <Show when={canManagePrices()}>
            <button
              onClick={() => catalogStore.openAssignPriceModal(p)}
              class="btn btn-primary"
              style={{ padding: '5px 10px', 'font-size': '12px' }}
              title="Asignar o Modificar Precio Oficial (Gerencia)"
            >
              <svg style={{ width: '14px', height: '14px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              {p.precioOficial ? 'Editar Precio' : 'Fijar Precio'}
            </button>
          </Show>

          <Show when={!canManagePrices() && p.estadoComercial === 'PendientePrecioBorrador'}>
            <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'font-style': 'italic' }}>Esp. Gerencia</span>
          </Show>
        </div>
      ),
    },
  ];

  return (
    <section class="panel">
      {/* Panel Header */}
      <div class="panel-head">
        <p class="panel-eyebrow">Catálogo Oficial</p>
        <h1 class="panel-title">Catálogo de Productos y Precios</h1>
        <p class="panel-desc">
          Gobernanza comercial, precios oficiales y estados de disponibilidad para venta del vivero.
        </p>
      </div>

      {/* Toolbar & Filters Container */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar" style={{ 'justify-content': 'space-between' }}>
          {/* Filters */}
          <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '12px', flex: '1', 'align-items': 'flex-end' }}>
            <div class="field" style={{ 'min-width': '200px', flex: '1' }}>
              <label>Buscar Producto</label>
              <input
                type="text"
                placeholder="Especie o nombre común..."
                value={catalogStore.searchTerm()}
                onInput={(e) => catalogStore.setSearchTerm(e.currentTarget.value)}
              />
            </div>

            <div class="field">
              <label>Categoría</label>
              <select
                value={catalogStore.categoryFilter()}
                onChange={(e) => catalogStore.setCategoryFilter(e.currentTarget.value)}
              >
                <option value="">Todas las Categorías</option>
                <option value="Semilla">Semilla</option>
                <option value="Plantin">Plantín</option>
                <option value="Insumo">Insumo</option>
                <option value="Otro">Otro</option>
              </select>
            </div>

            <div class="field">
              <label>Estado Comercial</label>
              <select
                value={catalogStore.stateFilter()}
                onChange={(e) => catalogStore.setStateFilter(e.currentTarget.value)}
              >
                <option value="">Todos los Estados</option>
                <option value="ActivoParaVenta">Activo para Venta</option>
                <option value="PendientePrecioBorrador">Borrador Sin Precio</option>
              </select>
            </div>
          </div>

          {/* Action Button */}
          <button
            onClick={() => catalogStore.setCreateModalOpen(true)}
            class="btn btn-primary"
            style={{ 'align-self': 'flex-end' }}
          >
            <svg style={{ width: '16px', height: '16px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
            </svg>
            Nuevo Producto
          </button>
        </div>

        {/* Stats Sub-bar */}
        <div style={{ padding: '12px 20px', background: 'var(--surface-alt)', 'border-top': '1px solid var(--border-soft)', display: 'flex', gap: '20px', 'font-size': '12px' }}>
          <div>Total: <strong>{stats().total}</strong></div>
          <div style={{ color: 'var(--green-deep)' }}>En Venta: <strong>{stats().activos}</strong></div>
          <div style={{ color: 'var(--amber)' }}>Borradores: <strong>{stats().borradores}</strong></div>
        </div>
      </div>

      {/* Reusable Data Table Component */}
      <DataTable
        columns={columns}
        data={catalogStore.filteredProducts()}
        loading={catalogStore.loading()}
        emptyMessage="No se encontraron productos en el catálogo con los filtros seleccionados."
      />

      {/* Modals */}
      <ProductFormModal />
      <AssignPriceModal />
    </section>
  );
}
