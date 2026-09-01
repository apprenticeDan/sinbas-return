/**
 * Vista de Registro de Ingresos a Almacén (Mock UI).
 *
 * Basado en wireframe: docs/borradores/wireframes/ui_inv_ingreso.png
 * Feature: F4 / MF-04-01 — Registro de Ingreso de Productos / Semillas
 *
 * MOCK: Los datos son estáticos. No hay comunicación con backend.
 * El formulario inline en la cabecera permite "registrar" ingresos
 * que se agregan al store local en memoria.
 */

import { Component, createSignal, For, Show, createMemo } from 'solid-js';
import { almacenStore } from '../store/almacenStore';
import {
  CATEGORIAS,
  TIPOS_INGRESO,
  DESCRIPCIONES_POR_CATEGORIA,
  type CategoriaAlmacen,
  type TipoIngreso,
} from '../../domain/models/Almacen';
import { DataTable, type Column } from '../components/DataTable';

export const IngresosView: Component = () => {
  // ─── Form state (inline header) ──────────────────────────────
  const [formFecha, setFormFecha] = createSignal(new Date().toISOString().split('T')[0]);
  const [formCategoria, setFormCategoria] = createSignal<CategoriaAlmacen | ''>('');
  const [formDescripcion, setFormDescripcion] = createSignal('');
  const [formTipo, setFormTipo] = createSignal<TipoIngreso | ''>('');
  const [formProcedencia, setFormProcedencia] = createSignal('');
  const [formCantidad, setFormCantidad] = createSignal<string>('');

  const descripcionesDisponibles = createMemo(() => {
    const cat = formCategoria();
    return cat ? DESCRIPCIONES_POR_CATEGORIA[cat] : [];
  });

  const canSubmit = createMemo(() => {
    return formCategoria() !== '' && formDescripcion() !== '' && formTipo() !== '';
  });

  function handleRegistrar() {
    if (!canSubmit()) return;
    almacenStore.registrarIngreso({
      fecha: formFecha(),
      categoria: formCategoria() as CategoriaAlmacen,
      descripcion: formDescripcion(),
      tipo: formTipo() as TipoIngreso,
      cantidad: formCantidad() ? Number(formCantidad()) : null,
      procedencia: formProcedencia(),
    });
    // Reset form parcial (mantener fecha y categoría)
    setFormDescripcion('');
    setFormTipo('');
    setFormProcedencia('');
    setFormCantidad('');
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
      width: '90px',
      cell: (item: any) => (
        <span style={{
          'font-family': 'monospace',
          'font-size': '14px',
          'font-weight': '700',
          color: item.cantidad != null ? 'var(--ink)' : 'var(--ink-faint)',
        }}>
          {item.cantidad != null ? item.cantidad : '—'}
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
                setFormDescripcion('');
              }}
            >
              <option value="">— Seleccionar —</option>
              <For each={CATEGORIAS}>
                {(c) => <option value={c.value}>{c.label}</option>}
              </For>
            </select>
          </div>

          <div class="field" style={{ 'min-width': '170px', flex: '1' }}>
            <label>Descripción</label>
            <select
              value={formDescripcion()}
              onChange={(e) => setFormDescripcion(e.currentTarget.value)}
              disabled={!formCategoria()}
            >
              <option value="">— Seleccionar producto —</option>
              <For each={descripcionesDisponibles()}>
                {(d) => <option value={d}>{d}</option>}
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

          <button
            class="btn btn-primary almacen-add-btn"
            onClick={handleRegistrar}
            disabled={!canSubmit()}
            title={canSubmit() ? 'Registrar ingreso' : 'Completa categoría, descripción y tipo'}
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

      <DataTable
        columns={columns}
        data={almacenStore.filteredIngresos()}
        loading={false}
        emptyMessage="No se encontraron ingresos registrados."
      />
    </section>
  );
};
