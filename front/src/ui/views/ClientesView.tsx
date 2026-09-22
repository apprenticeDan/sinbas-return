import { createSignal, onMount, createMemo, Show } from 'solid-js';
import { ApiClientGateway, ClienteDto } from '../../infrastructure/api/ApiClientGateway';
import { ClienteFormModal } from '../components/ClienteFormModal';
import { DataTable, Column } from '../components/DataTable';
import { authStore } from '../store/authStore';

export function ClientesView() {
  const [clientes, setClientes] = createSignal<ClienteDto[]>([]);
  const [loading, setLoading] = createSignal<boolean>(false);
  const [searchTerm, setSearchTerm] = createSignal<string>('');
  const [tipoFilter, setTipoFilter] = createSignal<string>('');
  const [createModalOpen, setCreateModalOpen] = createSignal<boolean>(false);
  const [notification, setNotification] = createSignal<{ type: 'success' | 'error'; message: string } | null>(null);

  const canManageClientes = createMemo(() => authStore.hasAnyRole(['Administrador', 'Comercial', 'Gerencia']));

  const loadClientes = async (term?: string) => {
    setLoading(true);
    try {
      const data = await ApiClientGateway.listarClientes(term);
      setClientes(data);
    } catch (err: any) {
      setNotification({ type: 'error', message: err.message || 'Error al cargar clientes' });
    } finally {
      setLoading(false);
    }
  };

  onMount(() => {
    loadClientes();
  });

  const handleSearchInput = (value: string) => {
    setSearchTerm(value);
    // Búsqueda multivariable reactiva
    loadClientes(value);
  };

  const filteredClientes = createMemo(() => {
    let list = clientes();
    if (tipoFilter()) {
      list = list.filter((c) => c.tipo === tipoFilter());
    }
    return list;
  });

  const columns: Column<ClienteDto>[] = [
    {
      header: 'Tipo',
      width: '110px',
      cell: (c) => (
        <span class={c.tipo === 'Juridica' ? 'pill pill-amber' : 'pill pill-green'}>
          {c.tipo === 'Juridica' ? '🏢 Jurídica' : '👤 Natural'}
        </span>
      ),
    },
    {
      header: 'Nombre / Razón Social',
      cell: (c) => (
        <div>
          <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>{c.nombreVisible}</div>
          <Show when={c.representante}>
            <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'margin-top': '2px' }}>
              Rep: {c.representante?.nombreCompleto} (CI: {c.representante?.ciFormateado})
            </div>
          </Show>
        </div>
      ),
    },
    {
      header: 'Identificación (NIT / CI)',
      cell: (c) => {
        if (c.tipo === 'Juridica') {
          return (
            <div>
              <span style={{ 'font-family': 'monospace', 'font-size': '12.5px', 'font-weight': '600', color: 'var(--ink)' }}>
                NIT: {c.nit || '-'}
              </span>
            </div>
          );
        }
        return (
          <div>
            <span style={{ 'font-family': 'monospace', 'font-size': '12.5px', 'font-weight': '600', color: 'var(--ink)' }}>
              CI: {c.persona?.ciFormateado || '-'}
            </span>
          </div>
        );
      },
    },
    {
      header: 'Contacto',
      cell: (c) => (
        <div>
          <Show when={c.telefono}>
            <div style={{ 'font-size': '12px', color: 'var(--ink)' }}>📞 {c.telefono}</div>
          </Show>
          <Show when={c.email}>
            <div style={{ 'font-size': '11.5px', color: 'var(--ink-soft)' }}>✉️ {c.email}</div>
          </Show>
          <Show when={!c.telefono && !c.email}>
            <span style={{ color: 'var(--ink-soft)', 'font-size': '12px' }}>-</span>
          </Show>
        </div>
      ),
    },
    {
      header: 'Dirección / Ciudad',
      cell: (c) => (
        <span style={{ 'font-size': '12px', color: 'var(--ink-soft)' }}>
          {c.direccion || '-'}
        </span>
      ),
    },
    {
      header: 'Estado',
      width: '90px',
      cell: (c) => (
        <span class={c.estado === 'Activo' ? 'pill pill-green' : 'pill pill-rust'}>
          ● {c.estado}
        </span>
      ),
    },
  ];

  return (
    <section class="panel">
      {/* Panel Header */}
      <div class="panel-head">
        <p class="panel-eyebrow">Gestión Comercial (F6)</p>
        <h1 class="panel-title">Catálogo de Clientes</h1>
        <p class="panel-desc">
          Registro y búsqueda multivariable de clientes (Personas Naturales y Jurídicas) para consignación de despachos y facturación.
        </p>
      </div>

      {/* Notifications */}
      <Show when={notification()}>
        <div
          class={notification()?.type === 'success' ? 'alert-success' : 'alert-error'}
          style={{ 'margin-bottom': '16px', display: 'flex', 'justify-content': 'space-between', 'align-items': 'center' }}
        >
          <span>{notification()?.message}</span>
          <button
            onClick={() => setNotification(null)}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', 'font-size': '14px' }}
          >
            ✕
          </button>
        </div>
      </Show>

      {/* Toolbar */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar" style={{ 'justify-content': 'space-between', 'align-items': 'flex-end' }}>
          <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '12px', flex: '1', 'align-items': 'flex-end' }}>
            <div class="field" style={{ 'min-width': '260px', flex: '1' }}>
              <label>Búsqueda Multivariable</label>
              <input
                type="text"
                placeholder="Buscar por Nombre, Razón Social, NIT o CI..."
                value={searchTerm()}
                onInput={(e) => handleSearchInput(e.currentTarget.value)}
              />
            </div>

            <div class="field">
              <label>Filtrar por Tipo</label>
              <select
                value={tipoFilter()}
                onChange={(e) => setTipoFilter(e.currentTarget.value)}
              >
                <option value="">Todos los Tipos</option>
                <option value="Natural">Persona Natural</option>
                <option value="Juridica">Persona Jurídica</option>
              </select>
            </div>
          </div>

          <Show when={canManageClientes()}>
            <button
              onClick={() => setCreateModalOpen(true)}
              class="btn btn-primary"
              style={{ height: '38px' }}
            >
              + Nuevo Cliente
            </button>
          </Show>
        </div>
      </div>

      {/* Data Table */}
      <DataTable
        columns={columns}
        data={filteredClientes()}
        loading={loading()}
        emptyMessage="No se encontraron clientes registrados con el criterio de búsqueda."
      />

      {/* Create Modal */}
      <ClienteFormModal
        open={createModalOpen()}
        onClose={() => setCreateModalOpen(false)}
        onSuccess={() => {
          setNotification({ type: 'success', message: 'Cliente registrado exitosamente.' });
          loadClientes(searchTerm());
        }}
      />
    </section>
  );
}
