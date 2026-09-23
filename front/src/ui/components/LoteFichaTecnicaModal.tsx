import { createSignal, createEffect, Show, For } from 'solid-js';
import { ApiLoteGateway, FichaTecnicaLoteDto } from '../../infrastructure/api/ApiLoteGateway';
import { ApiLabGateway } from '../../infrastructure/api/ApiLabGateway';

interface LoteFichaTecnicaModalProps {
  open: boolean;
  loteId: string | null;
  onClose: () => void;
}

export function LoteFichaTecnicaModal(props: LoteFichaTecnicaModalProps) {
  const [ficha, setFicha] = createSignal<FichaTecnicaLoteDto | null>(null);
  const [loading, setLoading] = createSignal<boolean>(false);
  const [downloadingPdf, setDownloadingPdf] = createSignal<boolean>(false);
  const [error, setError] = createSignal<string | null>(null);

  createEffect(() => {
    if (props.open && props.loteId) {
      cargarFicha(props.loteId);
    } else {
      setFicha(null);
      setError(null);
    }
  });

  const cargarFicha = async (id: string) => {
    setLoading(true);
    setError(null);
    try {
      const data = await ApiLoteGateway.obtenerFichaTecnica(id);
      setFicha(data);
    } catch (err: any) {
      setError(err.message || 'Error al cargar la ficha técnica del lote');
    } finally {
      setLoading(false);
    }
  };

  const handleDescargarEtiqueta = async () => {
    const f = ficha();
    if (!f) return;
    setDownloadingPdf(true);
    try {
      await ApiLabGateway.descargarEtiquetaPdf(f.id, f.codigo);
    } catch (err: any) {
      setError(err.message || 'Error al descargar la etiqueta oficial PDF');
    } finally {
      setDownloadingPdf(false);
    }
  };

  return (
    <Show when={props.open}>
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
            'max-width': '740px',
            'max-height': '90vh',
            'overflow-y': 'auto',
            background: 'var(--surface)',
            padding: '28px',
            'border-radius': 'var(--radius-l)',
            'box-shadow': '0 20px 40px rgba(0, 0, 0, 0.15)',
          }}
        >
          {/* Header */}
          <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'flex-start', 'margin-bottom': '18px' }}>
            <div>
              <div style={{ display: 'flex', 'align-items': 'center', gap: '8px' }}>
                <span style={{ 'font-size': '20px' }}>📄</span>
                <h2 style={{ 'font-family': 'var(--font-display)', 'font-size': '20px', margin: 0, color: 'var(--ink)' }}>
                  Ficha Técnica del Lote
                </h2>
                <Show when={ficha()}>
                  <span class="pill pill-green" style={{ 'font-family': 'monospace', 'font-size': '13px' }}>
                    {ficha()?.codigo}
                  </span>
                </Show>
              </div>
              <p style={{ 'font-size': '12.5px', color: 'var(--ink-soft)', margin: '4px 0 0' }}>
                Historial técnico, calidad y trazabilidad botánica (RF14 / CU-14 / F-LAB-06).
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

          <Show when={loading()}>
            <div style={{ padding: '40px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              Cargando ficha técnica...
            </div>
          </Show>

          <Show when={error()}>
            <div class="alert-error" style={{ 'margin-bottom': '16px' }}>
              {error()}
            </div>
          </Show>

          <Show when={!loading() && ficha()}>
            {/* Datos Generales y Botánicos */}
            <div
              style={{
                display: 'grid',
                'grid-template-columns': 'repeat(auto-fit, minmax(200px, 1fr))',
                gap: '12px',
                background: 'var(--surface-sunken)',
                padding: '16px',
                'border-radius': 'var(--radius-m)',
                'margin-bottom': '20px',
              }}
            >
              <div>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'font-weight': '600' }}>
                  Especie / Producto
                </span>
                <div style={{ 'font-weight': '600', color: 'var(--ink)', 'margin-top': '2px' }}>
                  {ficha()?.nombreProducto}
                </div>
                <Show when={ficha()?.genero && ficha()?.epiteto}>
                  <div style={{ 'font-size': '11.5px', color: 'var(--ink-soft)', 'font-style': 'italic' }}>
                    {ficha()?.genero} {ficha()?.epiteto}
                  </div>
                </Show>
              </div>

              <div>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'font-weight': '600' }}>
                  Procedencia / Rodal
                </span>
                <div style={{ 'font-size': '13px', color: 'var(--ink)', 'margin-top': '2px' }}>
                  📍 {ficha()?.procedencia || 'No especificada'}
                </div>
              </div>

              <div>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'font-weight': '600' }}>
                  Stock y Almacén
                </span>
                <div style={{ 'font-size': '13px', color: 'var(--ink)', 'margin-top': '2px' }}>
                  <strong>{ficha()?.cantidadActual}</strong> {ficha()?.unidad} (Inicial: {ficha()?.cantidadInicial} {ficha()?.unidad})
                </div>
                <Show when={ficha()?.ubicacion}>
                  <div style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>
                    Sector: {ficha()?.ubicacion}
                  </div>
                </Show>
              </div>

              <div>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'text-transform': 'uppercase', 'font-weight': '600' }}>
                  Estado y Dictamen
                </span>
                <div style={{ 'margin-top': '4px', display: 'flex', gap: '6px', 'align-items': 'center' }}>
                  <span class={ficha()?.estado === 'Activo' ? 'pill pill-green' : 'pill pill-rust'}>
                    ● {ficha()?.estado}
                  </span>
                  <Show when={ficha()?.ultimoDictamen}>
                    <span class={ficha()?.ultimoDictamen === 'Aprobado' ? 'pill pill-green' : 'pill pill-rust'}>
                      Dictamen: {ficha()?.ultimoDictamen}
                    </span>
                  </Show>
                </div>
              </div>
            </div>

            {/* Historial de Análisis de Laboratorio */}
            <div style={{ 'margin-bottom': '20px' }}>
              <div style={{ display: 'flex', 'justify-content': 'space-between', 'align-items': 'center', 'margin-bottom': '10px' }}>
                <h3 style={{ 'font-size': '15px', 'font-weight': '600', color: 'var(--ink)', margin: 0 }}>
                  🔬 Historial de Análisis Físicos de Laboratorio (RN12)
                </h3>
                <Show when={ficha()?.ultimoDictamen === 'Aprobado'}>
                  <button
                    type="button"
                    onClick={handleDescargarEtiqueta}
                    disabled={downloadingPdf()}
                    class="btn btn-ghost"
                    style={{ 'font-size': '12px', padding: '4px 10px' }}
                  >
                    {downloadingPdf() ? 'Generando PDF...' : '🏷️ Descargar Etiqueta Oficial PDF'}
                  </button>
                </Show>
              </div>

              <Show
                when={ficha()!.historialAnalisis.length > 0}
                fallback={
                  <div style={{ padding: '24px', 'text-align': 'center', background: 'var(--surface-sunken)', 'border-radius': 'var(--radius-m)', color: 'var(--ink-soft)', 'font-size': '13px' }}>
                    Este lote aún no cuenta con análisis de laboratorio registrados.
                  </div>
                }
              >
                <div style={{ 'overflow-x': 'auto', border: '1px solid var(--border)', 'border-radius': 'var(--radius-m)' }}>
                  <table style={{ width: '100%', 'border-collapse': 'collapse', 'font-size': '12.5px' }}>
                    <thead>
                      <tr style={{ background: 'var(--surface-sunken)', 'text-align': 'left', 'border-bottom': '1px solid var(--border)' }}>
                        <th style={{ padding: '8px 12px' }}>Fecha</th>
                        <th style={{ padding: '8px 12px' }}>Germinación</th>
                        <th style={{ padding: '8px 12px' }}>Pureza</th>
                        <th style={{ padding: '8px 12px' }}>Humedad</th>
                        <th style={{ padding: '8px 12px' }}>Viabilidad</th>
                        <th style={{ padding: '8px 12px' }}>Dictamen</th>
                        <th style={{ padding: '8px 12px' }}>Observaciones</th>
                      </tr>
                    </thead>
                    <tbody>
                      <For each={ficha()!.historialAnalisis}>
                        {(analisis) => (
                          <tr style={{ 'border-bottom': '1px solid var(--border)' }}>
                            <td style={{ padding: '8px 12px', 'font-family': 'monospace' }}>
                              {analisis.fechaAnalisis}
                            </td>
                            <td style={{ padding: '8px 12px', 'font-weight': '600' }}>
                              {analisis.germinacion}%
                            </td>
                            <td style={{ padding: '8px 12px' }}>{analisis.pureza}%</td>
                            <td style={{ padding: '8px 12px' }}>{analisis.humedad}%</td>
                            <td style={{ padding: '8px 12px' }}>{analisis.viabilidad}%</td>
                            <td style={{ padding: '8px 12px' }}>
                              <span class={analisis.dictamen === 'Aprobado' ? 'pill pill-green' : 'pill pill-rust'}>
                                {analisis.dictamen}
                              </span>
                            </td>
                            <td style={{ padding: '8px 12px', color: 'var(--ink-soft)' }}>
                              {analisis.observaciones || '-'}
                            </td>
                          </tr>
                        )}
                      </For>
                    </tbody>
                  </table>
                </div>
              </Show>
            </div>

            {/* Modal Actions */}
            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px' }}>
              <button
                type="button"
                onClick={props.onClose}
                class="btn btn-primary"
              >
                Cerrar
              </button>
            </div>
          </Show>
        </div>
      </div>
    </Show>
  );
}
