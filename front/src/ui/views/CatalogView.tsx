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
          <div class="font-bold text-slate-100 flex items-center gap-2">
            <Show when={p.genero && p.epiteto} fallback={<span>{p.nombreVisible}</span>}>
              <span class="italic font-serif text-emerald-300 text-base">{p.nombreVisible}</span>
            </Show>
          </div>
          <Show when={p.nombresComunes && p.nombresComunes.length > 0}>
            <div class="flex flex-wrap gap-1 mt-1">
              <For each={p.nombresComunes}>
                {(nc) => (
                  <span class="px-2 py-0.5 text-[11px] font-medium rounded-md bg-slate-800 text-slate-300 border border-slate-700/60">
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
      cell: (p) => {
        const bgMap: Record<string, string> = {
          Semilla: 'bg-emerald-950/80 text-emerald-300 border-emerald-700/60',
          Plantin: 'bg-teal-950/80 text-teal-300 border-teal-700/60',
          Insumo: 'bg-indigo-950/80 text-indigo-300 border-indigo-700/60',
          Otro: 'bg-slate-800 text-slate-300 border-slate-700',
        };
        return (
          <span class={`inline-block px-2.5 py-1 text-xs font-semibold rounded-lg border ${bgMap[p.categoria] || bgMap.Otro}`}>
            {p.categoria}
          </span>
        );
      },
    },
    {
      header: 'Unidad / Trazabilidad',
      cell: (p) => (
        <div class="space-y-1">
          <div class="text-slate-200 font-medium">{p.unidadManejo}</div>
          <span
            class={`inline-block px-2 py-0.5 text-[10px] font-bold rounded ${
              p.trazabilidad === 'PorLote' ? 'bg-amber-950/60 text-amber-300 border border-amber-800/60' : 'bg-slate-800 text-slate-400'
            }`}
          >
            {p.trazabilidad === 'PorLote' ? 'Lote Obligatorio' : 'Simple'}
          </span>
        </div>
      ),
    },
    {
      header: 'Precio Oficial',
      cell: (p) => (
        <div>
          <Show
            when={p.precioOficial !== undefined && p.precioOficial !== null}
            fallback={
              <span class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-medium rounded-lg bg-amber-950/40 text-amber-400 border border-amber-800/40">
                <span>⏳</span> Sin precio
              </span>
            }
          >
            <span class="font-mono text-base font-bold text-emerald-400">
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
          return (
            <span class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-full bg-emerald-950/80 text-emerald-300 border border-emerald-700/60 shadow-sm">
              <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
              Activo para Venta
            </span>
          );
        } else if (p.estadoComercial === 'PendientePrecioBorrador') {
          return (
            <span class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-full bg-amber-950/80 text-amber-300 border border-amber-700/60 shadow-sm">
              <span class="w-1.5 h-1.5 rounded-full bg-amber-400"></span>
              Borrador Sin Precio
            </span>
          );
        }
        return (
          <span class="inline-flex items-center gap-1 px-2.5 py-1 text-xs font-semibold rounded-full bg-slate-800 text-slate-400 border border-slate-700">
            Inactivo
          </span>
        );
      },
    },
    {
      header: 'Acciones',
      cell: (p) => (
        <div class="flex items-center gap-2">
          <Show when={canManagePrices()}>
            <button
              onClick={() => catalogStore.openAssignPriceModal(p)}
              class="px-3 py-1.5 text-xs font-medium rounded-lg bg-emerald-900/60 hover:bg-emerald-800/80 text-emerald-200 border border-emerald-700/60 transition-all flex items-center gap-1 shadow-sm"
              title="Asignar o Modificar Precio Oficial (Gerencia)"
            >
              <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              {p.precioOficial ? 'Editar Precio' : 'Fijar Precio'}
            </button>
          </Show>

          <Show when={!canManagePrices() && p.estadoComercial === 'PendientePrecioBorrador'}>
            <span class="text-xs text-slate-500 italic">Esp. Gerencia</span>
          </Show>
        </div>
      ),
    },
  ];

  return (
    <div class="space-y-6 animate-fade-in p-2 sm:p-4">
      {/* Top Title & Stats Banner */}
      <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900/60 border border-slate-800/80 p-5 rounded-2xl backdrop-blur-md">
        <div>
          <h1 class="text-2xl font-bold text-transparent bg-clip-text bg-gradient-to-r from-emerald-400 via-teal-300 to-cyan-400">
            Catálogo Oficial de Productos y Precios
          </h1>
          <p class="text-xs sm:text-sm text-slate-400 mt-1">
            Gobernanza comercial, precios oficiales y estados de disponibilidad para venta.
          </p>
        </div>

        {/* Stats Cards */}
        <div class="flex items-center gap-3">
          <div class="px-4 py-2 bg-slate-800/80 border border-slate-700/60 rounded-xl text-center">
            <div class="text-xs text-slate-400 font-medium">Total</div>
            <div class="text-lg font-bold text-slate-100">{stats().total}</div>
          </div>
          <div class="px-4 py-2 bg-emerald-950/40 border border-emerald-800/40 rounded-xl text-center">
            <div class="text-xs text-emerald-400 font-medium">En Venta</div>
            <div class="text-lg font-bold text-emerald-300">{stats().activos}</div>
          </div>
          <div class="px-4 py-2 bg-amber-950/40 border border-amber-800/40 rounded-xl text-center">
            <div class="text-xs text-amber-400 font-medium">Borradores</div>
            <div class="text-lg font-bold text-amber-300">{stats().borradores}</div>
          </div>
        </div>
      </div>

      {/* Filter & Action Toolbar */}
      <div class="flex flex-col sm:flex-row items-stretch sm:items-center justify-between gap-3 bg-slate-900/40 border border-slate-800 p-4 rounded-xl">
        {/* Left Filters */}
        <div class="flex flex-wrap items-center gap-3 flex-1">
          {/* Search Input */}
          <div class="relative min-w-[220px] flex-1">
            <input
              type="text"
              placeholder="Buscar por especie o nombre..."
              value={catalogStore.searchTerm()}
              onInput={(e) => catalogStore.setSearchTerm(e.currentTarget.value)}
              class="w-full pl-9 pr-3 py-2 bg-slate-800 border border-slate-700/80 rounded-lg text-sm text-slate-100 focus:outline-none focus:border-emerald-500 placeholder-slate-500"
            />
            <svg class="w-4 h-4 text-slate-400 absolute left-3 top-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"></path>
            </svg>
          </div>

          {/* Category Filter */}
          <select
            value={catalogStore.categoryFilter()}
            onChange={(e) => catalogStore.setCategoryFilter(e.currentTarget.value)}
            class="px-3 py-2 bg-slate-800 border border-slate-700/80 rounded-lg text-xs sm:text-sm text-slate-200 focus:outline-none focus:border-emerald-500"
          >
            <option value="">Todas las Categorías</option>
            <option value="Semilla">Semilla</option>
            <option value="Plantin">Plantín</option>
            <option value="Insumo">Insumo</option>
            <option value="Otro">Otro</option>
          </select>

          {/* State Filter */}
          <select
            value={catalogStore.stateFilter()}
            onChange={(e) => catalogStore.setStateFilter(e.currentTarget.value)}
            class="px-3 py-2 bg-slate-800 border border-slate-700/80 rounded-lg text-xs sm:text-sm text-slate-200 focus:outline-none focus:border-emerald-500"
          >
            <option value="">Todos los Estados</option>
            <option value="ActivoParaVenta">Activo para Venta</option>
            <option value="PendientePrecioBorrador">Borrador Sin Precio</option>
          </select>
        </div>

        {/* Right Action Button */}
        <button
          onClick={() => catalogStore.setCreateModalOpen(true)}
          class="px-4 py-2 rounded-xl bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white text-sm font-semibold shadow-lg shadow-emerald-950/40 transition-all flex items-center justify-center gap-2"
        >
          <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
          </svg>
          + Registrar Producto
        </button>
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
    </div>
  );
}
