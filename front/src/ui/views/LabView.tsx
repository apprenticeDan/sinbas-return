import { createSignal, onMount, createMemo, Show, For } from 'solid-js';
import { LoteItem } from '../../domain/models/Lote';
import { ApiLoteGateway } from '../../infrastructure/api/ApiLoteGateway';
import { ApiLabGateway } from '../../infrastructure/api/ApiLabGateway';
import { LabAnalysisModal } from '../components/LabAnalysisModal';
import { LoteFichaTecnicaModal } from '../components/LoteFichaTecnicaModal';
import { DataTable, Column } from '../components/DataTable';
import { authStore } from '../store/authStore';

export function LabView() {
  const [lotes, setLotes] = createSignal<LoteItem[]>([]);
  const [loading, setLoading] = createSignal<boolean>(false);
  const [searchTerm, setSearchTerm] = createSignal<string>('');
  const [selectedLote, setSelectedLote] = createSignal<LoteItem | null>(null);
  const [analysisModalOpen, setAnalysisModalOpen] = createSignal<boolean>(false);
  const [fichaModalOpen, setFichaModalOpen] = createSignal<boolean>(false);
  const [selectedFichaLoteId, setSelectedFichaLoteId] = createSignal<string | null>(null);
  const [downloadingId, setDownloadingId] = createSignal<string | null>(null);
  const [notification, setNotification] = createSignal<{ type: 'success' | 'error'; message: string } | null>(null);


  const canEditLab = createMemo(() => authStore.hasAnyRole(['Administrador', 'Laboratorio']));

  const loadData = async () => {
    setLoading(true);
    try {
      const data = await ApiLoteGateway.listarLotes();
      setLotes(data);
    } catch (err: any) {
      setNotification({ type: 'error', message: err.message || 'Error al cargar lotes' });
    } finally {
      setLoading(false);
    }
  };

  onMount(() => {
    loadData();
  });

  const filteredLotes = createMemo(() => {
    const q = searchTerm().toLowerCase().trim();
    if (!q) return lotes();
    return lotes().filter(
      (l) =>
        l.codigo.toLowerCase().includes(q) ||
        l.nombreProducto.toLowerCase().includes(q) ||
        (l.procedencia && l.procedencia.toLowerCase().includes(q))
    );
  });

  const handleDownloadPdf = async (lote: LoteItem) => {
    setDownloadingId(lote.id);
    try {
      await ApiLabGateway.descargarEtiquetaPdf(lote.id, lote.codigo);
      setNotification({ type: 'success', message: `Etiqueta oficial del lote ${lote.codigo} descargada correctamente` });
    } catch (err: any) {
      setNotification({ type: 'error', message: err.message || 'No se pudo descargar la etiqueta. Verifique que el lote tenga análisis registrado.' });
    } finally {
      setDownloadingId(null);
    }
  };

  const columns: Column<LoteItem>[] = [
    {
      header: 'Código Lote',
      cell: (l) => (
        <div>
          <span style={{ 'font-family': 'monospace', 'font-size': '13.5px', 'font-weight': '700', color: 'var(--green-deep)' }}>
            {l.codigo}
          </span>
          <Show when={l.procedencia}>
            <div style={{ 'font-size': '11.5px', color: 'var(--ink-soft)', 'margin-top': '2px' }}>
              📍 {l.procedencia}
            </div>
          </Show>
        </div>
      ),
    },
    {
      header: 'Especie / Producto',
      cell: (l) => (
        <div>
          <div style={{ 'font-weight': '600', color: 'var(--ink)' }}>{l.nombreProducto}</div>
          <div style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'margin-top': '2px' }}>
            Ingreso: {l.fechaIngreso}
          </div>
        </div>
      ),
    },
    {
      header: 'Stock Actual',
      cell: (l) => (
        <span style={{ 'font-family': 'monospace', 'font-weight': '600', color: 'var(--ink)' }}>
          {l.cantidadActual} {l.unidad}
        </span>
      ),
    },
    {
      header: 'Estado Lote',
      cell: (l) => {
        if (l.estado === 'Activo') {
          return <span class="pill pill-green">● Aprobado / Activo</span>;
        } else if (l.estado === 'Bloqueado') {
          return (
            <span class="pill pill-rust" title={l.observaciones || 'Lote rechazado o bajo observación'}>
              ● Rechazado / Bloqueado
            </span>
          );
        } else if (l.estado === 'Agotado') {
          return <span class="pill pill-amber">● Agotado</span>;
        }
        return <span class="pill">● {l.estado}</span>;
      },
    },
    {
      header: 'Acciones de Laboratorio',
      align: 'right',
      cell: (l) => (
        <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '8px' }}>
          <Show when={canEditLab()}>
            <button
              onClick={() => {
                setSelectedLote(l);
                setAnalysisModalOpen(true);
              }}
              class="btn btn-secondary"
              style={{ padding: '5px 10px', 'font-size': '12px' }}
              title="Registrar ensayo de germinación, pureza y viabilidad"
            >
              🧪 Ensayar Calidad
            </button>
          </Show>
          <button
            onClick={() => {
              setSelectedFichaLoteId(l.id);
              setFichaModalOpen(true);
            }}
            class="btn btn-ghost"
            style={{ padding: '5px 10px', 'font-size': '12px' }}
            title="Consultar ficha técnica consolidada e historial de análisis (RF14 / CU-14)"
          >
            📄 Ficha
          </button>
          <button
            onClick={() => handleDownloadPdf(l)}
            class="btn btn-ghost"
            style={{ padding: '5px 10px', 'font-size': '12px', color: 'var(--green-deep)' }}
            disabled={downloadingId() === l.id}
            title="Descargar etiqueta oficial de trazabilidad en formato PDF (RF07 / RN01)"
          >
            {downloadingId() === l.id ? 'Descargando...' : '🏷️ Etiqueta PDF'}
          </button>
        </div>
      ),
    },
  ];


  return (
    <section class="panel">
      {/* Panel Header */}
      <div class="panel-head">
        <p class="panel-eyebrow">Control de Calidad y Ensayos (F3)</p>
        <h1 class="panel-title">Laboratorio de Semillas & Etiquetado</h1>
        <p class="panel-desc">
          Evaluación de germinación, pureza física, viabilidad tetrazolio y emisión de dictamen técnico con etiqueta oficial.
        </p>
      </div>

      {/* Notifications */}
      <Show when={notification()}>
        <div
          class={notification()?.type === 'success' ? 'alert-success' : 'alert-error'}
          style={{ 'margin-bottom': '16px', display: 'flex', 'justify-content': 'space-between', 'align-items': 'center' }}
        >
          <span>{notification()?.message}</span>
          <button
            onClick={() => setNotification(null)}
            style={{ background: 'transparent', border: 'none', cursor: 'pointer', 'font-size': '14px' }}
          >
            ✕
          </button>
        </div>
      </Show>

      {/* Toolbar & Search */}
      <div class="card" style={{ 'margin-bottom': '20px' }}>
        <div class="toolbar" style={{ 'justify-content': 'space-between' }}>
          <div class="field" style={{ 'min-width': '280px', flex: '1' }}>
            <label>Buscar Lotes para Control de Calidad</label>
            <input
              type="text"
              placeholder="Filtrar por código de lote, especie o procedencia..."
              value={searchTerm()}
              onInput={(e) => setSearchTerm(e.currentTarget.value)}
            />
          </div>

          <button
            onClick={loadData}
            class="btn btn-ghost"
            style={{ 'align-self': 'flex-end', height: '38px' }}
          >
            🔄 Actualizar
          </button>
        </div>
      </div>

      {/* Table */}
      <DataTable
        columns={columns}
        data={filteredLotes()}
        loading={loading()}
        emptyMessage="No se encontraron lotes para análisis de laboratorio."
      />

      {/* Analysis Modal */}
      <LabAnalysisModal
        open={analysisModalOpen()}
        lote={selectedLote()}
        onClose={() => {
          setAnalysisModalOpen(false);
          setSelectedLote(null);
        }}
        onSuccess={() => {
          setNotification({ type: 'success', message: 'Dictamen de laboratorio registrado y lote actualizado con éxito.' });
          loadData();
        }}
      />

      {/* Ficha Técnica Modal (RF14 / CU-14) */}
      <LoteFichaTecnicaModal
        open={fichaModalOpen()}
        loteId={selectedFichaLoteId()}
        onClose={() => {
          setFichaModalOpen(false);
          setSelectedFichaLoteId(null);
        }}
      />
    </section>
  );
}

