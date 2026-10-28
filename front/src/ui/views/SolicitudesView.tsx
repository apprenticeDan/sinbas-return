/**
 * Vista de Solicitudes de Egreso Interno.
 *
 * Fase 6b: UI para crear, aprobar/rechazar y confirmar solicitudes
 * de uso interno, merma y ajuste de inventario.
 */

import { Component, createSignal, createMemo, For, Show, onMount, onCleanup } from 'solid-js';
import { solicitudStore } from '../store/solicitudStore';
import { almacenStore } from '../store/almacenStore';
import { authStore } from '../store/authStore';
import { DataTable, type Column } from '../components/DataTable';
import type { Product } from '../../domain/models/Product';
import {
  type SolicitudDto,
  type TipoEgresoInterno,
  type SignoEgresoInterno,
  type EstadoSolicitudInterna,
  type DestinoUso,
  type CrearSolicitudRequest,
} from '../../domain/models/Solicitud';
import { ApiLoteGateway } from '../../infrastructure/api/ApiLoteGateway';
import type { LoteItem } from '../../domain/models/Lote';

// ─── Helpers ──────────────────────────────────────────────────────

function estadoBadgeClass(estado: EstadoSolicitudInterna): string {
  switch (estado) {
    case 'Pendiente': return 'badge-warning';
    case 'Aprobada': return 'badge-info';
    case 'Rechazada': return 'badge-danger';
    case 'Confirmada': return 'badge-success';
    default: return 'badge-secondary';
  }
}

function tipoPillClass(tipo: TipoEgresoInterno): string {
  switch (tipo) {
    case 'UsoInterno': return 'pill pill-earth';
    case 'Ajuste': return 'pill pill-amber';
    case 'Merma': return 'pill pill-rust';
    default: return 'pill';
  }
}

function tipoLabel(tipo: TipoEgresoInterno): string {
  switch (tipo) {
    case 'UsoInterno': return 'Uso Interno';
    case 'Ajuste': return 'Ajuste';
    case 'Merma': return 'Merma';
    default: return tipo;
  }
}

function unidadAbrev(u: string): string {
  switch (u) {
    case 'Kilogramo': return 'kg';
    case 'Gramo': return 'g';
    case 'Litro': return 'l';
    case 'Mililitro': return 'ml';
    case 'UnidadDiscreta': return 'u';
    default: return u;
  }
}

function unidadesCompatibles(prod: Product | null): { value: string; label: string }[] {
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
  return [{ value: 'UnidadDiscreta', label: 'u' }];
}

