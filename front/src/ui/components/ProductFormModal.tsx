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
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-fade-in">
        <div class="w-full max-w-xl bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl overflow-hidden flex flex-col max-h-[90vh]">
          {/* Header */}
          <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-800/40">
            <h2 class="text-lg font-semibold text-emerald-400 flex items-center gap-2">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4"></path>
              </svg>
              Nuevo Producto en Catálogo (Borrador)
            </h2>
            <button
              onClick={() => catalogStore.setCreateModalOpen(false)}
              class="text-slate-400 hover:text-slate-200 transition-colors"
            >
              ✕
            </button>
          </div>

          {/* Form Content */}
          <form onSubmit={handleSubmit} class="p-6 space-y-4 overflow-y-auto custom-scrollbar">
            <Show when={formError()}>
              <div class="p-3 text-sm rounded-lg bg-rose-950/60 border border-rose-700/60 text-rose-300">
                {formError()}
              </div>
            </Show>

            {/* Categoría Selector */}
            <div>
              <label class="block text-xs font-semibold uppercase text-slate-300 mb-1">Categoría de Producto</label>
              <select
                value={categoria()}
                onChange={(e) => setCategoria(e.currentTarget.value as Category)}
                class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
              >
                <option value="Semilla">Semilla Forestal</option>
                <option value="Plantin">Plantín</option>
                <option value="Insumo">Insumo / Agroquímico</option>
                <option value="Otro">Otro Producto</option>
              </select>
            </div>

            {/* Campos condicionales para Semilla / Plantin */}
            <Show when={categoria() === 'Semilla' || categoria() === 'Plantin'}>
              <div class="grid grid-cols-2 gap-3">
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Género *</label>
                  <input
                    type="text"
                    required
                    placeholder="Ej. Swietenia"
                    value={genero()}
                    onInput={(e) => setGenero(e.currentTarget.value)}
                    class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                  />
                </div>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Epíteto *</label>
                  <input
                    type="text"
                    required
                    placeholder="Ej. macrophylla"
                    value={epiteto()}
                    onInput={(e) => setEpiteto(e.currentTarget.value)}
                    class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Nombres Comunes (separados por coma)</label>
                <input
                  type="text"
                  placeholder="Ej. Caoba, Mara, Mahogany"
                  value={nombresComunesText()}
                  onInput={(e) => setNombresComunesText(e.currentTarget.value)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                />
              </div>

              <Show when={categoria() === 'Plantin'}>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Etapa de Desarrollo</label>
                  <input
                    type="text"
                    placeholder="Ej. Repique, Aclimatación, Definitivo"
                    value={etapaDesarrollo()}
                    onInput={(e) => setEtapaDesarrollo(e.currentTarget.value)}
                    class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </Show>
            </Show>

            {/* Campos condicionales para Insumo / Otro */}
            <Show when={categoria() === 'Insumo' || categoria() === 'Otro'}>
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Nombre del Insumo / Producto *</label>
                <input
                  type="text"
                  required
                  placeholder="Ej. Fertilizante NPK 15-15-15"
                  value={nombreInsumo()}
                  onInput={(e) => setNombreInsumo(e.currentTarget.value)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                />
              </div>
              <Show when={categoria() === 'Insumo'}>
                <div>
                  <label class="block text-xs font-medium text-slate-300 mb-1">Marca</label>
                  <input
                    type="text"
                    placeholder="Ej. Yura, Yara"
                    value={marcaInsumo()}
                    onInput={(e) => setMarcaInsumo(e.currentTarget.value)}
                    class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </Show>
            </Show>

            {/* Unidad de Manejo & Trazabilidad */}
            <div class="grid grid-cols-2 gap-3">
              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Unidad de Manejo</label>
                <select
                  value={unidadManejo()}
                  onChange={(e) => setUnidadManejo(e.currentTarget.value)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                >
                  <option value="Kilogramo">Kilogramo (kg)</option>
                  <option value="Gramo">Gramo (g)</option>
                  <option value="Unidad_">Unidad (ud)</option>
                  <option value="Bolsa">Bolsa</option>
                </select>
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Trazabilidad</label>
                <select
                  value={trazabilidad()}
                  onChange={(e) => setTrazabilidad(e.currentTarget.value as Traceability)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
                >
                  <option value="PorLote">Por Lote (Requerido para Semillas)</option>
                  <option value="Simple">Simple (Sin lote)</option>
                </select>
              </div>
            </div>

            {/* Observaciones */}
            <div>
              <label class="block text-xs font-medium text-slate-300 mb-1">Observaciones</label>
              <textarea
                rows="2"
                placeholder="Detalles adicionales del producto..."
                value={observaciones()}
                onInput={(e) => setObservaciones(e.currentTarget.value)}
                class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 focus:outline-none focus:border-emerald-500"
              />
            </div>

            {/* Note alert */}
            <div class="p-3 rounded-lg bg-emerald-950/30 border border-emerald-800/40 text-emerald-300 text-xs">
              ⓘ El producto se registrará en estado <strong>PendientePrecioBorrador</strong>. Gerencia/Administración asignará el precio oficial para habilitarlo comercialmente.
            </div>

            {/* Actions */}
            <div class="pt-2 flex justify-end gap-3 border-t border-slate-800">
              <button
                type="button"
                onClick={() => catalogStore.setCreateModalOpen(false)}
                class="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-sm font-medium transition-colors"
              >
                Cancelar
              </button>
              <button
                type="submit"
                disabled={catalogStore.loading()}
                class="px-5 py-2 rounded-lg bg-gradient-to-r from-emerald-600 to-teal-600 hover:from-emerald-500 hover:to-teal-500 text-white text-sm font-medium shadow-lg shadow-emerald-900/30 transition-all flex items-center gap-2"
              >
                <Show when={catalogStore.loading()}>
                  <span class="animate-spin text-lg">↻</span>
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
