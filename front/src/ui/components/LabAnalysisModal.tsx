import { createSignal, createMemo, Show } from 'solid-js';
import { LoteItem } from '../../domain/models/Lote';
import { ApiLabGateway, RegistrarAnalisisPayload } from '../../infrastructure/api/ApiLabGateway';

interface LabAnalysisModalProps {
  open: boolean;
  lote: LoteItem | null;
  onClose: () => void;
  onSuccess: () => void;
}

export function LabAnalysisModal(props: LabAnalysisModalProps) {
  const todayStr = new Date().toISOString().split('T')[0];

  const [germinacion, setGerminacion] = createSignal<number>(85);
  const [pureza, setPureza] = createSignal<number>(95);
  const [humedad, setHumedad] = createSignal<number>(8);
  const [viabilidad, setViabilidad] = createSignal<number>(90);
  const [semillasPurasKg, setSemillasPurasKg] = createSignal<number>(15000);
  const [semillasImpurezasKg, setSemillasImpurezasKg] = createSignal<number>(500);
  const [dictamenManual, setDictamenManual] = createSignal<string>('Auto');
  const [fechaAnalisis, setFechaAnalisis] = createSignal<string>(todayStr);
  const [observaciones, setObservaciones] = createSignal<string>('');
  const [error, setError] = createSignal<string | null>(null);
  const [submitting, setSubmitting] = createSignal<boolean>(false);

  // Sugerencia automática según RN12 / F-LAB-05
  const dictamenSugerido = createMemo(() => {
    const g = germinacion();
    const p = pureza();
    const h = humedad();
    if (g >= 70 && p >= 85 && h <= 12) {
      return 'Aprobado';
    }
    return 'Rechazado';
  });

  const dictamenFinal = createMemo(() => {
    if (dictamenManual() === 'Auto') return dictamenSugerido();
    return dictamenManual();
  });

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    if (!props.lote) return;

    if (germinacion() < 0 || germinacion() > 100) {
      setError('La germinación debe estar entre 0 y 100%');
      return;
    }
    if (pureza() < 0 || pureza() > 100) {
      setError('La pureza debe estar entre 0 y 100%');
      return;
    }
    if (humedad() < 0 || humedad() > 100) {
      setError('La humedad debe estar entre 0 y 100%');
      return;
    }
    if (viabilidad() < 0 || viabilidad() > 100) {
      setError('La viabilidad debe estar entre 0 y 100%');
      return;
    }

    setSubmitting(true);
    setError(null);

    const payload: RegistrarAnalisisPayload = {
      germinacion: Number(germinacion()),
      pureza: Number(pureza()),
      humedad: Number(humedad()),
      viabilidad: Number(viabilidad()),
      semillasPurasKg: Number(semillasPurasKg()),
      semillasImpurezasKg: Number(semillasImpurezasKg()),
      dictamenManual: dictamenFinal(),
      fechaAnalisis: fechaAnalisis(),
      observaciones: observaciones(),
    };

    try {
      await ApiLabGateway.registrarAnalisis(props.lote.id, payload);
      props.onSuccess();
      props.onClose();
    } catch (err: any) {
      setError(err.message || 'Error al registrar el análisis de laboratorio');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <Show when={props.open && props.lote}>
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
            'max-width': '620px',
            'max-height': '90vh',
            'overflow-y': 'auto',
            background: 'var(--surface)',
            padding: '28px',
            'border-radius': 'var(--radius-l)',
            'box-shadow': '0 20px 40px rgba(0, 0, 0, 0.15)',
          }}
        >
          <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'margin-bottom': '16px' }}>
            <div>
              <h2 style={{ 'font-family': 'var(--font-display)', 'font-size': '20px', margin: 0, color: 'var(--ink)' }}>
                Registrar Análisis de Calidad (Lab)
              </h2>
              <p style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', margin: '4px 0 0' }}>
                Lote: <strong style={{ color: 'var(--green-deep)' }}>{props.lote?.codigo}</strong> ({props.lote?.nombreProducto})
              </p>
            </div>
            <button
              onClick={props.onClose}
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

          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
              <div class="field">
                <label>Germinación (%) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  value={germinacion()}
                  onInput={(e) => setGerminacion(Number(e.currentTarget.value))}
                  required
                />
              </div>

              <div class="field">
                <label>Pureza Física (%) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  value={pureza()}
                  onInput={(e) => setPureza(Number(e.currentTarget.value))}
                  required
                />
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
              <div class="field">
                <label>Contenido de Humedad (%) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  value={humedad()}
                  onInput={(e) => setHumedad(Number(e.currentTarget.value))}
                  required
                />
              </div>

              <div class="field">
                <label>Viabilidad Tetrazolio (%) *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  max="100"
                  value={viabilidad()}
                  onInput={(e) => setViabilidad(Number(e.currentTarget.value))}
                  required
                />
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
              <div class="field">
                <label>Semillas Puras (estimado/kg)</label>
                <input
                  type="number"
                  min="0"
                  value={semillasPurasKg()}
                  onInput={(e) => setSemillasPurasKg(Number(e.currentTarget.value))}
                />
              </div>

              <div class="field">
                <label>Impurezas (g/kg)</label>
                <input
                  type="number"
                  min="0"
                  value={semillasImpurezasKg()}
                  onInput={(e) => setSemillasImpurezasKg(Number(e.currentTarget.value))}
                />
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '12px' }}>
              <div class="field">
                <label>Fecha de Ensayos *</label>
                <input
                  type="date"
                  value={fechaAnalisis()}
                  onInput={(e) => setFechaAnalisis(e.currentTarget.value)}
                  required
                />
              </div>

              <div class="field">
                <label>Dictamen Oficial</label>
                <select
                  value={dictamenManual()}
                  onChange={(e) => setDictamenManual(e.currentTarget.value)}
                >
                  <option value="Auto">Sugerido Automático ({dictamenSugerido()})</option>
                  <option value="Aprobado">Aprobado (Apto para Comercialización)</option>
                  <option value="Rechazado">Rechazado (No Comercializable)</option>
                </select>
              </div>
            </div>

            {/* Dictamen Preview Badge */}
            <div
              style={{
                padding: '10px 14px',
                'border-radius': 'var(--radius-m)',
                background: dictamenFinal() === 'Aprobado' ? 'rgba(46, 125, 50, 0.1)' : 'rgba(198, 40, 40, 0.1)',
                border: dictamenFinal() === 'Aprobado' ? '1px solid var(--green-leaf)' : '1px solid var(--rust)',
                display: 'flex',
                'align-items': 'center',
                'justify-content': 'space-between',
              }}
            >
              <div>
                <span style={{ 'font-size': '12px', color: 'var(--ink-soft)' }}>Resultado Dictamen: </span>
                <strong style={{ color: dictamenFinal() === 'Aprobado' ? 'var(--green-deep)' : 'var(--rust)', 'font-size': '13.5px' }}>
                  {dictamenFinal()}
                </strong>
              </div>
              <span style={{ 'font-size': '11.5px', color: 'var(--ink-soft)' }}>
                RN12: Germinación ≥ 70%, Pureza ≥ 85%
              </span>
            </div>

            <div class="field">
              <label>Observaciones del Especialista</label>
              <textarea
                rows={2}
                placeholder="Detalles del ensayo, método tetrazolio, vigor vegetativo..."
                value={observaciones()}
                onInput={(e) => setObservaciones(e.currentTarget.value)}
              />
            </div>

            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '10px' }}>
              <button
                type="button"
                onClick={props.onClose}
                class="btn btn-ghost"
                disabled={submitting()}
              >
                Cancelar
              </button>
              <button
                type="submit"
                class="btn btn-primary"
                disabled={submitting()}
              >
                {submitting() ? 'Guardando...' : 'Certificar Dictamen'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