export const SolicitudesView: Component = () => {
  // ─── Permisos ────────────────────────────────────────────────
  const canCreate = () => authStore.hasAnyRole(['Administrador', 'Almacen', 'Gerencia']);
  const canApprove = () => authStore.hasAnyRole(['Administrador', 'Gerencia']);
  const canConfirm = () => authStore.hasAnyRole(['Administrador', 'Almacen', 'Gerencia']);

  function accionesDisponibles(s: SolicitudDto): {
    puedeAprobar: boolean;
    puedeRechazar: boolean;
    puedeConfirmar: boolean;
    esPropia: boolean;
    exentoRN11: boolean;
  } {
    const currentEmpleadoId = authStore.user()?.empleadoId;
    const esPropia = !!currentEmpleadoId && s.solicitanteId === currentEmpleadoId;
    const exentoRN11 = authStore.hasAnyRole(['Gerencia', 'Administrador']);

    if (s.estado === 'Pendiente') {
      if (s.tipo === 'UsoInterno') {
        return {
          puedeAprobar: false,
          puedeRechazar: false,
          puedeConfirmar: canConfirm(),
          esPropia,
          exentoRN11,
        };
      }
      // Merma / Ajuste
      const puedeAprobarRechazar = canApprove() && (!esPropia || exentoRN11);
      return {
        puedeAprobar: puedeAprobarRechazar,
        puedeRechazar: puedeAprobarRechazar,
        puedeConfirmar: false,
        esPropia,
        exentoRN11,
      };
    }
    if (s.estado === 'Aprobada') {
      return {
        puedeAprobar: false,
        puedeRechazar: false,
        puedeConfirmar: canConfirm(),
        esPropia,
        exentoRN11,
      };
    }
    return { puedeAprobar: false, puedeRechazar: false, puedeConfirmar: false, esPropia, exentoRN11 };
  }

  // ─── Estado local: modales ───────────────────────────────────
  const [modalNuevaOpen, setModalNuevaOpen] = createSignal(false);
  const [modalRechazoOpen, setModalRechazoOpen] = createSignal(false);
  const [modalDetalleOpen, setModalDetalleOpen] = createSignal(false);
  const [solicitudSeleccionada, setSolicitudSeleccionada] = createSignal<SolicitudDto | null>(null);
  const [confirmandoAccion, setConfirmandoAccion] = createSignal(false);
  const [accionError, setAccionError] = createSignal<string | null>(null);

  // ─── Form creación ──────────────────────────────────────────
  const [formTipo, setFormTipo] = createSignal<TipoEgresoInterno | ''>('');
  const [formDestino, setFormDestino] = createSignal<DestinoUso>('Vivero');
  const [formDestinoOtro, setFormDestinoOtro] = createSignal('');
  const [formSigno, setFormSigno] = createSignal<SignoEgresoInterno>('Negativo');
  const [formProductoId, setFormProductoId] = createSignal('');
  const [formCantidad, setFormCantidad] = createSignal('');
  const [formUnidad, setFormUnidad] = createSignal('Kilogramo');
  const [formMotivo, setFormMotivo] = createSignal('');
  const [formError, setFormError] = createSignal<string | null>(null);

  // ─── Form rechazo ──────────────────────────────────────────
  const [motivoRechazo, setMotivoRechazo] = createSignal('');
  const [rechazoError, setRechazoError] = createSignal<string | null>(null);

  // ─── Producto seleccionado ──────────────────────────────────
  const productoSeleccionado = createMemo(() => {
    const id = formProductoId();
    if (!id) return null;
    return almacenStore.productosCache().find((p) => p.id === id) || null;
  });

  const opcionesUnidad = createMemo(() => unidadesCompatibles(productoSeleccionado()));

  const [formLoteId, setFormLoteId] = createSignal('');
  const [lotesProducto, setLotesProducto] = createSignal<LoteItem[]>([]);
  const [loadingLotes, setLoadingLotes] = createSignal(false);

  async function handleProductoChange(id: string) {
    setFormProductoId(id);
    setFormLoteId('');
    setLotesProducto([]);
    const prod = almacenStore.productosCache().find((p) => p.id === id);
    if (prod && prod.unidadManejo) {
      setFormUnidad(prod.unidadManejo);
    }
    if (id) {
      setLoadingLotes(true);
      try {
        const items = await ApiLoteGateway.listarLotes(id, 'Activo');
        setLotesProducto(items);
      } catch (err) {
        console.error('Error cargando lotes del producto:', err);
      } finally {
        setLoadingLotes(false);
      }
    }
  }

  // ─── Helpers de form y modales ────────────────────────────
  function limpiarForm() {
    setFormTipo('');
    setFormDestino('Vivero');
    setFormDestinoOtro('');
    setFormSigno('Negativo');
    setFormProductoId('');
    setFormLoteId('');
    setLotesProducto([]);
    setFormCantidad('');
    setFormUnidad('Kilogramo');
    setFormMotivo('');
    setFormError(null);
  }

  function cerrarFormNueva() {
    setModalNuevaOpen(false);
    limpiarForm();
  }

  function tieneDatosFormNueva(): boolean {
    return (
      formTipo() !== '' ||
      formProductoId() !== '' ||
      formCantidad() !== '' ||
      formMotivo().trim() !== ''
    );
  }

  function intentarCerrarFormNueva() {
    if (tieneDatosFormNueva()) {
      if (window.confirm('¿Descartar la solicitud en curso? Se perderán los datos ingresados.')) {
        cerrarFormNueva();
      }
    } else {
      cerrarFormNueva();
    }
  }

  function cerrarRechazo() {
    setModalRechazoOpen(false);
    setMotivoRechazo('');
    setRechazoError(null);
  }

  // ─── Mount ────────────────────────────────────────────────
  onMount(async () => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        if (modalNuevaOpen()) {
          intentarCerrarFormNueva();
        } else if (modalRechazoOpen() && !confirmandoAccion()) {
          cerrarRechazo();
        } else if (modalDetalleOpen()) {
          setModalDetalleOpen(false);
        }
      }
    };
    window.addEventListener('keydown', handleEscape);
    onCleanup(() => window.removeEventListener('keydown', handleEscape));

    await Promise.all([
      solicitudStore.cargarSolicitudes(),
      almacenStore.asegurarCatalogoCargado(),
    ]);
  });

  async function handleCrear(e: Event) {
    e.preventDefault();
    setFormError(null);

    const tipo = formTipo();
    if (!tipo) { setFormError('Seleccione un tipo de solicitud.'); return; }

    const cant = Number(formCantidad());
    if (isNaN(cant) || cant <= 0) { setFormError('La cantidad debe ser un número mayor a cero.'); return; }

    if (!formProductoId()) { setFormError('Debe seleccionar un producto.'); return; }

    const motivo = formMotivo().trim();
    if (motivo.length < 5) { setFormError('El motivo debe tener al menos 5 caracteres.'); return; }

    if (tipo === 'Ajuste' && formSigno() === 'Positivo' && !formLoteId()) {
      setFormError('Un Ajuste Positivo requiere seleccionar el lote al que se suma el stock.');
      return;
    }

    const req: CrearSolicitudRequest = {
      tipo: tipo as TipoEgresoInterno,
      signo: tipo === 'Ajuste' ? formSigno() : 'Negativo',
      productoId: formProductoId() || null,
      loteId: formLoteId() || null,
      cantidad: cant,
      unidad: formUnidad(),
      motivo,
    };

    if (tipo === 'UsoInterno') {
      req.destinoTipo = formDestino();
      if (formDestino() === 'Otro') {
        const otro = formDestinoOtro().trim();
        if (!otro) { setFormError('Especifique el destino.'); return; }
        req.destinoOtro = otro;
      }
    }

    const result = await solicitudStore.crearSolicitud(req);
    if (result.ok) {
      cerrarFormNueva();
    } else {
      setFormError(result.error || 'Error al crear solicitud');
    }
  }

  // ─── Acciones ──────────────────────────────────────────────
  async function handleAprobar(id: string) {
    setConfirmandoAccion(true);
    setAccionError(null);
    const r = await solicitudStore.aprobarSolicitud(id);
    setConfirmandoAccion(false);
    if (!r.ok) setAccionError(r.error || 'Error al aprobar');
  }

  function abrirRechazo(s: SolicitudDto) {
    setSolicitudSeleccionada(s);
    setMotivoRechazo('');
    setRechazoError(null);
    setModalRechazoOpen(true);
  }

  async function handleRechazar() {
    const sol = solicitudSeleccionada();
    if (!sol) return;
    const motivo = motivoRechazo().trim();
    if (motivo.length < 5) { setRechazoError('El motivo debe tener al menos 5 caracteres.'); return; }

    setConfirmandoAccion(true);
    setRechazoError(null);
    const r = await solicitudStore.rechazarSolicitud(sol.id, motivo);
    setConfirmandoAccion(false);
    if (r.ok) {
      setModalRechazoOpen(false);
    } else {
      setRechazoError(r.error || 'Error al rechazar');
    }
  }

  async function handleConfirmar(id: string) {
    setConfirmandoAccion(true);
    setAccionError(null);
    const r = await solicitudStore.confirmarSolicitud(id);
    setConfirmandoAccion(false);
    if (!r.ok) setAccionError(r.error || 'Error al confirmar');
  }

  function abrirDetalle(s: SolicitudDto) {
    setSolicitudSeleccionada(s);
    setAccionError(null);
    setModalDetalleOpen(true);
  }

  // ─── Stats ─────────────────────────────────────────────────
  const st = solicitudStore.stats;

  // ─── Table columns ─────────────────────────────────────────
  const columns: Column<SolicitudDto>[] = [
    {
      header: 'Fecha',
      width: '100px',
      cell: (row) => (
        <span style={{ 'font-family': 'monospace', 'font-size': '12.5px', color: 'var(--ink-soft)' }}>
          {row.creadoEn.substring(0, 10)}
        </span>
      ),
    },
    {
      header: 'Tipo',
      width: '120px',
      cell: (row) => (
        <span class={tipoPillClass(row.tipo)}>{tipoLabel(row.tipo)}</span>
      ),
    },
    {
      header: 'Producto',
      cell: (row) => (
        <div>
          <span style={{ 'font-weight': '600', color: 'var(--ink)' }}>
            {row.nombreProducto || '—'}
          </span>
          <Show when={row.codigoLote}>
            <span style={{ display: 'block', 'font-size': '11px', color: 'var(--ink-soft)', 'font-family': 'monospace' }}>
              Lote: {row.codigoLote}
            </span>
          </Show>
        </div>
      ),
    },
    {
      header: 'Cantidad',
      width: '110px',
      align: 'right',
      cell: (row) => (
        <span style={{ 'font-family': 'monospace', 'font-weight': '600' }}>
          {row.cantidad} {unidadAbrev(row.unidad)}
        </span>
      ),
    },
    {
      header: 'Solicitante',
      width: '130px',
      cell: (row) => (
        <span style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', 'font-family': 'monospace' }}>
          {row.solicitanteId}
        </span>
      ),
    },
    {
      header: 'Estado',
      width: '110px',
      cell: (row) => (
        <span class={`px-2 py-0.5 text-xs rounded-full font-medium ${estadoBadgeClass(row.estado)}`}>
          {row.estado}
        </span>
      ),
    },
    {
      header: 'Acciones',
      width: '220px',
      cell: (row) => {
        const acc = accionesDisponibles(row);
        return (
          <div style={{ display: 'flex', 'flex-direction': 'column', gap: '4px' }}>
            <div style={{ display: 'flex', gap: '6px', 'flex-wrap': 'wrap' }}>
              <button class="btn btn-ghost" style={{ 'font-size': '12px', padding: '4px 8px' }} onClick={() => abrirDetalle(row)}>
                Ver
              </button>
              <Show when={acc.puedeAprobar}>
                <button
                  class="btn btn-primary"
                  style={{ 'font-size': '12px', padding: '4px 8px' }}
                  disabled={confirmandoAccion()}
                  onClick={() => handleAprobar(row.id)}
                >
                  Aprobar
                </button>
              </Show>
              <Show when={acc.puedeRechazar}>
                <button
                  class="btn btn-ghost"
                  style={{ 'font-size': '12px', padding: '4px 8px', color: 'var(--rust)' }}
                  disabled={confirmandoAccion()}
                  onClick={() => abrirRechazo(row)}
                >
                  Rechazar
                </button>
              </Show>
              <Show when={acc.puedeConfirmar}>
                <button
                  class="btn btn-primary"
                  style={{ 'font-size': '12px', padding: '4px 8px' }}
                  disabled={confirmandoAccion()}
                  onClick={() => handleConfirmar(row.id)}
                >
                  Confirmar
                </button>
              </Show>
            </div>
            <Show when={acc.esPropia && row.tipo !== 'UsoInterno' && row.estado === 'Pendiente' && !acc.exentoRN11}>
              <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'font-style': 'italic' }}>
                Tu propia solicitud (no auto-aprobable)
              </span>
            </Show>
            <Show when={!acc.esPropia && !canApprove() && row.tipo !== 'UsoInterno' && row.estado === 'Pendiente'}>
              <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'font-style': 'italic' }}>
                Requiere aprobación de Gerencia
              </span>
            </Show>
          </div>
        );
      },
    },
  ];

  return (
    <section class="panel">
      {/* ─── Header ──────────────────────────────────────────── */}
      <div class="panel-head" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'flex-start' }}>
        <div>
          <p class="panel-eyebrow">Gestión de Almacén</p>
          <h1 class="panel-title">Solicitudes de Egreso Interno</h1>
          <p class="panel-desc">
            Solicitudes de uso interno, merma y ajuste de inventario.
          </p>
        </div>
        <Show when={canCreate()}>
          <button
            class="btn btn-primary"
            style={{ 'margin-top': '8px' }}
            onClick={() => { limpiarForm(); setModalNuevaOpen(true); }}
          >
            + Nueva Solicitud
          </button>
        </Show>
      </div>

      {/* ─── Stats ──────────────────────────────────────────── */}
      <div class="almacen-stats-bar" style={{ 'margin-bottom': '14px', 'border-radius': 'var(--radius-s)' }}>
        <span><strong>{st().total}</strong> total</span>
        <span style={{ color: 'var(--amber)' }}><strong>{st().pendientes}</strong> pendientes</span>
        <span style={{ color: 'var(--green)' }}><strong>{st().aprobadas}</strong> aprobadas</span>
        <span style={{ color: 'var(--green-deep)' }}><strong>{st().confirmadas}</strong> confirmadas</span>
      </div>

      {/* ─── Filtros + Tabla ──────────────────────────────────── */}
      <div class="card">
        <div class="toolbar">
          <div class="field" style={{ 'min-width': '200px', flex: '1' }}>
            <label>Buscar</label>
            <input
              type="text"
              placeholder="Producto, lote, motivo..."
              value={solicitudStore.filtroSearch()}
              onInput={(e) => solicitudStore.setFiltroSearch(e.currentTarget.value)}
            />
          </div>
          <div class="field" style={{ 'min-width': '140px' }}>
            <label>Estado</label>
            <select
              value={solicitudStore.filtroEstado()}
              onChange={(e) => solicitudStore.setFiltroEstado(e.currentTarget.value as EstadoSolicitudInterna | '')}
            >
              <option value="">— Todos —</option>
              <option value="Pendiente">Pendiente</option>
              <option value="Aprobada">Aprobada</option>
              <option value="Rechazada">Rechazada</option>
              <option value="Confirmada">Confirmada</option>
            </select>
          </div>
          <div class="field" style={{ 'min-width': '140px' }}>
            <label>Tipo</label>
            <select
              value={solicitudStore.filtroTipo()}
              onChange={(e) => solicitudStore.setFiltroTipo(e.currentTarget.value)}
            >
              <option value="">— Todos —</option>
              <option value="UsoInterno">Uso Interno</option>
              <option value="Ajuste">Ajuste</option>
              <option value="Merma">Merma</option>
            </select>
          </div>
          <button
            class="btn btn-ghost"
            style={{ 'align-self': 'flex-end', 'font-size': '12px', padding: '8px 12px' }}
            onClick={() => solicitudStore.cargarSolicitudes()}
          >
            ↻ Refrescar
          </button>
        </div>

        <Show when={accionError()}>
          <div style={{ padding: '10px 20px', color: 'var(--rust)', 'font-size': '13px', background: 'var(--rust-tint)' }}>
            {accionError()}
          </div>
        </Show>

        <DataTable
          columns={columns}
          data={solicitudStore.filteredSolicitudes()}
          loading={solicitudStore.loading()}
          emptyMessage="No se encontraron solicitudes"
        />
      </div>

      {/* ═══ MODAL: Nueva Solicitud ═══ */}
      <Show when={modalNuevaOpen()}>
        <div class="modal-backdrop">
          <div class="modal card" style={{ 'max-width': '560px', width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <div class="modal-header" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-bottom': '1px solid var(--border-color)', 'padding-bottom': '12px' }}>
              <h3 style={{ margin: '0', 'font-size': '18px', color: 'var(--ink)' }}>Nueva Solicitud de Egreso</h3>
              <button type="button" class="btn btn-ghost" onClick={intentarCerrarFormNueva}>✕</button>
            </div>

            <form onSubmit={handleCrear}>
              <div class="modal-body" style={{ 'margin-top': '16px', display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
                <Show when={formError()}>
                  <div style={{ padding: '8px 12px', 'border-radius': '6px', background: 'var(--rust-tint)', color: 'var(--rust)', 'font-size': '13px' }}>
                    {formError()}
                  </div>
                </Show>

                {/* Tipo */}
                <div class="field">
                  <label>Tipo de Egreso</label>
                  <select
                    value={formTipo()}
                    onChange={(e) => {
                      const v = e.currentTarget.value as TipoEgresoInterno | '';
                      setFormTipo(v);
                      if (v !== 'Ajuste') setFormSigno('Negativo');
                    }}
                  >
                    <option value="">— Seleccione —</option>
                    <option value="UsoInterno">Uso Interno</option>
                    <option value="Merma">Merma</option>
                    <option value="Ajuste">Ajuste</option>
                  </select>
                </div>

                {/* Destino (solo UsoInterno) */}
                <Show when={formTipo() === 'UsoInterno'}>
                  <div class="field">
                    <label>Destino</label>
                    <select
                      value={formDestino()}
                      onChange={(e) => setFormDestino(e.currentTarget.value as DestinoUso)}
                    >
                      <option value="Vivero">Vivero</option>
                      <option value="Laboratorio">Laboratorio</option>
                      <option value="Otro">Otro</option>
                    </select>
                  </div>
                  <Show when={formDestino() === 'Otro'}>
                    <div class="field">
                      <label>Especifique destino</label>
                      <input
                        type="text"
                        placeholder="Ej: Oficina central"
                        value={formDestinoOtro()}
                        onInput={(e) => setFormDestinoOtro(e.currentTarget.value)}
                      />
                    </div>
                  </Show>
                </Show>

                {/* Signo (solo Ajuste) */}
                <Show when={formTipo() === 'Ajuste'}>
                  <div class="field">
                    <label>Signo del Ajuste</label>
                    <select
                      value={formSigno()}
                      onChange={(e) => setFormSigno(e.currentTarget.value as SignoEgresoInterno)}
                    >
                      <option value="Negativo">Negativo (resta stock)</option>
                      <option value="Positivo">Positivo (suma stock)</option>
                    </select>
                  </div>
                </Show>

                {/* Producto */}
                <div class="field">
                  <label>Producto</label>
                  <select
                    value={formProductoId()}
                    onChange={(e) => handleProductoChange(e.currentTarget.value)}
                  >
                    <option value="">— Seleccione producto —</option>
                    <For each={almacenStore.productosCache()}>
                      {(p) => <option value={p.id}>{p.nombreVisible}</option>}
                    </For>
                  </select>
                </div>

                {/* Lote */}
                <div class="field">
                  <label>
                    Lote
                    {formTipo() === 'Ajuste' && formSigno() === 'Positivo' ? (
                      <span style={{ color: 'var(--rust)', 'margin-left': '4px' }}>*</span>
                    ) : (
                      <span style={{ color: 'var(--ink-soft)', 'font-size': '11px', 'margin-left': '4px' }}>
                        (opcional)
                      </span>
                    )}
                  </label>
                  <select
                    value={formLoteId()}
                    onChange={(e) => setFormLoteId(e.currentTarget.value)}
                    disabled={!formProductoId() || loadingLotes()}
                  >
                    <option value="">
                      {loadingLotes()
                        ? 'Cargando lotes...'
                        : formTipo() === 'Ajuste' && formSigno() === 'Positivo'
                        ? '— Seleccionar lote (obligatorio) —'
                        : '— Seleccionar lote (opcional) —'}
                    </option>
                    <For each={lotesProducto()}>
                      {(lote) => (
                        <option value={lote.id}>
                          {lote.codigo} — {lote.procedencia || 'sin procedencia'} ({lote.cantidadActual} {lote.unidad})
                        </option>
                      )}
                    </For>
                  </select>
                </div>

                {/* Cantidad + Unidad */}
                <div style={{ display: 'grid', 'grid-template-columns': '1fr 120px', gap: '10px' }}>
                  <div class="field">
                    <label>Cantidad</label>
                    <input
                      type="number"
                      step="0.01"
                      min="0.01"
                      placeholder="0.00"
                      value={formCantidad()}
                      onInput={(e) => setFormCantidad(e.currentTarget.value)}
                    />
                  </div>
                  <div class="field">
                    <label>Unidad</label>
                    <select value={formUnidad()} onChange={(e) => setFormUnidad(e.currentTarget.value)}>
                      <For each={opcionesUnidad()}>
                        {(u) => <option value={u.value}>{u.label}</option>}
                      </For>
                    </select>
                  </div>
                </div>

                {/* Motivo */}
                <div class="field">
                  <label>Motivo (mín. 5 caracteres)</label>
                  <input
                    type="text"
                    placeholder="Describa el motivo de la solicitud..."
                    value={formMotivo()}
                    onInput={(e) => setFormMotivo(e.currentTarget.value)}
                  />
                </div>
              </div>

              <div class="modal-footer" style={{ 'margin-top': '20px', display: 'flex', 'justify-content': 'flex-end', gap: '10px' }}>
                <button type="button" class="btn btn-secondary" onClick={intentarCerrarFormNueva}>Cancelar</button>
                <button type="submit" class="btn btn-primary">Crear Solicitud</button>
              </div>
            </form>
          </div>
        </div>
      </Show>

      {/* ═══ MODAL: Detalle ═══ */}
      <Show when={modalDetalleOpen() && solicitudSeleccionada()}>
        <div class="modal-backdrop" onClick={() => setModalDetalleOpen(false)}>
          <div class="modal card" style={{ 'max-width': '600px', width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <div class="modal-header" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-bottom': '1px solid var(--border-color)', 'padding-bottom': '12px' }}>
              <div>
                <h3 style={{ margin: '0', 'font-size': '18px', color: 'var(--ink)' }}>
                  Detalle de Solicitud
                </h3>
                <span class={`px-2 py-0.5 text-xs rounded-full font-medium ${estadoBadgeClass(solicitudSeleccionada()!.estado)}`}>
                  {solicitudSeleccionada()!.estado}
                </span>
              </div>
              <button class="btn btn-ghost" onClick={() => setModalDetalleOpen(false)}>✕</button>
            </div>

            <div class="modal-body" style={{ 'margin-top': '16px', display: 'flex', 'flex-direction': 'column', gap: '16px' }}>
              <div style={{ display: 'grid', 'grid-template-columns': 'repeat(2, 1fr)', gap: '12px', background: 'var(--surface-alt)', padding: '12px', 'border-radius': '6px' }}>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Tipo</span>
                  <strong style={{ 'font-size': '14px' }}>
                    <span class={tipoPillClass(solicitudSeleccionada()!.tipo)}>
                      {tipoLabel(solicitudSeleccionada()!.tipo)}
                    </span>
                  </strong>
                </div>
                <Show when={solicitudSeleccionada()!.destino}>
                  <div>
                    <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Destino</span>
                    <strong style={{ 'font-size': '14px' }}>{solicitudSeleccionada()!.destino}</strong>
                  </div>
                </Show>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Signo</span>
                  <strong style={{ 'font-size': '14px' }}>{solicitudSeleccionada()!.signo}</strong>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Producto</span>
                  <strong style={{ 'font-size': '14px' }}>{solicitudSeleccionada()!.nombreProducto || '—'}</strong>
                </div>
                <Show when={solicitudSeleccionada()!.codigoLote}>
                  <div>
                    <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Lote</span>
                    <span style={{ 'font-size': '13px', 'font-family': 'monospace' }}>{solicitudSeleccionada()!.codigoLote}</span>
                  </div>
                </Show>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Cantidad</span>
                  <strong style={{ 'font-size': '14px', 'font-family': 'monospace' }}>
                    {solicitudSeleccionada()!.cantidad} {unidadAbrev(solicitudSeleccionada()!.unidad)}
                  </strong>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Creado</span>
                  <span style={{ 'font-size': '13px', 'font-family': 'monospace' }}>{solicitudSeleccionada()!.creadoEn.substring(0, 10)}</span>
                </div>
                <div>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Solicitante</span>
                  <span style={{ 'font-size': '13px', 'font-family': 'monospace' }}>{solicitudSeleccionada()!.solicitanteId}</span>
                </div>
              </div>

              <div style={{ background: 'var(--surface-alt)', padding: '12px', 'border-radius': '6px' }}>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block' }}>Motivo</span>
                <p style={{ margin: '4px 0 0', 'font-size': '13px' }}>{solicitudSeleccionada()!.motivo}</p>
              </div>

              {/* Resolución (si aplica) */}
              <Show when={solicitudSeleccionada()!.estado === 'Aprobada' || solicitudSeleccionada()!.estado === 'Rechazada'}>
                <div style={{ background: 'var(--surface-alt)', padding: '12px', 'border-radius': '6px' }}>
                  <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', display: 'block', 'margin-bottom': '4px' }}>Resolución</span>
                  <div style={{ display: 'grid', 'grid-template-columns': 'repeat(2, 1fr)', gap: '8px', 'font-size': '13px' }}>
                    <div>
                      <span style={{ color: 'var(--ink-soft)' }}>Aprobador: </span>
                      <span style={{ 'font-family': 'monospace', 'font-size': '11px' }}>{solicitudSeleccionada()!.aprobadorId || '—'}</span>
                    </div>
                    <div>
                      <span style={{ color: 'var(--ink-soft)' }}>Fecha: </span>
                      <span>{solicitudSeleccionada()!.fechaResolucion?.substring(0, 10) || '—'}</span>
                    </div>
                  </div>
                  <Show when={solicitudSeleccionada()!.motivoResolucion}>
                    <p style={{ margin: '8px 0 0', 'font-size': '13px', color: 'var(--rust)' }}>
                      Motivo: {solicitudSeleccionada()!.motivoResolucion}
                    </p>
                  </Show>
                </div>
              </Show>

              {/* Entrega (si confirmada) */}
              <Show when={solicitudSeleccionada()!.estado === 'Confirmada'}>
                <div style={{ background: 'var(--green-tint)', padding: '12px', 'border-radius': '6px' }}>
                  <span style={{ 'font-size': '11px', color: 'var(--green-deep)', display: 'block', 'margin-bottom': '4px' }}>Entrega</span>
                  <div style={{ display: 'grid', 'grid-template-columns': 'repeat(2, 1fr)', gap: '8px', 'font-size': '13px' }}>
                    <div>
                      <span style={{ color: 'var(--ink-soft)' }}>Entregado por: </span>
                      <span style={{ 'font-family': 'monospace', 'font-size': '11px' }}>{solicitudSeleccionada()!.entregadoPor || '—'}</span>
                    </div>
                    <div>
                      <span style={{ color: 'var(--ink-soft)' }}>Fecha: </span>
                      <span>{solicitudSeleccionada()!.fechaEntrega?.substring(0, 10) || '—'}</span>
                    </div>
                  </div>
                </div>
              </Show>
            </div>

            <div class="modal-footer" style={{ 'margin-top': '20px', display: 'flex', 'justify-content': 'flex-end' }}>
              <button class="btn btn-secondary" onClick={() => setModalDetalleOpen(false)}>Cerrar</button>
            </div>
          </div>
        </div>
      </Show>

      {/* ═══ MODAL: Rechazo ═══ */}
      <Show when={modalRechazoOpen() && solicitudSeleccionada()}>
        <div class="modal-backdrop">
          <div class="modal card" style={{ 'max-width': '480px', width: '90%' }} onClick={(e) => e.stopPropagation()}>
            <div class="modal-header" style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'border-bottom': '1px solid var(--border-color)', 'padding-bottom': '12px' }}>
              <h3 style={{ margin: '0', 'font-size': '18px', color: 'var(--rust)' }}>Rechazar Solicitud</h3>
              <button type="button" class="btn btn-ghost" disabled={confirmandoAccion()} onClick={cerrarRechazo}>✕</button>
            </div>

            <div class="modal-body" style={{ 'margin-top': '16px', display: 'flex', 'flex-direction': 'column', gap: '12px' }}>
              <p style={{ 'font-size': '13px', color: 'var(--ink-soft)', margin: '0' }}>
                Indique el motivo por el cual se rechaza la solicitud de <strong>{tipoLabel(solicitudSeleccionada()!.tipo)}</strong>.
              </p>

              <Show when={rechazoError()}>
                <div style={{ padding: '8px 12px', 'border-radius': '6px', background: 'var(--rust-tint)', color: 'var(--rust)', 'font-size': '13px' }}>
                  {rechazoError()}
                </div>
              </Show>

              <div class="field">
                <label>Motivo de Rechazo (mín. 5 caracteres)</label>
                <textarea
                  style={{ width: '100%', 'min-height': '80px', 'font-family': 'var(--font-body)', 'font-size': '13.5px', padding: '8px 10px', border: '1px solid var(--border)', 'border-radius': 'var(--radius-s)', background: 'var(--surface-alt)', color: 'var(--ink)', resize: 'vertical' }}
                  placeholder="Describa el motivo del rechazo..."
                  value={motivoRechazo()}
                  onInput={(e) => setMotivoRechazo(e.currentTarget.value)}
                />
              </div>
            </div>

            <div class="modal-footer" style={{ 'margin-top': '20px', display: 'flex', 'justify-content': 'flex-end', gap: '10px' }}>
              <button type="button" class="btn btn-secondary" disabled={confirmandoAccion()} onClick={cerrarRechazo}>Cancelar</button>
              <button class="btn btn-primary" style={{ background: 'var(--rust)' }} disabled={confirmandoAccion()} onClick={handleRechazar}>
                {confirmandoAccion() ? 'Procesando...' : 'Confirmar Rechazo'}
              </button>
            </div>
          </div>
        </div>
      </Show>
    </section>
  );
};
