import { createSignal, Show } from 'solid-js';
import { catalogStore } from '../store/catalogStore';

export function AssignPriceModal() {
  const [monto, setMonto] = createSignal<number>(0);
  const [moneda, setMoneda] = createSignal<string>('BOB');
  const [modalError, setModalError] = createSignal<string | null>(null);

  const product = () => catalogStore.selectedProductForPrice();

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setModalError(null);

    const prod = product();
    if (!prod) return;

    if (monto() <= 0) {
      setModalError('El precio oficial debe ser mayor a cero (0).');
      return;
    }

    const success = await catalogStore.assignPrice({
      productoId: prod.id,
      monto: monto(),
      moneda: moneda(),
    });

    if (!success) {
      setModalError(catalogStore.error() || 'Ocurrió un error al asignar el precio.');
    }
  };

  return (
    <Show when={catalogStore.priceModalOpen() && product() !== null}>
      <div class="modal-overlay">
        <div class="modal-card" style={{ 'max-width': '440px' }}>
          {/* Header */}
          <div class="modal-header">
            <h2 class="modal-title">Gobernanza de Precio Oficial</h2>
            <button
              onClick={() => catalogStore.setPriceModalOpen(false)}
              class="btn btn-ghost"
              style={{ padding: '4px 8px', 'font-size': '16px' }}
            >
              ✕
            </button>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
            <Show when={modalError()}>
              <div class="alert-error">
                {modalError()}
              </div>
            </Show>

            <div style={{ padding: '12px', background: 'var(--surface-alt)', border: '1px solid var(--border)', 'border-radius': 'var(--radius-s)' }}>
              <div style={{ 'font-size': '10.5px', 'text-transform': 'uppercase', color: 'var(--ink-soft)', 'font-weight': '600' }}>Producto Seleccionado</div>
              <div style={{ 'font-weight': '700', color: 'var(--green-deep)', 'font-size': '16px', 'margin-top': '2px' }}>{product()?.nombreVisible}</div>
              <div style={{ 'font-size': '12px', color: 'var(--ink-soft)', 'margin-top': '4px' }}>
                Categoría: <strong>{product()?.categoria}</strong> | Unidad: <strong>{product()?.unidadManejo}</strong>
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '2fr 1fr', gap: '10px' }}>
              <div class="field">
                <label>Precio Oficial *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  required
                  placeholder="0.00"
                  value={monto() || ''}
                  onInput={(e) => setMonto(parseFloat(e.currentTarget.value) || 0)}
                  style={{ 'font-family': 'monospace', 'font-size': '16px', 'font-weight': '700' }}
                />
              </div>

              <div class="field">
                <label>Moneda</label>
                <select
                  value={moneda()}
                  onChange={(e) => setMoneda(e.currentTarget.value)}
                >
                  <option value="BOB">BOB (Bs.)</option>
                  <option value="USD">USD ($)</option>
                </select>
              </div>
            </div>

            <div class="pill pill-amber" style={{ padding: '8px 12px', 'border-radius': 'var(--radius-s)', 'font-size': '11.5px', 'line-height': '1.4' }}>
              ⚠️ Al fijar el precio oficial, el producto cambiará automáticamente a <strong>Activo para Venta</strong>.
            </div>

            {/* Actions */}
            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '8px', 'margin-top': '8px' }}>
              <button
                type="button"
                onClick={() => catalogStore.setPriceModalOpen(false)}
                class="btn btn-ghost"
              >
                Cancelar
              </button>
              <button
                type="submit"
                disabled={catalogStore.loading()}
                class="btn btn-primary"
              >
                <Show when={catalogStore.loading()}>
                  <span>↻</span>
                </Show>
                Aprobar y Activar Venta
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
