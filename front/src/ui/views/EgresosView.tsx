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
import { ventaStore } from '../store/ventaStore';
import type {
  OrdenDespachoDto,
  DespachoConfirmadoDto,
} from '../../infrastructure/api/ApiVentaGateway';
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
  // ─── Tabs de Navegación (F8) ──────────────────────────────────
  const [tabActivo, setTabActivo] = createSignal<'despachos' | 'manual'>('despachos');

  // ─── Despachos Pendientes State ───────────────────────────────
  const [modalDespachoOpen, setModalDespachoOpen] = createSignal(false);
  const [despachoSeleccionadoLocal, setDespachoSeleccionadoLocal] = createSignal<OrdenDespachoDto | null>(null);
  const [obsDespacho, setObsDespacho] = createSignal('');
  const [despachando, setDespachando] = createSignal(false);
  const [despachoError, setDespachoError] = createSignal<string | null>(null);
  const [despachoExito, setDespachoExito] = createSignal<DespachoConfirmadoDto | null>(null);

  // ─── Form state (inline header para egreso manual) ───────────
  const [formFecha, setFormFecha] = createSignal(new Date().toISOString().split('T')[0]);
  const [formCategoria, setFormCategoria] = createSignal<CategoriaAlmacen | ''>('');
  const [formProductoId, setFormProductoId] = createSignal('');
  const [formTipo, setFormTipo] = createSignal<TipoEgreso | ''>('');
  const [formConsignatario, setFormConsignatario] = createSignal('');
  const [formCantidad, setFormCantidad] = createSignal<string>('');
  const [formUnidad, setFormUnidad] = createSignal<string>('Kilogramo');
  const [formCostoAdic, setFormCostoAdic] = createSignal<string>('');
  const [formError, setFormError] = createSignal<string | null>(null);

  // Cargar catálogo, clientes, movimientos de egreso y despachos al montar
  onMount(async () => {
    await Promise.all([
      almacenStore.asegurarCatalogoCargado(),
      almacenStore.asegurarClientesCargados(),
      ventaStore.cargarDespachosPendientes(),
    ]);
    almacenStore.cargarEgresos();
  });

  const abrirModalDespacho = async (d: OrdenDespachoDto) => {
    setDespachoSeleccionadoLocal(d);
    setObsDespacho('');
    setDespachoError(null);
    setDespachoExito(null);
    setModalDespachoOpen(true);
    await ventaStore.cargarDespachoDetalle(d.id);
  };

  const handleConfirmarDespachoFisico = async (e: Event) => {
    e.preventDefault();
    const d = despachoSeleccionadoLocal();
    if (!d) return;

    setDespachando(true);
    setDespachoError(null);
    try {
      const res = await ventaStore.confirmarDespacho(d.id, obsDespacho().trim() || undefined);
      setDespachoExito(res);
      // Recargar histórico de egresos de almacén para reflejar el movimiento tipo Salida / Venta
      almacenStore.cargarEgresos();
    } catch (err: any) {
      setDespachoError(err.message || 'Error al ejecutar despacho físico');
    } finally {
      setDespachando(false);
    }
  };

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
        <h1 class="panel-title">Egresos & Despacho Físico (F8)</h1>
        <p class="panel-desc">
          Atención física de órdenes de venta pendientes de entrega con deducción FIFO y registro de salidas por uso interno o merma.
        </p>
      </div>

      {/* ─── Barra de Pestañas / Tabs ────────────────────────── */}
      <div style={{ display: 'flex', gap: '8px', 'margin-bottom': '20px' }}>
        <button
          class={`btn ${tabActivo() === 'despachos' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setTabActivo('despachos')}
          style={{ 'font-weight': '600', display: 'flex', 'align-items': 'center', gap: '6px' }}
        >
          📦 Despachos Pendientes de Venta
          <Show when={ventaStore.despachosPendientes().length > 0}>
            <span
              style={{
                background: 'var(--amber, #f59e0b)',
                color: '#fff',
                padding: '1px 7px',
                'border-radius': '10px',
                'font-size': '11px',
                'font-weight': '700',
              }}
            >
              {ventaStore.despachosPendientes().length}
            </span>
          </Show>
        </button>

        <button
          class={`btn ${tabActivo() === 'manual' ? 'btn-primary' : 'btn-ghost'}`}
          onClick={() => setTabActivo('manual')}
          style={{ 'font-weight': '600' }}
        >
          📤 Salidas Manuales (Uso Interno / Merma)
        </button>
      </div>

      {/* ─── TAB 1: Despachos Pendientes (F8) ────────────────── */}
      <Show when={tabActivo() === 'despachos'}>
        <div class="card" style={{ padding: '0', overflow: 'hidden', 'margin-bottom': '20px' }}>
          <div style={{ padding: '16px', 'border-bottom': '1px solid var(--border-color)', display: 'flex', 'justify-content': 'space-between', 'align-items': 'center' }}>
            <div>
              <h3 style={{ margin: '0', 'font-size': '16px', color: 'var(--ink)' }}>
                Órdenes Pendientes de Salida Física
              </h3>
              <p style={{ margin: '2px 0 0', 'font-size': '12px', color: 'var(--ink-soft)' }}>
                Ventas confirmadas por Comercial listas para preparar y retirar de almacén mediante algoritmo FIFO.
              </p>
            </div>
            <button
              class="btn btn-ghost"
              style={{ 'font-size': '12px' }}
              onClick={() => ventaStore.cargarDespachosPendientes()}
            >
              🔄 Actualizar
            </button>
          </div>

          <div style={{ 'overflow-x': 'auto' }}>
            <table class="data-table" style={{ width: '100%', 'border-collapse': 'collapse' }}>
              <thead>
                <tr style={{ 'border-bottom': '1px solid var(--border-color)', 'text-align': 'left' }}>
                  <th style={{ padding: '10px 14px', 'font-size': '12px' }}>Código Despacho</th>
                  <th style={{ padding: '10px 14px', 'font-size': '12px' }}>Venta Asociada</th>
                  <th style={{ padding: '10px 14px', 'font-size': '12px' }}>Cliente</th>
                  <th style={{ padding: '10px 14px', 'font-size': '12px' }}>Fecha</th>
                  <th style={{ padding: '10px 14px', 'font-size': '12px' }}>Materiales a Despachar</th>
                  <th style={{ padding: '10px 14px', 'font-size': '12px', 'text-align': 'center' }}>Acción</th>
                </tr>
              </thead>
              <tbody>
                <Show
                  when={ventaStore.despachosPendientes().length > 0}
                  fallback={
                    <tr>
                      <td colspan="6" style={{ padding: '28px', 'text-align': 'center', color: 'var(--ink-soft)', 'font-size': '13px' }}>
                        <Show when={ventaStore.loadingDespachos()} fallback="No hay despachos pendientes en este momento. ¡Todo al día!">
                          Cargando órdenes de despacho...
                        </Show>
                      </td>
                    </tr>
                  }
                >
                  <For each={ventaStore.despachosPendientes()}>
                    {(d) => (
                      <tr style={{ 'border-bottom': '1px solid var(--border-soft)' }}>
                        <td style={{ padding: '10px 14px', 'font-family': 'monospace', 'font-weight': '700', color: 'var(--forest)' }}>
                          {d.codigo}
                        </td>
                        <td style={{ padding: '10px 14px', 'font-family': 'monospace', color: 'var(--ink-soft)' }}>
                          {d.codigoVenta || '—'}
                        </td>
                        <td style={{ padding: '10px 14px', 'font-weight': '600' }}>
                          {d.clienteNombre || 'Sin cliente registrado'}
                        </td>
                        <td style={{ padding: '10px 14px', 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
                          {d.creadoEn ? d.creadoEn.substring(0, 10) : '—'}
                        </td>
                        <td style={{ padding: '10px 14px', 'font-size': '12.5px' }}>
                          <For each={d.lineas}>
                            {(l) => (
                              <div style={{ 'line-height': '1.3' }}>
                                • <strong>{l.cantidad} {l.unidad}</strong> de {l.nombreProducto}
                              </div>
                            )}
                          </For>
                        </td>
                        <td style={{ padding: '10px 14px', 'text-align': 'center' }}>
                          <button
                            class="btn btn-primary"
                            style={{ 'font-size': '12px', padding: '5px 12px' }}
                            onClick={() => abrirModalDespacho(d)}
                          >
                            📦 Despachar (FIFO)
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
      </Show>

      {/* ─── TAB 2: Salidas Manuales ─────────────────────────── */}
      <Show when={tabActivo() === 'manual'}>
        {/* Formulario Inline (cabecera) */}
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
      </Show>

      {/* ─── Modal: Confirmación de Despacho Físico FIFO (F8) ───── */}
      <Show when={modalDespachoOpen() && despachoSeleccionadoLocal()}>
        <div class="modal-backdrop" onClick={() => setModalDespachoOpen(false)}>
          <div class="modal card" style={{ 'max-width': '650px', width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <div class="modal-header" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-bottom': '1px solid var(--border-color)', 'padding-bottom': '12px' }}>
              <div>
                <h3 style={{ margin: '0', 'font-size': '18px', color: 'var(--ink)' }}>
                  Despacho Físico: {despachoSeleccionadoLocal()?.codigo}
                </h3>
                <span class="pill pill-amber" style={{ 'font-size': '11px', 'margin-top': '4px' }}>
                  Pendiente de Entrega
                </span>
              </div>
              <button class="btn btn-ghost" onClick={() => setModalDespachoOpen(false)}>✕</button>
            </div>

            <Show
              when={despachoExito()}
              fallback={
                <form onSubmit={handleConfirmarDespachoFisico}>
                  <div class="modal-body" style={{ 'margin-top': '16px', display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
                    <div style={{ display: 'grid', 'grid-template-columns': 'repeat(2, 1fr)', gap: '10px', background: 'var(--panel-bg-soft)', padding: '12px', 'border-radius': '6px', 'font-size': '13px' }}>
                      <div>
                        <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Venta Asociada</span>
                        <strong style={{ 'font-family': 'monospace' }}>{despachoSeleccionadoLocal()?.codigoVenta || '—'}</strong>
                      </div>
                      <div>
                        <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Cliente</span>
                        <strong>{despachoSeleccionadoLocal()?.clienteNombre || 'Sin cliente'}</strong>
                      </div>
                    </div>

                    <div>
                      <h4 style={{ margin: '0 0 6px 0', 'font-size': '13px', color: 'var(--ink)' }}>
                        Materiales Solicitados
                      </h4>
                      <table style={{ width: '100%', 'border-collapse': 'collapse', 'font-size': '12.5px' }}>
                        <thead>
                          <tr style={{ 'border-bottom': '1px solid var(--border-color)', color: 'var(--ink-soft)' }}>
                            <th style={{ 'text-align': 'left', padding: '6px 0' }}>Producto</th>
                            <th style={{ 'text-align': 'right', padding: '6px 0' }}>Cantidad Solicitada</th>
                          </tr>
                        </thead>
                        <tbody>
                          <For each={despachoSeleccionadoLocal()?.lineas}>
                            {(l) => (
                              <tr style={{ 'border-bottom': '1px solid var(--border-soft)' }}>
                                <td style={{ padding: '6px 0', 'font-weight': '600' }}>{l.nombreProducto}</td>
                                <td style={{ padding: '6px 0', 'text-align': 'right', 'font-family': 'monospace', 'font-weight': '700' }}>
                                  {l.cantidad} {l.unidad}
                                </td>
                              </tr>
                            )}
                          </For>
                        </tbody>
                      </table>
                    </div>

                    {/* Sugerencia FIFO de lotes */}
                    <div style={{ background: '#f0fdf4', border: '1px solid #bbf7d0', padding: '12px', 'border-radius': '6px' }}>
                      <div style={{ 'font-weight': '700', 'font-size': '12px', color: '#166534', 'margin-bottom': '6px', display: 'flex', 'align-items': 'center', gap: '6px' }}>
                        <span>⚡ Algoritmo FIFO de Lotes Activos</span>
                      </div>
                      <p style={{ 'font-size': '11.5px', color: '#15803d', margin: '0 0 8px' }}>
                        El sistema descontará automáticamente el stock físico de los lotes más antiguos disponibles cumpliendo la regla de inventario.
                      </p>

                      <Show when={ventaStore.despachoSeleccionado()?.sugerenciasFifo}>
                        <For each={ventaStore.despachoSeleccionado()?.sugerenciasFifo}>
                          {(sug) => (
                            <div style={{ 'font-size': '11.5px', 'margin-bottom': '4px', color: '#166534' }}>
                              • <strong>{sug.nombreProducto}</strong>: {sug.lotesSugeridos.length} lote(s) comprometido(s)
                            </div>
                          )}
                        </For>
                      </Show>
                    </div>

                    <div>
                      <label style={{ display: 'block', 'font-size': '12px', 'font-weight': '600', color: 'var(--ink)', 'margin-bottom': '4px' }}>
                        Observaciones del Despacho (Opcional)
                      </label>
                      <input
                        type="text"
                        placeholder="Ej. Entregado al transportista, precinto #123..."
                        value={obsDespacho()}
                        onInput={(e) => setObsDespacho(e.currentTarget.value)}
                        style={{ width: '100%', padding: '8px 10px', 'font-size': '12.5px' }}
                      />
                    </div>

                    <Show when={despachoError()}>
                      <div style={{ background: 'rgba(231,76,60,0.12)', border: '1px solid var(--rust)', color: 'var(--rust)', padding: '8px 12px', 'border-radius': '6px', 'font-size': '12px' }}>
                        {despachoError()}
                      </div>
                    </Show>
                  </div>

                  <div class="modal-footer" style={{ 'margin-top': '20px', display: 'flex', 'justify-content': 'flex-end', gap: '8px' }}>
                    <button
                      type="button"
                      class="btn btn-secondary"
                      onClick={() => setModalDespachoOpen(false)}
                      disabled={despachando()}
                    >
                      Cancelar
                    </button>
                    <button
                      type="submit"
                      class="btn btn-primary"
                      disabled={despachando()}
                      style={{ display: 'flex', 'align-items': 'center', gap: '6px' }}
                    >
                      {despachando() ? 'Procesando Salida...' : '✓ Confirmar Salida Física (FIFO)'}
                    </button>
                  </div>
                </form>
              }
            >
              <div style={{ 'text-align': 'center', padding: '20px 10px' }}>
                <div style={{ width: '48px', height: '48px', background: '#dcfce7', color: '#16a34a', 'border-radius': '50%', display: 'flex', 'align-items': 'center', 'justify-content': 'center', margin: '0 auto 12px', 'font-size': '24px' }}>
                  ✓
                </div>
                <h3 style={{ margin: '0 0 6px', 'font-size': '18px', color: 'var(--ink)' }}>
                  ¡Despacho Físico Confirmado!
                </h3>
                <p style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', margin: '0 0 16px' }}>
                  Se ha generado la salida en el kárdex y se ha actualizado el stock de los lotes correspondientes.
                </p>

                <div style={{ background: 'var(--panel-bg-soft)', padding: '12px', 'border-radius': '6px', 'text-align': 'left', 'font-size': '12.5px', display: 'flex', 'flex-direction': 'column', gap: '6px', 'margin-bottom': '16px' }}>
                  <div>
                    <span style={{ color: 'var(--ink-soft)' }}>Despacho: </span>
                    <strong style={{ 'font-family': 'monospace' }}>{despachoExito()?.codigoDespacho}</strong>
                  </div>
                  <div>
                    <span style={{ color: 'var(--ink-soft)' }}>Movimiento de Inventario: </span>
                    <strong style={{ 'font-family': 'monospace', color: 'var(--forest)' }}>{despachoExito()?.movimientoId}</strong>
                  </div>
                  <div>
                    <span style={{ color: 'var(--ink-soft)' }}>Fecha Operación: </span>
                    <span>{despachoExito()?.fecha}</span>
                  </div>
                  <div>
                    <span style={{ color: 'var(--ink-soft)' }}>Lotes descargados: </span>
                    <span>{despachoExito()?.lineasDespachadas?.length || 0} línea(s) FIFO</span>
                  </div>
                </div>

                <button
                  class="btn btn-primary"
                  style={{ width: '100%' }}
                  onClick={() => {
                    setModalDespachoOpen(false);
                    setDespachoExito(null);
                  }}
                >
                  Cerrar
                </button>
              </div>
            </Show>
          </div>
        </div>
      </Show>
    </section>
  );
};
