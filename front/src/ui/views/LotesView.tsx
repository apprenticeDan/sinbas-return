// Nota: la creación de lotes se gestiona exclusivamente desde
// IngresosView, que genera movimiento de entrada + actualización de
// stock + kardex coherentemente. Este módulo es consulta + gestión
// de estado (bloquear, liberar, archivar, ficha técnica).

import { onMount, Show, createMemo, createSignal } from 'solid-js';
import { loteStore } from '../store/loteStore';
import { authStore } from '../store/authStore';
import { LoteItem } from '../../domain/models/Lote';
import { DataTable, Column } from '../components/DataTable';
import { LoteBlockModal } from '../components/LoteBlockModal';
import { LoteFichaTecnicaModal } from '../components/LoteFichaTecnicaModal';
import { formatDisplayId } from '../utils/formatters';
import { downloadBlob } from '../../infrastructure/api/HttpClient';
import { FiltersBar } from '../primitives/FiltersBar';
import { createFilteredSorted, SortConfig } from '../hooks/createFilteredSorted';

export function LotesView() {
  const [fichaModalOpen, setFichaModalOpen] = createSignal<boolean>(false);
  const [selectedFichaLoteId, setSelectedFichaLoteId] = createSignal<string | null>(null);

  const [search, setSearch] = createSignal<string>('');
  const [estadoFilter, setEstadoFilter] = createSignal<string>('');
  const [sortField, setSortField] = createSignal<string>('fecha');

  onMount(() => {
    loteStore.loadLotes();
  });

  const canBlockLotes = createMemo(() => authStore.hasAnyRole(['Laboratorio', 'Gerencia']));

  const stats = createMemo(() => {
    const list = loteStore.lotes();
    const total = list.length;
    const activos = list.filter((l) => l.estado === 'Activo').length;
    const cuarentena = list.filter((l) => l.estado === 'EnCuarentena').length;
    const rechazados = list.filter((l) => l.estado === 'Rechazado').length;
    const bloqueados = list.filter((l) => l.estado === 'Bloqueado').length;
    const agotados = list.filter((l) => l.estado === 'Agotado').length;
    return { total, activos, cuarentena, rechazados, bloqueados, agotados };
  });

  const sortConfig = createMemo<SortConfig<LoteItem> | null>(() => {
    const f = sortField();
    switch (f) {
      case 'fecha':
        return { key: (l) => l.fechaIngreso, direction: 'desc' };
      case 'codigo':
        return { key: (l) => l.codigo, direction: 'asc' };
      case 'producto':
        return { key: (l) => l.nombreProducto, direction: 'asc' };
      case 'estado':
        return { key: (l) => l.estado, direction: 'asc' };
      default:
        return null;
    }
  });

  const filteredLotes = createFilteredSorted(loteStore.lotes, {
    search: {
      value: search,
      keys: [
        (l) => l.codigo,
        (l) => l.nombreProducto,
        (l) => l.procedencia || '',
        (l) => l.ubicacion || '',
      ],
    },
    filters: [
      {
        value: estadoFilter,
        match: (l, val) => l.estado === val,
      },
    ],
    sort: sortConfig,
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
        } else if (l.estado === 'EnCuarentena') {
          return (
            <span
              class="pill pill-amber"
              style={{ 'border-color': '#d97706', color: '#b45309', background: 'rgba(245, 158, 11, 0.12)' }}
              title={l.observaciones || 'Lote en cuarentena técnica'}
            >
              ⚠️ En Cuarentena
            </span>
          );
        } else if (l.estado === 'Rechazado') {
          return (
            <span class="pill pill-rust" title={l.observaciones || 'Lote rechazado definitivamente'}>
              ⛔ Rechazado
            </span>
          );
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
          <button
            onClick={() => {
              setSelectedFichaLoteId(l.id);
              setFichaModalOpen(true);
            }}
            class="btn btn-ghost"
            style={{ padding: '4px 8px', 'font-size': '11.5px' }}
            title="Consultar ficha técnica consolidada e historial"
          >
            📄 Ficha
          </button>

          <Show when={canBlockLotes() && l.estado === 'Activo'}>
            <button
              onClick={() => {
                loteStore.setSelectedLote(l);
                loteStore.setBlockModalOpen(true);
              }}
              class="btn btn-ghost"
              style={{ padding: '4px 8px', 'font-size': '11.5px', color: 'var(--rust)' }}
              title="Bloqueo preventivo de lote (Laboratorio / Gerencia)"
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
      <div style={{ 'margin-bottom': '20px', display: 'flex', 'flex-direction': 'column', gap: '12px' }}>
        <div style={{ display: 'flex', gap: '12px', 'align-items': 'center', 'justify-content': 'space-between', 'flex-wrap': 'wrap' }}>
          <div style={{ flex: '1', 'min-width': '300px' }}>
            <FiltersBar
              search={{
                value: search(),
                onChange: setSearch,
                placeholder: 'Buscar por código, producto o procedencia...',
              }}
              filters={[
                {
                  label: 'Estado',
                  value: estadoFilter(),
                  onChange: setEstadoFilter,
                  options: [
                    { value: '', label: 'Todos' },
                    { value: 'Activo', label: 'Activo' },
                    { value: 'EnCuarentena', label: 'En Cuarentena' },
                    { value: 'Agotado', label: 'Agotado' },
                    { value: 'Bloqueado', label: 'Bloqueado' },
                    { value: 'Rechazado', label: 'Rechazado' },
                  ],
                },
              ]}
              sort={{
                value: sortField(),
                onChange: setSortField,
                options: [
                  { value: 'fecha', label: 'Fecha ingreso' },
                  { value: 'codigo', label: 'Código' },
                  { value: 'producto', label: 'Producto' },
                  { value: 'estado', label: 'Estado' },
                ],
              }}
              onRefresh={() => {
                loteStore.loadLotes();
              }}
              onClear={() => {
                setSearch('');
                setEstadoFilter('');
                setSortField('fecha');
              }}
            />
          </div>
          <button
            class="btn btn-ghost"
            style={{ 'font-size': '12.5px', padding: '8px 14px', 'white-space': 'nowrap' }}
            onClick={() => downloadBlob(
              '/reportes/lotes.csv',
              `lotes-${new Date().toISOString().split('T')[0]}.csv`
            ).catch(e => alert(e.message))}
            title="Exportar lotes a CSV"
          >
            📥 Exportar CSV
          </button>
        </div>

        {/* Stats Sub-bar */}
        <div
          class="card"
          style={{
            padding: '10px 16px',
            background: 'var(--surface-alt)',
            display: 'flex',
            'flex-wrap': 'wrap',
            gap: '20px',
            'font-size': '12px',
          }}
        >
          <div>Total Lotes: <strong>{stats().total}</strong></div>
          <div style={{ color: 'var(--green-deep)' }}>Disponibles: <strong>{stats().activos}</strong></div>
          <div style={{ color: '#b45309' }}>En Cuarentena: <strong>{stats().cuarentena}</strong></div>
          <div style={{ color: 'var(--rust)' }}>Rechazados: <strong>{stats().rechazados}</strong></div>
          <div style={{ color: 'var(--rust)' }}>Bloqueados: <strong>{stats().bloqueados}</strong></div>
          <div style={{ color: 'var(--amber)' }}>Agotados: <strong>{stats().agotados}</strong></div>
        </div>
      </div>

      {/* Data Table */}
      <DataTable
        columns={columns}
        data={filteredLotes()}
        loading={loteStore.loading()}
        emptyMessage="No se encontraron lotes registrados con los filtros seleccionados."
      />

      {/* Modals */}
      <LoteBlockModal />
      <LoteFichaTecnicaModal
        open={fichaModalOpen()}
        loteId={selectedFichaLoteId()}
        onClose={() => {
          setFichaModalOpen(false);
          setSelectedFichaLoteId(null);
        }}
      />
    </section>
  );
}

