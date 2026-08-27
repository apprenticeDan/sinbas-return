import { createSignal, Show, For, onMount } from 'solid-js';
import { loteStore } from '../store/loteStore';
import { catalogStore } from '../store/catalogStore';

export function LoteFormModal() {
  const todayStr = new Date().toISOString().split('T')[0];

  const [productoId, setProductoId] = createSignal('');
  const [procedencia, setProcedencia] = createSignal('');
  const [cantidad, setCantidad] = createSignal(10);
  const [unidad, setUnidad] = createSignal('Kilogramo');
  const [fechaIngreso, setFechaIngreso] = createSignal(todayStr);
  const [ubicacion, setUbicacion] = createSignal('');
  const [codigoPersonalizado, setCodigoPersonalizado] = createSignal('');
  const [observaciones, setObservaciones] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  onMount(() => {
    if (catalogStore.products().length === 0) {
      catalogStore.loadCatalog();
    }
  });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    if (!productoId()) {
      setError('Seleccione un producto del catálogo');
      return;
    }
    if (cantidad() <= 0) {
      setError('La cantidad inicial debe ser mayor a 0');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await loteStore.crearLote({
        productoId: productoId(),
        procedencia: procedencia(),
        cantidad: Number(cantidad()),
        unidad: unidad(),
        fechaIngreso: fechaIngreso(),
        ubicacion: ubicacion(),
        codigoPersonalizado: codigoPersonalizado(),
        observaciones: observaciones(),
      });
    } catch (err: any) {
      setError(err.message || 'Error al registrar el lote');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Show when={loteStore.createModalOpen()}>
      <div
        style={{
          position: 'fixed',
          top: 0,
          left: 0,
          right: 0,
          bottom: 0,
          background: 'rgba(15, 23, 18, 0.45)',
          'backdrop-filter': 'blur(4px)',
          display: 'flex',
          'align-items': 'center',
          'justify-content': 'center',
          'z-index': 1000,
          padding: '20px',
        }}
      >
        <div
          class="card animate-fade-in"
          style={{
            width: '100%',
            'max-width': '580px',
            background: 'var(--surface)',
            padding: '28px',
            'border-radius': 'var(--radius-l)',
            'box-shadow': '0 20px 40px rgba(0, 0, 0, 0.15)',
          }}
        >
          <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'margin-bottom': '20px' }}>
            <div>
              <h2 style={{ 'font-family': 'var(--font-display)', 'font-size': '20px', margin: 0, color: 'var(--ink)' }}>
                Registrar Ingreso de Lote
              </h2>
              <p style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', margin: '4px 0 0' }}>
                Ingreso físico de semillas o plantines al inventario con trazabilidad por procedencia.
              </p>
            </div>
            <button
              onClick={() => loteStore.setCreateModalOpen(false)}
              class="btn btn-ghost"
              style={{ padding: '6px 12px', 'font-size': '12px' }}
            >
              ✕
            </button>
          </div>

          <Show when={error()}>
            <div class="alert-error" style={{ 'margin-bottom': '16px' }}>
              {error()}
            </div>
          </Show>

          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '16px' }}>
            <div class="field">
              <label>Producto del Catálogo *</label>
              <select
                value={productoId()}
                onChange={(e) => setProductoId(e.currentTarget.value)}
                required
              >
                <option value="">-- Seleccionar Especie / Producto --</option>
                <For each={catalogStore.products()}>
                  {(p) => (
                    <option value={p.id}>
                      {p.nombreVisible} ({p.categoria} - {p.trazabilidad})
                    </option>
                  )}
                </For>
              </select>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '14px' }}>
              <div class="field">
                <label>Procedencia / Origen</label>
                <input
                  type="text"
                  placeholder="Ej. Bosque Chiquitano, Vivero X..."
                  value={procedencia()}
                  onInput={(e) => setProcedencia(e.currentTarget.value)}
                />
              </div>

              <div class="field">
                <label>Fecha de Ingreso *</label>
                <input
                  type="date"
                  value={fechaIngreso()}
                  onInput={(e) => setFechaIngreso(e.currentTarget.value)}
                  required
                />
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '14px' }}>
              <div class="field">
                <label>Cantidad Inicial *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  value={cantidad()}
                  onInput={(e) => setCantidad(parseFloat(e.currentTarget.value) || 0)}
                  required
                />
              </div>

              <div class="field">
                <label>Unidad de Medida *</label>
                <select
                  value={unidad()}
                  onChange={(e) => setUnidad(e.currentTarget.value)}
                  required
                >
                  <option value="Kilogramo">Kilogramo (kg)</option>
                  <option value="Gramo">Gramo (g)</option>
                  <option value="Unidad_">Unidad (u)</option>
                </select>
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '14px' }}>
              <div class="field">
                <label>Ubicación en Almacén / Vivero</label>
                <input
                  type="text"
                  placeholder="Ej. Almacén Central - Estante A1..."
                  value={ubicacion()}
                  onInput={(e) => setUbicacion(e.currentTarget.value)}
                />
              </div>

              <div class="field">
                <label>Código Lote (Opcional)</label>
                <input
                  type="text"
                  placeholder="Dejar vacío para autogenerar"
                  value={codigoPersonalizado()}
                  onInput={(e) => setCodigoPersonalizado(e.currentTarget.value)}
                />
              </div>
            </div>

            <div class="field">
              <label>Observaciones o Alertas de Ingreso</label>
              <textarea
                rows={2}
                placeholder="Detalles sobre recolección, calidad percibida o notas..."
                value={observaciones()}
                onInput={(e) => setObservaciones(e.currentTarget.value)}
              />
            </div>

            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '8px' }}>
              <button
                type="button"
                onClick={() => loteStore.setCreateModalOpen(false)}
                class="btn btn-ghost"
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn btn-primary"
                disabled={submitting()}
              >
                {submitting() ? 'Guardando...' : 'Registrar Ingreso de Lote'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
