import { createSignal, Show } from 'solid-js';
import { loteStore } from '../store/loteStore';
import { ModalBase } from '../primitives/ModalBase';

export function LoteBlockModal() {
  const [motivo, setMotivo] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal(false);

  const handleClose = () => {
    setMotivo('');
    setError(null);
    loteStore.setBlockModalOpen(false);
  };

  const handleConfirmClose = (): boolean => {
    if (motivo().trim() !== '') {
      const ok = window.confirm('¿Descartar el motivo ingresado y cerrar?');
      if (ok) {
        setMotivo('');
        setError(null);
      }
      return ok;
    }
    setError(null);
    return true;
  };

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
      setMotivo('');
    } catch (err: any) {
      setError(err.message || 'Error al bloquear el lote');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ModalBase
      open={Boolean(loteStore.blockModalOpen() && loteStore.selectedLote())}
      onClose={handleClose}
      confirmClose={handleConfirmClose}
      title="Bloquear Lote por Alerta o Calidad"
      size="sm"
    >
      <p style={{ 'font-size': '12px', color: 'var(--ink-soft)', margin: '0 0 14px' }}>
        Lote <strong>{loteStore.selectedLote()?.codigo}</strong> ({loteStore.selectedLote()?.nombreProducto})
      </p>

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
            onClick={() => {
              if (handleConfirmClose()) {
                handleClose();
              }
            }}
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
    </ModalBase>
  );
}
