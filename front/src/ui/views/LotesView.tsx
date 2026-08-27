import { onMount, Show, For, createMemo } from 'solid-js';
import { loteStore } from '../store/loteStore';
import { authStore } from '../store/authStore';
import { LoteItem } from '../../domain/models/Lote';
import { DataTable, Column } from '../components/DataTable';
import { LoteFormModal } from '../components/LoteFormModal';
import { LoteBlockModal } from '../components/LoteBlockModal';
import { formatDisplayId } from '../utils/formatters';

export function LotesView() {
  onMount(() => {
    loteStore.loadLotes();
  });

  const canManageLotes = createMemo(() => authStore.hasAnyRole(['Administrador', 'Almacen', 'Gerencia']));
  const canBlockLotes = createMemo(() => authStore.hasAnyRole(['Administrador', 'Gerencia', 'Laboratorio', 'Almacen']));

  const stats = createMemo(() => {
    const list = loteStore.lotes();
    const total = list.length;
    const activos = list.filter((l) => l.estado === 'Activo').length;
    const bloqueados = list.filter((l) => l.estado === 'Bloqueado').length;
    const agotados = list.filter((l) => l.estado === 'Agotado').length;
    return { total, activos, bloqueados, agotados };
  });

  const columns: Column<LoteItem>[] = [
    {
      header: 'ID',
      width: '90px',
      cell: (l, idx) => (
        <span
          title={`UUID v7: ${l.id}`}
          style={{
            'font-family': 'monospace',
            'font-size': '12px',
            color: 'var(--ink-soft)',
            cursor: 'help',
            'border-bottom': '1px dotted var(--border)',
          }}
        >
          {formatDisplayId('LOT', idx, l.id)}
        </span>
      ),
    },
    {
      header: 'Código de Lote',
      cell: (l) => (
        <div>
          <span style={{ 'font-family': 'monospace', 'font-size': '13.5px', 'font-weight': '700', color: 'var(--green-deep)' }}>
            {l.codigo}
          </span>
          <Show when={l.procedencia}>
            <div style={{ 'font-size': '11.5px', color: 'var(--ink-soft)', 'margin-top': '2px' }}>
              📍 {l.procedencia}
            </div>
          </Show>
        </div>
      ),
    },
    {
      header: 'Producto / Especie',
      cell: (l) => (
        <div>
          <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>{l.nombreProducto}</div>
          <Show when={l.ubicacion}>
            <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'margin-top': '2px' }}>
              📦 Ubicación: {l.ubicacion}
            </div>
          </Show>
        </div>
      ),
    },
    {
      header: 'Existencia Actual',
      cell: (l) => {
        const pct = l.cantidadInicial > 0 ? Math.round((l.cantidadActual / l.cantidadInicial) * 100) : 0;
        return (
          <div>
            <div style={{ 'font-family': 'monospace', 'font-size': '14px', 'font-weight': '700', color: 'var(--ink)' }}>
              {l.cantidadActual} <span style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>/ {l.cantidadInicial} {l.unidad}</span>
            </div>
            <div style={{ width: '100px', height: '4px', background: 'var(--border-soft)', 'border-radius': '2px', 'margin-top': '4px', overflow: 'hidden' }}>
              <div
                style={{
                  width: `${Math.min(100, Math.max(0, pct))}%`,
                  height: '100%',
                  background: pct > 20 ? 'var(--brand-600)' : 'var(--rust)',
                }}
              />
            </div>
          </div>
        );
      },
    },
    {
      header: 'Fecha Ingreso',
      cell: (l) => (
        <span style={{ 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
          {l.fechaIngreso}
        </span>
      ),
    },
    {
      header: 'Estado',
      cell: (l) => {
        if (l.estado === 'Activo') {
          return <span class="pill pill-green">● Activo</span>;
        } else if (l.estado === 'Bloqueado') {
          return (
            <span class="pill pill-rust" title={l.observaciones || 'Lote bajo observación'}>
              ● Bloqueado
            </span>
          );
        } else if (l.estado === 'Agotado') {
          return <span class="pill pill-amber">● Agotado</span>;
        }
        return <span class="pill">● {l.estado}</span>;
      },
    },
    {
      header: 'Acciones',
      align: 'right',
      cell: (l) => (
        <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '6px' }}>
          <Show when={canBlockLotes() && l.estado === 'Activo'}>
            <button
              onClick={() => {
                loteStore.setSelectedLote(l);
                loteStore.setBlockModalOpen(true);
              }}
              class="btn btn-ghost"
              style={{ padding: '4px 8px', 'font-size': '11.5px', color: 'var(--rust)' }}
              title="Reportar alerta o bloquear lote (Almacén / Lab / Gerencia)"
            >
              Bloquear
            </button>
          </Show>
        </div>
      ),
    },
  ];

  return (
    <section class="panel">
      {/* Panel Header */}
      <div class="panel-head">
        <p class="panel-eyebrow">Gestión de Almacén</p>
        <h1 class="panel-title">Lotes de Semilla e Inventario</h1>
        <p class="panel-desc">
          Trazabilidad de lotes recibidos, origen/procedencia, control de existencias y estados de disponibilidad.
        </p>
      </div>

      {/* Toolbar & Filters */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar" style={{ 'justify-content': 'space-between' }}>
          <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '12px', flex: '1', 'align-items': 'flex-end' }}>
            <div class="field" style={{ 'min-width': '220px', flex: '1' }}>
              <label>Buscar Lote o Producto</label>
              <input
                type="text"
                placeholder="Código (ej. SWIETMAC), especie o procedencia..."
                value={loteStore.searchTerm()}
                onInput={(e) => loteStore.setSearchTerm(e.currentTarget.value)}
              />
            </div>

            <div class="field">
              <label>Estado de Lote</label>
              <select
                value={loteStore.stateFilter()}
                onChange={(e) => loteStore.setStateFilter(e.currentTarget.value)}
              >
                <option value="">Todos los Estados</option>
                <option value="Activo">Activos</option>
                <option value="Agotado">Agotados</option>
                <option value="Bloqueado">Bloqueados</option>
              </select>
            </div>
          </div>

          <Show when={canManageLotes()}>
            <button
              onClick={() => loteStore.setCreateModalOpen(true)}
              class="btn btn-primary"
              style={{ 'align-self': 'flex-end' }}
            >
              <svg style={{ width: '16px', height: '16px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
              </svg>
              Ingreso de Lote
            </button>
          </Show>
        </div>

        {/* Stats Sub-bar */}
        <div style={{ padding: '12px 20px', background: 'var(--surface-alt)', 'border-top': '1px solid var(--border-soft)', display: 'flex', gap: '20px', 'font-size': '12px' }}>
          <div>Total Lotes: <strong>{stats().total}</strong></div>
          <div style={{ color: 'var(--green-deep)' }}>Disponibles: <strong>{stats().activos}</strong></div>
          <div style={{ color: 'var(--rust)' }}>Bloqueados / Alertas: <strong>{stats().bloqueados}</strong></div>
          <div style={{ color: 'var(--amber)' }}>Agotados: <strong>{stats().agotados}</strong></div>
        </div>
      </div>

      {/* Data Table */}
      <DataTable
        columns={columns}
        data={loteStore.filteredLotes()}
        loading={loteStore.loading()}
        emptyMessage="No se encontraron lotes registrados con los filtros seleccionados."
      />

      {/* Modals */}
      <LoteFormModal />
      <LoteBlockModal />
    </section>
  );
}
