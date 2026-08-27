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
      <div class="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-sm animate-fade-in">
        <div class="w-full max-w-md bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl overflow-hidden flex flex-col">
          {/* Header */}
          <div class="px-6 py-4 border-b border-slate-800 flex items-center justify-between bg-slate-800/40">
            <h2 class="text-lg font-semibold text-emerald-400 flex items-center gap-2">
              <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
              </svg>
              Gobernanza de Precio Oficial
            </h2>
            <button
              onClick={() => catalogStore.setPriceModalOpen(false)}
              class="text-slate-400 hover:text-slate-200 transition-colors"
            >
              ✕
            </button>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} class="p-6 space-y-4">
            <Show when={modalError()}>
              <div class="p-3 text-sm rounded-lg bg-rose-950/60 border border-rose-700/60 text-rose-300">
                {modalError()}
              </div>
            </Show>

            <div class="p-3 rounded-lg bg-slate-800/60 border border-slate-700/60 text-slate-200 text-sm space-y-1">
              <div class="text-xs font-semibold uppercase text-slate-400">Producto Seleccionado</div>
              <div class="font-bold text-emerald-400 text-base">{product()?.nombreVisible}</div>
              <div class="text-xs text-slate-400">
                Categoría: <span class="text-slate-300">{product()?.categoria}</span> | Unidad: <span class="text-slate-300">{product()?.unidadManejo}</span>
              </div>
            </div>

            <div class="grid grid-cols-3 gap-3">
              <div class="col-span-2">
                <label class="block text-xs font-medium text-slate-300 mb-1">Precio Oficial *</label>
                <input
                  type="number"
                  step="0.01"
                  min="0.01"
                  required
                  placeholder="0.00"
                  value={monto() || ''}
                  onInput={(e) => setMonto(parseFloat(e.currentTarget.value) || 0)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 font-mono text-lg focus:outline-none focus:border-emerald-500"
                />
              </div>

              <div>
                <label class="block text-xs font-medium text-slate-300 mb-1">Moneda</label>
                <select
                  value={moneda()}
                  onChange={(e) => setMoneda(e.currentTarget.value)}
                  class="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-slate-100 font-semibold focus:outline-none focus:border-emerald-500"
                >
                  <option value="BOB">BOB (Bs.)</option>
                  <option value="USD">USD ($)</option>
                </select>
              </div>
            </div>

            <div class="p-3 rounded-lg bg-amber-950/30 border border-amber-800/40 text-amber-300 text-xs">
              ⚠️ Al guardar el precio oficial, el producto cambiará automáticamente de <strong>PendientePrecioBorrador</strong> a <strong>ActivoParaVenta</strong>.
            </div>

            {/* Actions */}
            <div class="pt-3 flex justify-end gap-3 border-t border-slate-800">
              <button
                type="button"
                onClick={() => catalogStore.setPriceModalOpen(false)}
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
                Aprobar y Activar Venta
              </button>
            </div>
          </form>
        </div>
      </div>
    </Show>
  );
}
