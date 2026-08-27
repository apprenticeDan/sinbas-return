import { createSignal, Show } from 'solid-js';
import { loteStore } from '../store/loteStore';

export function LoteBlockModal() {
  const [motivo, setMotivo] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    const lote = loteStore.selectedLote();
    if (!lote) return;

    if (!motivo().trim()) {
      setError('Especifique el motivo de bloqueo o alerta sobre el lote');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await loteStore.bloquearLote(lote.id, { motivo: motivo().trim() });
    } catch (err: any) {
      setError(err.message || 'Error al bloquear el lote');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Show when={loteStore.blockModalOpen() && loteStore.selectedLote()}>
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
            'max-width': '460px',
            background: 'var(--surface)',
            padding: '24px',
            'border-radius': 'var(--radius-l)',
            'box-shadow': '0 20px 40px rgba(0, 0, 0, 0.15)',
          }}
        >
          <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'margin-bottom': '16px' }}>
            <div>
              <h2 style={{ 'font-family': 'var(--font-display)', 'font-size': '18px', margin: 0, color: 'var(--rust)' }}>
                Bloquear Lote por Alerta o Calidad
              </h2>
              <p style={{ 'font-size': '12px', color: 'var(--ink-soft)', margin: '4px 0 0' }}>
                Lote <strong>{loteStore.selectedLote()?.codigo}</strong> ({loteStore.selectedLote()?.nombreProducto})
              </p>
            </div>
            <button
              onClick={() => loteStore.setBlockModalOpen(false)}
              class="btn btn-ghost"
              style={{ padding: '4px 8px', 'font-size': '12px' }}
            >
              ✕
            </button>
          </div>

          <Show when={error()}>
            <div class="alert-error" style={{ 'margin-bottom': '14px' }}>
              {error()}
            </div>
          </Show>

          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
            <div class="field">
              <label>Motivo de Bloqueo / Observación *</label>
              <textarea
                rows={3}
                placeholder="Ej. Alerta de germinación < 40%, presencia de hongos o reporte de almacén..."
                value={motivo()}
                onInput={(e) => setMotivo(e.currentTarget.value)}
                required
              />
            </div>

            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '6px' }}>
              <button
                type="button"
                onClick={() => loteStore.setBlockModalOpen(false)}
                class="btn btn-ghost"
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn btn-danger"
                disabled={submitting()}
              >
                {submitting() ? 'Procesando...' : 'Confirmar Bloqueo'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
