/**
 * Vista de Elaboración y Gestión de Proformas / Cotizaciones.
 *
 * Feature: F7 (RF-07 | CU-10 | RN04 / RN05 / RN13 / RN16)
 * - Validación de stock disponible en tiempo real (RN04)
 * - Leyenda legal obligatoria de no reserva física (RN05)
 * - Congelamiento de precios oficiales del catálogo (RN13)
 * - Solo productos activos y con precio oficial (RN16)
 * - Soporte para clientes registrados o venta mostrador/anónimo
 * - Vista de impresión con @media print
 */

import { Component, createSignal, For, Show, createMemo, onMount } from 'solid-js';
import { proformaStore } from '../store/proformaStore';
import type { ProformaDto, CrearProformaRequest, LineaCotizadaRequest, StockDisponibleDto } from '../../infrastructure/api/ApiProformaGateway';
import type { Product } from '../../domain/models/Product';

interface LineaForm {
  id: string; // id temporal para key
  categoria: string;
  productoId: string;
  cantidad: number;
  unidad: string;
  precioUnitario: number;
  stockDisp: number;
  stockLoading: boolean;
}

export const ProformasView: Component = () => {
  // ─── Modal States ─────────────────────────────────────────────
  const [modalNuevoOpen, setModalNuevoOpen] = createSignal(false);
  const [modalDetalleOpen, setModalDetalleOpen] = createSignal(false);
  const [modalAnularOpen, setModalAnularOpen] = createSignal(false);
  const [proformaSeleccionada, setProformaSeleccionada] = createSignal<ProformaDto | null>(null);

  // ─── Form State para Nueva Proforma ───────────────────────────
  const [clienteTipo, setClienteTipo] = createSignal<'registrado' | 'libre'>('registrado');
  const [selectedClienteId, setSelectedClienteId] = createSignal('');
  const [clienteNombreLibre, setClienteNombreLibre] = createSignal('');
  const [fechaVencimiento, setFechaVencimiento] = createSignal('');
  const [observaciones, setObservaciones] = createSignal('');
  const [formError, setFormError] = createSignal<string | null>(null);
  const [formSubmitting, setFormSubmitting] = createSignal(false);

  // Líneas de la cotización
  const [lineas, setLineas] = createSignal<LineaForm[]>([]);

  // ─── Anulación State ──────────────────────────────────────────
  const [motivoAnulacion, setMotivoAnulacion] = createSignal('');
  const [anularError, setAnularError] = createSignal<string | null>(null);
  const [anularSubmitting, setAnularSubmitting] = createSignal(false);

  onMount(async () => {
    await Promise.all([
      proformaStore.cargarProformas(),
      proformaStore.cargarAuxiliares(),
    ]);
  });

  const abrirModalNuevo = () => {
    setClienteTipo('registrado');
    setSelectedClienteId('');
    setClienteNombreLibre('');
    setObservaciones('');
    setFormError(null);

    // Fecha de vencimiento por defecto: hoy + 7 días
    const hoy = new Date();
    hoy.setDate(hoy.getDate() + 7);
    setFechaVencimiento(hoy.toISOString().split('T')[0]);

    // Iniciar con 1 línea vacía
    setLineas([crearLineaVacia()]);
    setModalNuevoOpen(true);
  };

  const crearLineaVacia = (): LineaForm => ({
    id: Math.random().toString(36).substring(2, 9),
    categoria: '',
    productoId: '',
    cantidad: 1,
    unidad: 'kg',
    precioUnitario: 0,
    stockDisp: 0,
    stockLoading: false,
  });

  const agregarLinea = () => {
    setLineas((prev) => [...prev, crearLineaVacia()]);
  };

  const eliminarLinea = (id: string) => {
    if (lineas().length <= 1) {
      setFormError('La proforma debe contener al menos un producto.');
      return;
    }
    setLineas((prev) => prev.filter((l) => l.id !== id));
  };

  const actualizarLinea = (id: string, updates: Partial<LineaForm>) => {
    setLineas((prev) =>
      prev.map((l) => (l.id === id ? { ...l, ...updates } : l))
    );
  };

  const onProductoChange = async (lineaId: string, productoId: string) => {
    const prod = proformaStore.productosCache().find((p) => p.id === productoId);
    if (!prod) {
      actualizarLinea(lineaId, {
        productoId: '',
        precioUnitario: 0,
        unidad: 'kg',
        stockDisp: 0,
        stockLoading: false,
      });
      return;
    }

    const unidad = prod.unidadManejo || 'kg';
    const precio = prod.precioOficial || 0;

    actualizarLinea(lineaId, {
      productoId,
      precioUnitario: precio,
      unidad,
      stockLoading: true,
    });

    const stockInfo = await proformaStore.consultarStock(productoId);
    actualizarLinea(lineaId, {
      stockDisp: stockInfo ? stockInfo.stockDisponible : 0,
      stockLoading: false,
    });
  };

  // Cálculo de total dinámico
  const totalCotizacion = createMemo(() => {
    return lineas().reduce((sum, l) => sum + (l.cantidad * l.precioUnitario), 0);
  });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setFormError(null);

    // Validar líneas
    const currentLineas = lineas();
    if (currentLineas.length === 0) {
      setFormError('Debe agregar al menos un producto a la cotización.');
      return;
    }

    // Validar productos seleccionados
    const prodIds = new Set<string>();
    for (const l of currentLineas) {
      if (!l.productoId) {
        setFormError('Debe seleccionar un producto en todas las filas agregadas.');
        return;
      }
      if (prodIds.has(l.productoId)) {
        setFormError('No puede incluir el mismo producto más de una vez. Ajuste la cantidad en una sola fila.');
        return;
      }
      prodIds.add(l.productoId);

      if (l.cantidad <= 0) {
        setFormError('La cantidad debe ser mayor a cero en todas las líneas.');
        return;
      }

      // Validación de stock en frontend (RN04)
      if (l.cantidad > l.stockDisp) {
        setFormError(`Stock insuficiente para uno de los productos seleccionados (disponible: ${l.stockDisp}, solicitado: ${l.cantidad}).`);
        return;
      }
    }

    // Validar cliente
    let reqClienteId: string | undefined = undefined;
    let reqClienteNombre: string | undefined = undefined;

    if (clienteTipo() === 'registrado') {
      if (!selectedClienteId()) {
        setFormError('Por favor seleccione un cliente registrado o elija la opción de venta libre.');
        return;
      }
      reqClienteId = selectedClienteId();
    } else {
      if (!clienteNombreLibre().trim()) {
        setFormError('Por favor ingrese el nombre o razón social del cliente.');
        return;
      }
      reqClienteNombre = clienteNombreLibre().trim();
    }

    const payload: CrearProformaRequest = {
      clienteId: reqClienteId,
      clienteNombreLibre: reqClienteNombre,
      fechaVencimiento: fechaVencimiento() || undefined,
      observaciones: observaciones().trim() || undefined,
      lineas: currentLineas.map((l) => ({
        productoId: l.productoId,
        cantidad: Number(l.cantidad),
        unidad: l.unidad,
      })),
    };

    setFormSubmitting(true);
    const res = await proformaStore.crearProforma(payload);
    setFormSubmitting(false);

    if (!res.ok) {
      setFormError(res.error || 'Error al elaborar la proforma');
      return;
    }

    setModalNuevoOpen(false);
    if (res.data) {
      setProformaSeleccionada(res.data);
      setModalDetalleOpen(true);
    }
  };

  const handleAnular = async (e: Event) => {
    e.preventDefault();
    setAnularError(null);
    const p = proformaSeleccionada();
    if (!p) return;

    if (!motivoAnulacion().trim()) {
      setAnularError('El motivo de anulación es obligatorio.');
      return;
    }

    setAnularSubmitting(true);
    const res = await proformaStore.anularProforma(p.id, motivoAnulacion().trim());
    setAnularSubmitting(false);

    if (!res.ok) {
      setAnularError(res.error || 'Error al anular la proforma');
      return;
    }

    setModalAnularOpen(false);
    setProformaSeleccionada(res.data || null);
  };

  const badgeEstadoClass = (estado: string) => {
    switch (estado) {
      case 'Vigente':
        return 'badge-success';
      case 'Vencida':
        return 'badge-warning';
      case 'Convertida':
        return 'badge-info';
      case 'Anulada':
        return 'badge-danger';
      default:
        return 'badge-secondary';
    }
  };

  return (
    <div class="view-container">
      {/* ─── Encabezado Principal ──────────────────────────────── */}
      <div class="view-header flex justify-between items-center mb-6">
        <div>
          <h1 class="text-2xl font-bold text-gray-800">Proformas & Cotizaciones</h1>
          <p class="text-sm text-gray-500">
            Módulo Comercial (Feature F7): Elaboración de cotizaciones oficiales con validación de stock y precios congelados.
          </p>
        </div>
        <button class="btn btn-primary flex items-center gap-2" onClick={abrirModalNuevo}>
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-4 h-4">
            <path d="M12 5v14M5 12h14" />
          </svg>
          Nueva Proforma
        </button>
      </div>

      {/* ─── Barra de Filtros ─────────────────────────────────── */}
      <div class="card mb-6 p-4 bg-white shadow-sm rounded-lg border border-gray-100 flex flex-wrap gap-4 items-center justify-between">
        <div class="flex flex-wrap gap-3 items-center flex-1">
          <input
            type="text"
            class="input-field text-sm"
            placeholder="Buscar por cliente, ID u observaciones..."
            value={proformaStore.filtroSearch()}
            onInput={(e) => proformaStore.setFiltroSearch(e.currentTarget.value)}
            style={{ 'min-width': '260px' }}
          />

          <select
            class="input-field text-sm"
            value={proformaStore.filtroEstado()}
            onChange={(e) => proformaStore.setFiltroEstado(e.currentTarget.value)}
          >
            <option value="">Todos los Estados</option>
            <option value="Vigente">Vigente</option>
            <option value="Vencida">Vencida</option>
            <option value="Convertida">Convertida</option>
            <option value="Anulada">Anulada</option>
          </select>
        </div>

        <div class="text-xs text-gray-500">
          Total encontradas: <strong>{proformaStore.filteredProformas().length}</strong>
        </div>
      </div>

      {/* ─── Tabla de Proformas ───────────────────────────────── */}
      <div class="card bg-white shadow-sm rounded-lg overflow-hidden border border-gray-100">
        <Show when={proformaStore.loadingProformas()}>
          <div class="p-8 text-center text-gray-400">Cargando proformas...</div>
        </Show>

        <Show when={!proformaStore.loadingProformas() && proformaStore.errorProformas()}>
          <div class="p-6 text-center text-red-500 bg-red-50">
            {proformaStore.errorProformas()}
          </div>
        </Show>

        <Show when={!proformaStore.loadingProformas() && !proformaStore.errorProformas()}>
          <Show
            when={proformaStore.filteredProformas().length > 0}
            fallback={
              <div class="p-12 text-center text-gray-400">
                No se encontraron proformas registradas.
              </div>
            }
          >
            <div class="overflow-x-auto">
              <table class="w-full text-left border-collapse">
                <thead>
                  <tr class="border-b bg-gray-50 text-xs font-semibold text-gray-600 uppercase tracking-wider">
                    <th class="p-3">ID / Código</th>
                    <th class="p-3">Fecha</th>
                    <th class="p-3">Cliente</th>
                    <th class="p-3">Vencimiento</th>
                    <th class="p-3">Estado</th>
                    <th class="p-3 text-right">Total (BOB)</th>
                    <th class="p-3 text-center">Acciones</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-100 text-sm">
                  <For each={proformaStore.filteredProformas()}>
                    {(p) => (
                      <tr class="hover:bg-gray-50 transition-colors">
                        <td class="p-3 font-mono text-xs font-semibold text-gray-700">
                          {p.id.substring(0, 8)}...
                        </td>
                        <td class="p-3 text-gray-600">{p.fecha}</td>
                        <td class="p-3 font-medium text-gray-800">{p.clienteNombre}</td>
                        <td class="p-3 text-gray-600">
                          {p.fechaVencimiento || 'Sin fecha'}
                        </td>
                        <td class="p-3">
                          <span class={`px-2 py-0.5 text-xs rounded-full font-medium ${badgeEstadoClass(p.estadoProyectado || p.estado)}`}>
                            {p.estadoProyectado || p.estado}
                          </span>
                        </td>
                        <td class="p-3 text-right font-bold text-gray-800">
                          {p.total.toFixed(2)}
                        </td>
                        <td class="p-3 text-center">
                          <div class="flex items-center justify-center gap-2">
                            <button
                              class="btn btn-sm btn-outline text-xs"
                              onClick={() => {
                                setProformaSeleccionada(p);
                                setModalDetalleOpen(true);
                              }}
                              title="Ver detalle e imprimir"
                            >
                              Ver / Imprimir
                            </button>
                            <Show when={(p.estadoProyectado || p.estado) === 'Vigente'}>
                              <button
                                class="btn btn-sm text-xs bg-red-50 text-red-600 hover:bg-red-100 border border-red-200"
                                onClick={() => {
                                  setProformaSeleccionada(p);
                                  setMotivoAnulacion('');
                                  setAnularError(null);
                                  setModalAnularOpen(true);
                                }}
                                title="Anular proforma"
                              >
                                Anular
                              </button>
                            </Show>
                          </div>
                        </td>
                      </tr>
                    )}
                  </For>
                </tbody>
              </table>
            </div>
          </Show>
        </Show>
      </div>

      {/* ─── Modal: Nueva Proforma / Editor de Líneas ─────────── */}
      <Show when={modalNuevoOpen()}>
        <div class="modal-overlay fixed inset-0 bg-black/40 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div class="modal-content bg-white rounded-xl shadow-xl max-w-4xl w-full max-h-[90vh] flex flex-col overflow-hidden">
            {/* Modal Header */}
            <div class="p-5 border-b flex justify-between items-center bg-gray-50">
              <div>
                <h2 class="text-lg font-bold text-gray-800">Elaborar Nueva Proforma de Venta</h2>
                <p class="text-xs text-gray-500">
                  Precios oficiales congelados del catálogo. Validación de stock en tiempo real.
                </p>
              </div>
              <button
                class="text-gray-400 hover:text-gray-600 text-xl font-bold p-1"
                onClick={() => setModalNuevoOpen(false)}
              >
                ✕
              </button>
            </div>

            {/* Modal Body */}
            <form onSubmit={handleSubmit} class="p-6 overflow-y-auto flex-1 flex flex-col gap-5">
              <Show when={formError()}>
                <div class="p-3 bg-red-50 text-red-700 text-xs rounded-lg border border-red-200">
                  {formError()}
                </div>
              </Show>

              {/* Sección Datos del Cliente */}
              <div class="bg-gray-50 p-4 rounded-lg border border-gray-200">
                <div class="text-xs font-bold text-gray-700 uppercase tracking-wide mb-3">
                  Información del Cliente
                </div>
                <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div>
                    <label class="block text-xs font-semibold text-gray-600 mb-1">Tipo de Cliente</label>
                    <div class="flex gap-2">
                      <button
                        type="button"
                        class={`btn btn-sm flex-1 text-xs ${clienteTipo() === 'registrado' ? 'btn-primary' : 'btn-outline'}`}
                        onClick={() => setClienteTipo('registrado')}
                      >
                        Registrado (F6)
                      </button>
                      <button
                        type="button"
                        class={`btn btn-sm flex-1 text-xs ${clienteTipo() === 'libre' ? 'btn-primary' : 'btn-outline'}`}
                        onClick={() => setClienteTipo('libre')}
                      >
                        Mostrador / Libre
                      </button>
                    </div>
                  </div>

                  <Show
                    when={clienteTipo() === 'registrado'}
                    fallback={
                      <div class="md:col-span-2">
                        <label class="block text-xs font-semibold text-gray-600 mb-1">Nombre / Razón Social *</label>
                        <input
                          type="text"
                          class="input-field text-sm w-full"
                          placeholder="Ej. Juan Pérez o Vivero del Norte"
                          value={clienteNombreLibre()}
                          onInput={(e) => setClienteNombreLibre(e.currentTarget.value)}
                        />
                      </div>
                    }
                  >
                    <div class="md:col-span-2">
                      <label class="block text-xs font-semibold text-gray-600 mb-1">Seleccionar Cliente *</label>
                      <select
                        class="input-field text-sm w-full"
                        value={selectedClienteId()}
                        onChange={(e) => setSelectedClienteId(e.currentTarget.value)}
                      >
                        <option value="">-- Seleccione un cliente --</option>
                        <For each={proformaStore.clientesCache()}>
                          {(c) => (
                            <option value={c.id}>
                              {c.nombreVisible} {c.nit ? `(NIT: ${c.nit})` : ''}
                            </option>
                          )}
                        </For>
                      </select>
                    </div>
                  </Show>
                </div>

                <div class="grid grid-cols-1 md:grid-cols-2 gap-4 mt-3">
                  <div>
                    <label class="block text-xs font-semibold text-gray-600 mb-1">Fecha de Vencimiento (Opcional)</label>
                    <input
                      type="date"
                      class="input-field text-sm w-full"
                      value={fechaVencimiento()}
                      onInput={(e) => setFechaVencimiento(e.currentTarget.value)}
                    />
                  </div>
                  <div>
                    <label class="block text-xs font-semibold text-gray-600 mb-1">Observaciones</label>
                    <input
                      type="text"
                      class="input-field text-sm w-full"
                      placeholder="Ej. Destino reforestación, entrega en vivero..."
                      value={observaciones()}
                      onInput={(e) => setObservaciones(e.currentTarget.value)}
                    />
                  </div>
                </div>
              </div>

              {/* Sección Editor de Líneas de Cotización */}
              <div>
                <div class="flex justify-between items-center mb-2">
                  <div class="text-xs font-bold text-gray-700 uppercase tracking-wide">
                    Líneas Cotizadas
                  </div>
                  <button
                    type="button"
                    class="btn btn-sm btn-outline text-xs flex items-center gap-1"
                    onClick={agregarLinea}
                  >
                    + Agregar Producto
                  </button>
                </div>

                <div class="border rounded-lg overflow-hidden border-gray-200">
                  <table class="w-full text-left border-collapse">
                    <thead>
                      <tr class="bg-gray-100 text-xs font-semibold text-gray-600">
                        <th class="p-2 w-28">Categoría</th>
                        <th class="p-2">Producto Oficial (F1)</th>
                        <th class="p-2 w-24 text-center">Stock Disp.</th>
                        <th class="p-2 w-28 text-right">Precio Of.</th>
                        <th class="p-2 w-24">Cant.</th>
                        <th class="p-2 w-16">Unidad</th>
                        <th class="p-2 w-28 text-right">Subtotal</th>
                        <th class="p-2 w-10 text-center"></th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-100 text-sm">
                      <For each={lineas()}>
                        {(linea) => {
                          const prodsFiltrados = createMemo(() => {
                            const cat = linea.categoria;
                            const all = proformaStore.productosCache();
                            if (!cat) return all;
                            return all.filter((p) => p.categoria === cat);
                          });

                          const subtotal = () => (linea.cantidad * linea.precioUnitario).toFixed(2);
                          const sinStock = () => linea.productoId && linea.cantidad > linea.stockDisp;

                          return (
                            <tr class={`hover:bg-gray-50 transition-colors ${sinStock() ? 'bg-red-50/50' : ''}`}>
                              {/* Categoría */}
                              <td class="p-2">
                                <select
                                  class="input-field text-xs w-full p-1"
                                  value={linea.categoria}
                                  onChange={(e) => {
                                    actualizarLinea(linea.id, { categoria: e.currentTarget.value, productoId: '', precioUnitario: 0, stockDisp: 0 });
                                  }}
                                >
                                  <option value="">Todas</option>
                                  <option value="Semilla">Semilla</option>
                                  <option value="Plantin">Plantín</option>
                                  <option value="Insumo">Insumo</option>
                                </select>
                              </td>

                              {/* Producto */}
                              <td class="p-2">
                                <select
                                  class="input-field text-xs w-full p-1 font-medium"
                                  value={linea.productoId}
                                  onChange={(e) => onProductoChange(linea.id, e.currentTarget.value)}
                                >
                                  <option value="">-- Seleccionar Producto --</option>
                                  <For each={prodsFiltrados()}>
                                    {(p) => (
                                      <option value={p.id}>
                                        {p.nombreVisible} ({p.precioOficial} BOB/{p.unidadManejo})
                                      </option>
                                    )}
                                  </For>
                                </select>
                              </td>

                              {/* Stock Disponible */}
                              <td class="p-2 text-center text-xs">
                                <Show when={linea.stockLoading} fallback={
                                  <Show when={linea.productoId} fallback={<span class="text-gray-400">-</span>}>
                                    <span class={`px-1.5 py-0.5 rounded text-xs font-bold ${sinStock() ? 'bg-red-100 text-red-700' : 'bg-green-100 text-green-800'}`}>
                                      {linea.stockDisp} {linea.unidad}
                                    </span>
                                  </Show>
                                }>
                                  <span class="text-xs text-gray-400">...</span>
                                </Show>
                              </td>

                              {/* Precio Oficial Congelado (RN13) */}
                              <td class="p-2 text-right font-mono text-xs text-gray-700">
                                {linea.precioUnitario > 0 ? `${linea.precioUnitario.toFixed(2)}` : '-'}
                              </td>

                              {/* Cantidad */}
                              <td class="p-2">
                                <input
                                  type="number"
                                  step="0.01"
                                  min="0.01"
                                  class="input-field text-xs w-full p-1 text-right"
                                  value={linea.cantidad}
                                  onInput={(e) => actualizarLinea(linea.id, { cantidad: parseFloat(e.currentTarget.value) || 0 })}
                                />
                              </td>

                              {/* Unidad */}
                              <td class="p-2 text-xs text-gray-500">
                                {linea.unidad}
                              </td>

                              {/* Subtotal */}
                              <td class="p-2 text-right font-mono font-bold text-xs text-gray-800">
                                {subtotal()} BOB
                              </td>

                              {/* Botón Eliminar */}
                              <td class="p-2 text-center">
                                <button
                                  type="button"
                                  class="text-gray-400 hover:text-red-600 font-bold text-sm"
                                  onClick={() => eliminarLinea(linea.id)}
                                  title="Eliminar línea"
                                >
                                  ✕
                                </button>
                              </td>
                            </tr>
                          );
                        }}
                      </For>
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Resumen Total y Leyenda Legal */}
              <div class="bg-amber-50/70 border border-amber-200 rounded-lg p-4 flex flex-col md:flex-row justify-between items-center gap-3">
                <div class="text-xs text-amber-900 font-medium flex items-center gap-2">
                  <span class="text-base">⚠️</span>
                  <span><strong>Aviso Legal (RN05):</strong> Disponibilidad sujeta a cambios - La proforma no reserva stock físico.</span>
                </div>
                <div class="text-right">
                  <div class="text-xs text-gray-500 uppercase tracking-wide">Total Cotizado</div>
                  <div class="text-2xl font-black text-gray-900 font-mono">
                    {totalCotizacion().toFixed(2)} <span class="text-sm font-bold text-gray-600">BOB</span>
                  </div>
                </div>
              </div>

              {/* Botones de Acción */}
              <div class="flex justify-end gap-3 pt-3 border-t">
                <button
                  type="button"
                  class="btn btn-outline text-sm"
                  onClick={() => setModalNuevoOpen(false)}
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  class="btn btn-primary text-sm"
                  disabled={formSubmitting()}
                >
                  {formSubmitting() ? 'Generando Proforma...' : 'Guardar y Emitir Proforma'}
                </button>
              </div>
            </form>
          </div>
        </div>
      </Show>

      {/* ─── Modal: Detalle de Proforma & Impresión ───────────── */}
      <Show when={modalDetalleOpen() && proformaSeleccionada()}>
        {(() => {
          const p = proformaSeleccionada()!;
          return (
            <div class="modal-overlay fixed inset-0 bg-black/40 backdrop-blur-sm flex items-center justify-center z-50 p-4">
              <div class="modal-content bg-white rounded-xl shadow-xl max-w-3xl w-full max-h-[90vh] flex flex-col overflow-hidden">
                {/* Header modal (oculto al imprimir) */}
                <div class="p-4 border-b flex justify-between items-center bg-gray-50 print:hidden">
                  <h3 class="font-bold text-gray-800">Documento de Cotización / Proforma</h3>
                  <div class="flex items-center gap-2">
                    <button
                      class="btn btn-sm btn-primary flex items-center gap-1.5"
                      onClick={() => window.print()}
                    >
                      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="w-4 h-4">
                        <polyline points="6 9 6 2 18 2 18 9" />
                        <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
                        <rect x="6" y="14" width="12" height="8" />
                      </svg>
                      Imprimir
                    </button>
                    <button
                      class="text-gray-400 hover:text-gray-600 text-xl font-bold p-1"
                      onClick={() => setModalDetalleOpen(false)}
                    >
                      ✕
                    </button>
                  </div>
                </div>

                {/* Hoja Imprimible */}
                <div class="p-8 overflow-y-auto flex-1 print:p-0 print:m-0" id="proforma-print-sheet">
                  {/* Encabezado de la Proforma */}
                  <div class="flex justify-between items-start border-b pb-4 mb-6">
                    <div>
                      <h2 class="text-xl font-black text-gray-900 tracking-tight">PROFORMA DE VENTA</h2>
                      <div class="text-xs text-gray-500 font-mono mt-0.5">Nº {p.id}</div>
                      <div class="text-xs font-semibold text-emerald-800 mt-1">SINBAS — Banco de Semillas Forestales</div>
                    </div>
                    <div class="text-right">
                      <div class="text-xs text-gray-500">Fecha de Emisión: <strong>{p.fecha}</strong></div>
                      <div class="text-xs text-gray-500 mt-0.5">
                        Válido hasta: <strong>{p.fechaVencimiento || 'Sin fecha de vencimiento'}</strong>
                      </div>
                      <div class="mt-2">
                        <span class={`px-2.5 py-0.5 text-xs rounded-full font-bold ${badgeEstadoClass(p.estadoProyectado || p.estado)}`}>
                          ESTADO: {(p.estadoProyectado || p.estado).toUpperCase()}
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Datos del Cliente */}
                  <div class="bg-gray-50 rounded-lg p-4 mb-6 border border-gray-100">
                    <div class="text-xs font-bold text-gray-500 uppercase tracking-wider mb-1">Cliente / Destinatario</div>
                    <div class="text-base font-bold text-gray-800">{p.clienteNombre}</div>
                    <Show when={p.observaciones}>
                      <div class="text-xs text-gray-600 mt-1"><strong>Observaciones:</strong> {p.observaciones}</div>
                    </Show>
                    <Show when={p.anulacionMotivo}>
                      <div class="text-xs text-red-600 font-medium mt-1">
                        <strong>Motivo de Anulación:</strong> {p.anulacionMotivo}
                      </div>
                    </Show>
                  </div>

                  {/* Tabla de Productos Cotizados */}
                  <div class="mb-6">
                    <table class="w-full text-left border-collapse">
                      <thead>
                        <tr class="border-b-2 border-gray-200 text-xs font-bold text-gray-600 uppercase">
                          <th class="py-2">Item</th>
                          <th class="py-2">Descripción del Producto</th>
                          <th class="py-2 text-right">Cantidad</th>
                          <th class="py-2 text-center">Unidad</th>
                          <th class="py-2 text-right">Precio Unit. ({p.moneda})</th>
                          <th class="py-2 text-right">Subtotal ({p.moneda})</th>
                        </tr>
                      </thead>
                      <tbody class="divide-y divide-gray-100 text-sm">
                        <For each={p.lineas}>
                          {(l, idx) => (
                            <tr>
                              <td class="py-2.5 text-xs text-gray-400 font-mono">{idx() + 1}</td>
                              <td class="py-2.5 font-medium text-gray-800">{l.nombreProducto}</td>
                              <td class="py-2.5 text-right font-mono">{l.cantidad}</td>
                              <td class="py-2.5 text-center text-xs text-gray-500">{l.unidad}</td>
                              <td class="py-2.5 text-right font-mono">{l.precioUnitario.toFixed(2)}</td>
                              <td class="py-2.5 text-right font-mono font-bold text-gray-800">{l.subtotal.toFixed(2)}</td>
                            </tr>
                          )}
                        </For>
                      </tbody>
                    </table>
                  </div>

                  {/* Total y Leyenda */}
                  <div class="border-t-2 border-gray-200 pt-4 flex flex-col md:flex-row justify-between items-start gap-4">
                    <div class="text-xs text-gray-500 max-w-sm">
                      <div class="font-bold text-gray-700 mb-1">CONDICIONES GENERALES</div>
                      <div>{p.leyenda}</div>
                    </div>
                    <div class="text-right w-full md:w-auto">
                      <div class="text-xs text-gray-500 uppercase tracking-wider font-semibold">Total a Pagar</div>
                      <div class="text-2xl font-black text-gray-900 font-mono">
                        {p.total.toFixed(2)} <span class="text-sm font-bold text-gray-600">{p.moneda}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          );
        })()}
      </Show>

      {/* ─── Modal: Anular Proforma ──────────────────────────── */}
      <Show when={modalAnularOpen() && proformaSeleccionada()}>
        <div class="modal-overlay fixed inset-0 bg-black/40 backdrop-blur-sm flex items-center justify-center z-50 p-4">
          <div class="modal-content bg-white rounded-xl shadow-xl max-w-md w-full p-6">
            <h3 class="text-lg font-bold text-gray-900 mb-2">Anular Proforma de Venta</h3>
            <p class="text-xs text-gray-500 mb-4">
              Esta acción marcará la proforma como <strong>Anulada</strong> y registrará el motivo oficial en la auditoría.
            </p>

            <Show when={anularError()}>
              <div class="p-3 bg-red-50 text-red-700 text-xs rounded-lg border border-red-200 mb-3">
                {anularError()}
              </div>
            </Show>

            <form onSubmit={handleAnular} class="flex flex-col gap-4">
              <div>
                <label class="block text-xs font-semibold text-gray-700 mb-1">Motivo de Anulación *</label>
                <textarea
                  class="input-field text-sm w-full p-2 h-24"
                  placeholder="Ej. El cliente desistió de la compra por limitaciones de presupuesto..."
                  value={motivoAnulacion()}
                  onInput={(e) => setMotivoAnulacion(e.currentTarget.value)}
                  required
                />
              </div>

              <div class="flex justify-end gap-2 pt-2 border-t">
                <button
                  type="button"
                  class="btn btn-outline text-xs"
                  onClick={() => setModalAnularOpen(false)}
                >
                  Cancelar
                </button>
                <button
                  type="submit"
                  class="btn text-xs bg-red-600 hover:bg-red-700 text-white font-semibold"
                  disabled={anularSubmitting()}
                >
                  {anularSubmitting() ? 'Anulando...' : 'Confirmar Anulación'}
                </button>
              </div>
            </form>
          </div>
        </div>
      </Show>
    </div>
  );
};
