/**
 * Vista de Registro de Egresos de Almacén.
 *
 * Basado en wireframe: docs/borradores/wireframes/ui_com_egresos.png
 * Feature: F8 / F9 (MF-08-03 / MF-09-01) — Registro de Salidas, Venta, Merma y Uso Interno con FIFO
 *
 * Conectado a backend real vía API REST con persistencia y resolución FIFO de lotes.
 * El selector de productos se construye dinámicamente desde el catálogo oficial (F1).
 *
 * EXTENSIBILITY:
 * - F6 (Clientes): Cabecera preparada para filtro multifactorial (nombre, apellido, NIT, teléfono, email).
 * - F9 (Uso Interno): Soporte para departamento y solicitante (cliente interno).
 */

import { Component, createSignal, For, Show, createMemo, onMount } from 'solid-js';
import { almacenStore } from '../store/almacenStore';
import {
  CATEGORIAS,
  TIPOS_EGRESO,
  CONSIGNATARIOS_MOCK,
  type CategoriaAlmacen,
  type TipoEgreso,
} from '../../domain/models/Almacen';
import type { Product } from '../../domain/models/Product';
import { DataTable, type Column } from '../components/DataTable';

export const EgresosView: Component = () => {
  // ─── Form state (inline header) ──────────────────────────────
  const [formFecha, setFormFecha] = createSignal(new Date().toISOString().split('T')[0]);
  const [formCategoria, setFormCategoria] = createSignal<CategoriaAlmacen | ''>('');
  const [formProductoId, setFormProductoId] = createSignal('');
  const [formTipo, setFormTipo] = createSignal<TipoEgreso | ''>('');
  const [formConsignatario, setFormConsignatario] = createSignal('');
  const [formCantidad, setFormCantidad] = createSignal<string>('');
  const [formUnidad, setFormUnidad] = createSignal<string>('Kilogramo');
  const [formCostoAdic, setFormCostoAdic] = createSignal<string>('');
  const [formError, setFormError] = createSignal<string | null>(null);

  // Cargar catálogo, clientes y movimientos de egreso reales al montar
  onMount(async () => {
    await Promise.all([
      almacenStore.asegurarCatalogoCargado(),
      almacenStore.asegurarClientesCargados(),
    ]);
    almacenStore.cargarEgresos();
  });

  // Productos del catálogo filtrados por categoría seleccionada
  const productosDisponibles = createMemo(() => {
    const cat = formCategoria();
    const productos = almacenStore.productosCache();
    if (!cat) return productos;
    return productos.filter((p) => p.categoria === cat);
  });

  // Producto seleccionado actualmente
  const productoSeleccionado = createMemo(() => {
    const id = formProductoId();
    if (!id) return null;
    return almacenStore.productosCache().find((p) => p.id === id) || null;
  });

  // Opciones de unidades compatibles según la dimensión física del producto
  const unidadesCompatibles = createMemo(() => {
    const prod = productoSeleccionado();
    if (!prod) return [{ value: 'Kilogramo', label: 'kg' }, { value: 'Gramo', label: 'g' }];
    const u = (prod.unidadManejo || '').toLowerCase();
    if (u.includes('kilo') || u.includes('gram')) {
      return [
        { value: 'Kilogramo', label: 'kg' },
        { value: 'Gramo', label: 'g' },
      ];
    }
    if (u.includes('lit') || u.includes('mili')) {
      return [
        { value: 'Litro', label: 'l' },
        { value: 'Mililitro', label: 'ml' },
      ];
    }
    return [{ value: 'Unidad', label: 'u' }];
  });

  // Clientes reales registrados + valores predeterminados para departamentos internos
  const opcionesConsignatario = createMemo(() => {
    const clientes = almacenStore.clientesCache();
    const nombresClientes = clientes.map((c) =>
      c.nit ? `${c.nombreVisible} (NIT: ${c.nit})` : c.nombreVisible
    );
    return Array.from(new Set([...nombresClientes, ...CONSIGNATARIOS_MOCK]));
  });

  const canSubmit = createMemo(() => {
    const cant = Number(formCantidad());
    return (
      formProductoId() !== '' &&
      formTipo() !== '' &&
      !isNaN(cant) &&
      cant > 0 &&
      !almacenStore.loadingEgresos()
    );
  });

  async function handleRegistrar() {
    const cant = Number(formCantidad());
    if (isNaN(cant) || cant <= 0) {
      setFormError('La cantidad a egresar debe ser un número mayor a cero.');
      return;
    }
    if (!canSubmit()) return;
    setFormError(null);

    const prod = productoSeleccionado();
    if (!prod) {
      setFormError('Debe seleccionar un producto del catálogo.');
      return;
    }

    try {
      await almacenStore.registrarEgreso({
        productoId: prod.id,
        fecha: formFecha(),
        categoria: prod.categoria as CategoriaAlmacen,
        descripcion: prod.nombreVisible,
        tipo: formTipo() as TipoEgreso,
        cantidad: cant,
        unidad: formUnidad() || prod.unidadManejo || 'Kilogramo',
        consignatario: formConsignatario(),
        costoAdicional: formCostoAdic() ? Number(formCostoAdic()) : undefined,
      });
      // Reset parcial
      setFormProductoId('');
      setFormTipo('');
      setFormConsignatario('');
      setFormCantidad('');
      setFormCostoAdic('');
    } catch (err: any) {
      setFormError(err.message || 'Error al registrar el egreso.');
    }
  }

  // ─── Pill styles para tipo de egreso ──────────────────────────
  function tipoPillClass(tipo: string): string {
    switch (tipo) {
      case 'Venta': return 'pill pill-green';
      case 'Merma': return 'pill pill-rust';
      case 'UsoLabor': return 'pill pill-amber';
      case 'UsoVivero': return 'pill pill-earth';
      case 'Intercambio': return 'pill';
      default: return 'pill';
    }
  }

  function tipoLabel(tipo: string): string {
    const found = TIPOS_EGRESO.find((t) => t.value === tipo);
    return found ? found.label : tipo;
  }

  // ─── Estadísticas ─────────────────────────────────────────────
  const stats = createMemo(() => {
    const list = almacenStore.filteredEgresos();
    const total = list.length;
    const ventas = list.filter((e) => e.tipo === 'Venta').length;
    const mermas = list.filter((e) => e.tipo === 'Merma').length;
    return { total, ventas, mermas };
  });

  // ─── Table columns ───────────────────────────────────────────
  const columns: Column<any>[] = [
    {
      header: 'Fecha',
      width: '110px',
      cell: (item: any) => (
        <span style={{ 'font-family': 'monospace', 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
          {item.fecha}
        </span>
      ),
    },
    {
      header: 'Categoría',
      width: '100px',
      cell: (item: any) => {
        const cat = CATEGORIAS.find((c) => c.value === item.categoria);
        return (
          <span style={{ 'font-weight': '600', 'font-size': '12.5px', color: 'var(--earth)' }}>
            {cat ? cat.abbr : item.categoria}
          </span>
        );
      },
    },
    {
      header: 'Descripción',
      cell: (item: any) => (
        <span style={{ 'font-weight': '600', color: 'var(--ink)' }}>
          {item.descripcion}
        </span>
      ),
    },
    {
      header: 'Tipo',
      width: '120px',
      cell: (item: any) => (
        <span class={tipoPillClass(item.tipo)}>
          {tipoLabel(item.tipo)}
        </span>
      ),
    },
    {
      header: 'Cantidad',
      width: '120px',
      cell: (item: any) => (
        <span style={{
          'font-family': 'monospace',
          'font-size': '14px',
          'font-weight': '700',
          color: item.cantidad != null ? 'var(--ink)' : 'var(--ink-faint)',
        }}>
          {item.cantidad != null ? `${item.cantidad} ${item.unidad || ''}` : '—'}
        </span>
      ),
    },
    {
      header: 'Consignatario',
      width: '140px',
      cell: (item: any) => (
        <span style={{ 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
          {item.consignatario || '—'}
        </span>
      ),
    },
    {
      header: 'Obs.',
      width: '90px',
      cell: (item: any) => (
        <span style={{ 'font-size': '12px', color: 'var(--ink-faint)' }}>
          {item.observaciones || '—'}
        </span>
      ),
    },
  ];

  return (
    <section class="panel">
      {/* ─── Header ──────────────────────────────────────────── */}
      <div class="panel-head">
        <p class="panel-eyebrow">Gestión de Almacén & Comercial</p>
        <h1 class="panel-title">Registro de Egresos</h1>
        <p class="panel-desc">
          Registro de salidas de productos y materiales por ventas, mermas, uso en laboratorio, vivero o trueque.
        </p>
      </div>

      {/* ─── Formulario Inline (cabecera) ────────────────────── */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar almacen-form-toolbar">
          <div class="field" style={{ 'min-width': '120px' }}>
            <label>Fecha</label>
            <input
              type="date"
              value={formFecha()}
              onInput={(e) => setFormFecha(e.currentTarget.value)}
            />
          </div>

          {/* Botón Solicitudes Egreso según Wireframe */}
          <div class="field" style={{ 'width': 'auto' }}>
            <label>&nbsp;</label>
            <button
              class="btn btn-ghost"
              style={{
                'font-size': '12px',
                padding: '8px 12px',
                'white-space': 'nowrap',
                opacity: '0.85',
                cursor: 'not-allowed',
              }}
              disabled
              title="Las operaciones de almacén generalmente se disparan en atención comercial. Vista de solicitudes en desarrollo (Próximamente)."
            >
              📋 Solicitudes Egreso
              <span style={{
                'font-size': '10px',
                background: 'var(--amber-tint)',
                color: 'var(--amber)',
                padding: '2px 5px',
                'border-radius': '4px',
                'margin-left': '4px'
              }}>Próximamente</span>
            </button>
          </div>

          <div class="field" style={{ 'min-width': '120px' }}>
            <label>Categoría</label>
            <select
              value={formCategoria()}
              onChange={(e) => {
                setFormCategoria(e.currentTarget.value as CategoriaAlmacen | '');
                setFormProductoId('');
              }}
            >
              <option value="">— Todas —</option>
              <For each={CATEGORIAS}>
                {(c) => <option value={c.value}>{c.label}</option>}
              </For>
            </select>
          </div>

          <div class="field" style={{ 'min-width': '200px', flex: '1' }}>
            <label>Producto (Catálogo)</label>
            <select
              value={formProductoId()}
              onChange={(e) => {
                const id = e.currentTarget.value;
                setFormProductoId(id);
                const prod = almacenStore.productosCache().find((p) => p.id === id);
                if (prod && prod.unidadManejo) {
                  setFormUnidad(prod.unidadManejo);
                }
              }}
            >
              <option value="">— Seleccionar producto —</option>
              <For each={productosDisponibles()}>
                {(p: Product) => (
                  <option value={p.id}>
                    {p.nombreVisible} ({p.unidadManejo})
                  </option>
                )}
              </For>
            </select>
          </div>

          <div class="field" style={{ 'min-width': '120px' }}>
            <label>Tipo</label>
            <select
              value={formTipo()}
              onChange={(e) => setFormTipo(e.currentTarget.value as TipoEgreso | '')}
            >
              <option value="">— Tipo —</option>
              <For each={TIPOS_EGRESO}>
                {(t) => <option value={t.value}>{t.label}</option>}
              </For>
            </select>
          </div>

          {/* EXTENSIBILITY (F6 / F9): Selector multivariable (nombre/apellido/nit/tel/email) para clientes y depto/solicitante para uso interno */}
          <div class="field" style={{ 'min-width': '140px', flex: '1' }}>
            <label>Consignatario / Destino</label>
            <input
              type="text"
              list="consignatarios-list"
              placeholder="Buscar cliente o escribir..."
              value={formConsignatario()}
              onInput={(e) => setFormConsignatario(e.currentTarget.value)}
            />
            <datalist id="consignatarios-list">
              <For each={opcionesConsignatario()}>
                {(c) => <option value={c} />}
              </For>
            </datalist>
          </div>

          <div class="field" style={{ 'min-width': '75px', 'max-width': '95px' }}>
            <label>Cantidad</label>
            <input
              type="number"
              min="0"
              step="any"
              placeholder="0"
              value={formCantidad()}
              onInput={(e) => setFormCantidad(e.currentTarget.value)}
            />
          </div>

          <div class="field" style={{ 'min-width': '80px', 'max-width': '100px' }}>
            <label>Unidad</label>
            <select
              value={formUnidad()}
              onChange={(e) => setFormUnidad(e.currentTarget.value)}
              disabled={!productoSeleccionado()}
            >
              <For each={unidadesCompatibles()}>
                {(u) => <option value={u.value}>{u.label}</option>}
              </For>
            </select>
          </div>

          <div class="field" style={{ 'min-width': '80px', 'max-width': '100px' }}>
            <label>Costo Adq.</label>
            <input
              type="number"
              min="0"
              placeholder="0.00"
              value={formCostoAdic()}
              onInput={(e) => setFormCostoAdic(e.currentTarget.value)}
            />
          </div>

          <button
            class="btn btn-primary almacen-add-btn"
            onClick={handleRegistrar}
            disabled={!canSubmit()}
            title={canSubmit() ? 'Registrar egreso' : 'Seleccione un producto del catálogo, tipo y una cantidad mayor a 0'}
            style={{ 'align-self': 'flex-end' }}
          >
            <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>

        {/* Stats sub-bar */}
        <div class="almacen-stats-bar">
          <div>Total Egresos: <strong>{stats().total}</strong></div>
          <div style={{ color: 'var(--green-deep)' }}>Ventas: <strong>{stats().ventas}</strong></div>
          <div style={{ color: 'var(--rust)' }}>Mermas: <strong>{stats().mermas}</strong></div>
        </div>
      </div>

      {/* ─── Lista de Egresos ────────────────────────────────── */}
      <div style={{ 'margin-bottom': '14px' }}>
        <h2 style={{
          'font-family': 'var(--font-display)',
          'font-size': '18px',
          'font-weight': '600',
          margin: '0 0 4px',
          color: 'var(--ink)',
        }}>
          Lista de Egresos
        </h2>
        <p style={{ 'font-size': '12px', color: 'var(--ink-faint)', margin: '0' }}>
          Historial de salidas de stock registradas en almacén
        </p>
      </div>

      <Show when={formError()}>
        <div style={{
          'background': 'rgba(231, 76, 60, 0.12)',
          'border': '1px solid var(--rust, #e74c3c)',
          'border-radius': '6px',
          'padding': '8px 12px',
          'margin-bottom': '10px',
          'font-size': '12px',
          'color': 'var(--rust, #c0392b)',
        }}>
          {formError()}
        </div>
      </Show>

      <DataTable
        columns={columns}
        data={almacenStore.filteredEgresos()}
        loading={almacenStore.loadingEgresos()}
        emptyMessage="No se encontraron egresos registrados."
      />
    </section>
  );
};
