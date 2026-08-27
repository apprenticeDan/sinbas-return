import { createSignal, Show } from 'solid-js';
import { Category, CreateProductPayload, Traceability } from '../../domain/models/Product';
import { catalogStore } from '../store/catalogStore';

export function ProductFormModal() {
  const [categoria, setCategoria] = createSignal<Category>('Semilla');
  const [genero, setGenero] = createSignal<string>('');
  const [epiteto, setEpiteto] = createSignal<string>('');
  const [observacionesNc, setObservacionesNc] = createSignal<string>('');
  const [nombresComunesText, setNombresComunesText] = createSignal<string>('');
  const [etapaDesarrollo, setEtapaDesarrollo] = createSignal<string>('');
  const [nombreInsumo, setNombreInsumo] = createSignal<string>('');
  const [marcaInsumo, setMarcaInsumo] = createSignal<string>('');
  const [descripcionInsumo, setDescripcionInsumo] = createSignal<string>('');
  const [unidadManejo, setUnidadManejo] = createSignal<string>('Kilogramo');
  const [gramosNominales, setGramosNominales] = createSignal<number | undefined>(undefined);
  const [trazabilidad, setTrazabilidad] = createSignal<Traceability>('PorLote');
  const [observaciones, setObservaciones] = createSignal<string>('');

  const [formError, setFormError] = createSignal<string | null>(null);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setFormError(null);

    const coms = nombresComunesText()
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s.length > 0);

    const payload: CreateProductPayload = {
      categoria: categoria(),
      genero: genero().trim() || undefined,
      epiteto: epiteto().trim() || undefined,
      observacionesNc: observacionesNc().trim() || undefined,
      nombresComunes: coms,
      etapaDesarrollo: etapaDesarrollo().trim() || undefined,
      nombreInsumo: nombreInsumo().trim() || undefined,
      marcaInsumo: marcaInsumo().trim() || undefined,
      descripcionInsumo: descripcionInsumo().trim() || undefined,
      unidadManejo: unidadManejo(),
      gramosNominales: gramosNominales(),
      trazabilidad: trazabilidad(),
      observaciones: observaciones().trim() || undefined,
    };

    const success = await catalogStore.createProduct(payload);
    if (!success) {
      setFormError(catalogStore.error() || 'Ocurrió un error al registrar el producto.');
    }
  };

  return (
    <Show when={catalogStore.createModalOpen()}>
      <div class="modal-overlay">
        <div class="modal-card" style={{ 'max-width': '520px' }}>
          {/* Header */}
          <div class="modal-header">
            <h2 class="modal-title">Nuevo Producto en Catálogo</h2>
            <button
              onClick={() => catalogStore.setCreateModalOpen(false)}
              class="btn btn-ghost"
              style={{ padding: '4px 8px', 'font-size': '16px' }}
            >
              ✕
            </button>
          </div>

          {/* Form Content */}
          <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
            <Show when={formError()}>
              <div class="alert-error">
                {formError()}
              </div>
            </Show>

            {/* Categoría Selector */}
            <div class="field">
              <label>Categoría de Producto</label>
              <select
                value={categoria()}
                onChange={(e) => setCategoria(e.currentTarget.value as Category)}
              >
                <option value="Semilla">Semilla Forestal</option>
                <option value="Plantin">Plantín</option>
                <option value="Insumo">Insumo / Agroquímico</option>
                <option value="Otro">Otro Producto</option>
              </select>
            </div>

            {/* Campos condicionales para Semilla / Plantin */}
            <Show when={categoria() === 'Semilla' || categoria() === 'Plantin'}>
              <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '10px' }}>
                <div class="field">
                  <label>Género *</label>
                  <input
                    type="text"
                    required
                    placeholder="Ej. Swietenia"
                    value={genero()}
                    onInput={(e) => setGenero(e.currentTarget.value)}
                  />
                </div>
                <div class="field">
                  <label>Epíteto *</label>
                  <input
                    type="text"
                    required
                    placeholder="Ej. macrophylla"
                    value={epiteto()}
                    onInput={(e) => setEpiteto(e.currentTarget.value)}
                  />
                </div>
              </div>

              <div class="field">
                <label>Nombres Comunes (separados por coma)</label>
                <input
                  type="text"
                  placeholder="Ej. Caoba, Mara, Mahogany"
                  value={nombresComunesText()}
                  onInput={(e) => setNombresComunesText(e.currentTarget.value)}
                />
              </div>

              <Show when={categoria() === 'Plantin'}>
                <div class="field">
                  <label>Etapa de Desarrollo</label>
                  <input
                    type="text"
                    placeholder="Ej. Repique, Aclimatación, Definitivo"
                    value={etapaDesarrollo()}
                    onInput={(e) => setEtapaDesarrollo(e.currentTarget.value)}
                  />
                </div>
              </Show>
            </Show>

            {/* Campos condicionales para Insumo / Otro */}
            <Show when={categoria() === 'Insumo' || categoria() === 'Otro'}>
              <div class="field">
                <label>Nombre del Insumo / Producto *</label>
                <input
                  type="text"
                  required
                  placeholder="Ej. Fertilizante NPK 15-15-15"
                  value={nombreInsumo()}
                  onInput={(e) => setNombreInsumo(e.currentTarget.value)}
                />
              </div>
              <Show when={categoria() === 'Insumo'}>
                <div class="field">
                  <label>Marca</label>
                  <input
                    type="text"
                    placeholder="Ej. Yura, Yara"
                    value={marcaInsumo()}
                    onInput={(e) => setMarcaInsumo(e.currentTarget.value)}
                  />
                </div>
              </Show>
            </Show>

            {/* Unidad de Manejo & Trazabilidad */}
            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '10px' }}>
              <div class="field">
                <label>Unidad de Manejo</label>
                <select
                  value={unidadManejo()}
                  onChange={(e) => setUnidadManejo(e.currentTarget.value)}
                >
                  <option value="Kilogramo">Kilogramo (kg)</option>
                  <option value="Gramo">Gramo (g)</option>
                  <option value="Unidad_">Unidad (ud)</option>
                  <option value="Bolsa">Bolsa</option>
                </select>
              </div>

              <div class="field">
                <label>Trazabilidad</label>
                <select
                  value={trazabilidad()}
                  onChange={(e) => setTrazabilidad(e.currentTarget.value as Traceability)}
                >
                  <option value="PorLote">Por Lote (Requerido para Semillas)</option>
                  <option value="Simple">Simple (Sin lote)</option>
                </select>
              </div>
            </div>

            {/* Observaciones */}
            <div class="field">
              <label>Observaciones</label>
              <input
                type="text"
                placeholder="Detalles adicionales..."
                value={observaciones()}
                onInput={(e) => setObservaciones(e.currentTarget.value)}
              />
            </div>

            {/* Note alert */}
            <div class="pill pill-amber" style={{ padding: '8px 12px', 'border-radius': 'var(--radius-s)', 'font-size': '11.5px', 'line-height': '1.4' }}>
              ⓘ El producto se registrará en estado <strong>PendientePrecioBorrador</strong>. Gerencia asignará el precio oficial.
            </div>

            {/* Actions */}
            <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '8px', 'margin-top': '8px' }}>
              <button
                type="button"
                onClick={() => catalogStore.setCreateModalOpen(false)}
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
                Registrar Producto
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
