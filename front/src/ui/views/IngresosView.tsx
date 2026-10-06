/**
 * Vista de Registro de Ingresos a Almacén.
 *
 * Basado en wireframe: docs/borradores/wireframes/ui_inv_ingreso.png
 * Feature: F4 / MF-04-01 — Registro de Ingreso de Productos / Semillas
 *
 * Conectado a backend real vía API REST.
 * El selector de productos se construye dinámicamente desde el catálogo oficial (F1).
 */

import { Component, createSignal, For, Show, createMemo, onMount } from 'solid-js';
import { almacenStore } from '../store/almacenStore';
import {
  CATEGORIAS,
  TIPOS_INGRESO,
  type CategoriaAlmacen,
  type TipoIngreso,
} from '../../domain/models/Almacen';
import type { Product } from '../../domain/models/Product';
import { DataTable, type Column } from '../components/DataTable';

export const IngresosView: Component = () => {
  // ─── Form state (inline header) ──────────────────────────────
  const [formFecha, setFormFecha] = createSignal(new Date().toISOString().split('T')[0]);
  const [formCategoria, setFormCategoria] = createSignal<CategoriaAlmacen | ''>('');
  const [formProductoId, setFormProductoId] = createSignal('');
  const [formTipo, setFormTipo] = createSignal<TipoIngreso | ''>('');
  const [formProcedencia, setFormProcedencia] = createSignal('');
  const [formCantidad, setFormCantidad] = createSignal<string>('');
  const [formError, setFormError] = createSignal<string | null>(null);

  // Cargar catálogo y movimientos reales al montar
  onMount(async () => {
    await almacenStore.asegurarCatalogoCargado();
    almacenStore.cargarIngresos();
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

  const canSubmit = createMemo(() => {
    const cant = Number(formCantidad());
    return (
      formProductoId() !== '' &&
      formTipo() !== '' &&
      !isNaN(cant) &&
      cant > 0 &&
      !almacenStore.loadingIngresos()
    );
  });

  async function handleRegistrar() {
    const cant = Number(formCantidad());
    if (isNaN(cant) || cant <= 0) {
      setFormError('La cantidad a ingresar debe ser un número mayor a cero.');
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
      await almacenStore.registrarIngreso({
        productoId: prod.id,
        fecha: formFecha(),
        categoria: prod.categoria as CategoriaAlmacen,
        descripcion: prod.nombreVisible,
        tipo: formTipo() as TipoIngreso,
        cantidad: cant,
        unidad: prod.unidadManejo || 'Kilogramo',
        procedencia: formProcedencia(),
      });
      // Reset form parcial (mantener fecha y categoría)
      setFormProductoId('');
      setFormTipo('');
      setFormProcedencia('');
      setFormCantidad('');
    } catch (err: any) {
      setFormError(err.message || 'Error al registrar el ingreso.');
    }
  }

  // ─── Pill styles para tipo de ingreso ─────────────────────────
  function tipoPillClass(tipo: string): string {
    switch (tipo) {
      case 'Compra': return 'pill pill-green';
      case 'Recoleccion': return 'pill pill-amber';
      case 'Intercambio': return 'pill pill-earth';
      case 'Devolucion': return 'pill pill-rust';
      default: return 'pill';
    }
  }

  function tipoLabel(tipo: string): string {
    const found = TIPOS_INGRESO.find((t) => t.value === tipo);
    return found ? found.label : tipo;
  }

  // ─── Estadísticas ─────────────────────────────────────────────
  const stats = createMemo(() => {
    const list = almacenStore.filteredIngresos();
    const total = list.length;
    const compras = list.filter((i) => i.tipo === 'Compra').length;
    const recolecciones = list.filter((i) => i.tipo === 'Recoleccion').length;
    return { total, compras, recolecciones };
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
      header: 'Procedencia',
      width: '140px',
      cell: (item: any) => (
        <span style={{ 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
          {item.procedencia || '—'}
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
        <p class="panel-eyebrow">Gestión de Almacén</p>
        <h1 class="panel-title">Registro de Ingresos</h1>
        <p class="panel-desc">
          Recepción y registro de productos, semillas y materiales que ingresan al almacén.
        </p>
      </div>

      {/* ─── Formulario Inline (cabecera) ────────────────────── */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar almacen-form-toolbar">
          <div class="field" style={{ 'min-width': '130px' }}>
            <label>Fecha</label>
            <input
              type="date"
              value={formFecha()}
              onInput={(e) => setFormFecha(e.currentTarget.value)}
            />
          </div>

          <div class="field" style={{ 'min-width': '130px' }}>
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
              onChange={(e) => setFormProductoId(e.currentTarget.value)}
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

          <div class="field" style={{ 'min-width': '130px' }}>
            <label>Tipo</label>
            <select
              value={formTipo()}
              onChange={(e) => setFormTipo(e.currentTarget.value as TipoIngreso | '')}
            >
              <option value="">— Tipo —</option>
              <For each={TIPOS_INGRESO}>
                {(t) => <option value={t.value}>{t.label}</option>}
              </For>
            </select>
          </div>

          <div class="field" style={{ 'min-width': '120px' }}>
            <label>Procedencia</label>
            <input
              type="text"
              placeholder="Origen..."
              value={formProcedencia()}
              onInput={(e) => setFormProcedencia(e.currentTarget.value)}
            />
          </div>

          <div class="field" style={{ 'min-width': '80px', 'max-width': '100px' }}>
            <label>Cantidad</label>
            <input
              type="number"
              min="0"
              placeholder="0"
              value={formCantidad()}
              onInput={(e) => setFormCantidad(e.currentTarget.value)}
            />
          </div>

          <Show when={productoSeleccionado()}>
            <div class="field" style={{ 'min-width': '60px', 'max-width': '80px' }}>
              <label>Unidad</label>
              <input
                type="text"
                value={productoSeleccionado()?.unidadManejo || ''}
                disabled
                style={{ background: 'var(--surface-alt)', color: 'var(--ink-soft)' }}
              />
            </div>
          </Show>

          <button
            class="btn btn-primary almacen-add-btn"
            onClick={handleRegistrar}
            disabled={!canSubmit()}
            title={canSubmit() ? 'Registrar ingreso' : 'Seleccione un producto del catálogo, tipo y una cantidad mayor a 0'}
            style={{ 'align-self': 'flex-end' }}
          >
            <svg style={{ width: '18px', height: '18px' }} fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M12 4v16m8-8H4" />
            </svg>
          </button>
        </div>

        {/* Stats sub-bar */}
        <div class="almacen-stats-bar">
          <div>Total Registros: <strong>{stats().total}</strong></div>
          <div style={{ color: 'var(--green-deep)' }}>Compras: <strong>{stats().compras}</strong></div>
          <div style={{ color: 'var(--amber)' }}>Recolecciones: <strong>{stats().recolecciones}</strong></div>
        </div>
      </div>

      {/* ─── Lista de Ingresos ────────────────────────────────── */}
      <div style={{ 'margin-bottom': '14px' }}>
        <h2 style={{
          'font-family': 'var(--font-display)',
          'font-size': '18px',
          'font-weight': '600',
          margin: '0 0 4px',
          color: 'var(--ink)',
        }}>
          Lista de Ingresos
        </h2>
        <p style={{ 'font-size': '12px', color: 'var(--ink-faint)', margin: '0' }}>
          Historial de recepciones registradas en almacén
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
        data={almacenStore.filteredIngresos()}
        loading={almacenStore.loadingIngresos()}
        emptyMessage="No se encontraron ingresos registrados."
      />
    </section>
  );
};
