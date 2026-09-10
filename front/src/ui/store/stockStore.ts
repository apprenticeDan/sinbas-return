import { createSignal } from 'solid-js';
import { ApiInventoryGateway } from '../../infrastructure/api/ApiInventoryGateway';
import type { StockConsolidadoProducto, KardexItem } from '../../domain/models/Stock';

const [productosStock, setProductosStock] = createSignal<StockConsolidadoProducto[]>([]);
const [kardexItems, setKardexItems] = createSignal<KardexItem[]>([]);
const [isLoading, setIsLoading] = createSignal<boolean>(false);
const [error, setError] = createSignal<string | null>(null);

const [filtroCategoria, setFiltroCategoria] = createSignal<string>('TODAS');
const [filtroBusqueda, setFiltroBusqueda] = createSignal<string>('');
const [filtroAlerta, setFiltroAlerta] = createSignal<string>('TODAS');

const [productoKardexId, setProductoKardexId] = createSignal<string>('');
const [loteKardexId, setLoteKardexId] = createSignal<string>('');
const [productoExpandidoId, setProductoExpandidoId] = createSignal<string | null>(null);

export const stockStore = {
  productosStock,
  kardexItems,
  isLoading,
  error,
  filtroCategoria,
  filtroBusqueda,
  filtroAlerta,
  productoKardexId,
  loteKardexId,
  productoExpandidoId,

  setFiltroCategoria,
  setFiltroBusqueda,
  setFiltroAlerta,
  setProductoKardexId,
  setLoteKardexId,

  toggleExpandirLotes: (prodId: string) => {
    setProductoExpandidoId((prev) => (prev === prodId ? null : prodId));
  },

  cargarStock: async (umbral?: number) => {
    setIsLoading(true);
    setError(null);
    try {
      const data = await ApiInventoryGateway.consultarStockConsolidado(umbral);
      setProductosStock(data);
    } catch (err: any) {
      console.error('Error al cargar existencias de almacén:', err);
      setError(err?.message || 'Error al conectar con el servidor de inventario');
    } finally {
      setIsLoading(false);
    }
  },

  cargarKardex: async (productoId?: string, loteId?: string) => {
    setIsLoading(true);
    setError(null);
    try {
      const pId = productoId !== undefined ? productoId : productoKardexId();
      const lId = loteId !== undefined ? loteId : loteKardexId();
      const data = await ApiInventoryGateway.consultarKardex(pId || undefined, lId || undefined);
      setKardexItems(data);
    } catch (err: any) {
      console.error('Error al cargar kardex:', err);
      setError(err?.message || 'Error al cargar el historial del kardex');
    } finally {
      setIsLoading(false);
    }
  },

  // ─────────────────────────────────────────────────────────────
  // Métricas Computadas
  // ─────────────────────────────────────────────────────────────

  productosFiltrados: () => {
    const prods = productosStock();
    const cat = filtroCategoria();
    const q = filtroBusqueda().toLowerCase().trim();
    const alerta = filtroAlerta();

    return prods.filter((p) => {
      // Filtro por categoría
      if (cat !== 'TODAS' && p.categoria !== cat) {
        return false;
      }

      // Filtro por nivel de alerta
      if (alerta === 'ALERTA' && p.alerta === 'StockNormal') {
        return false;
      }
      if (alerta === 'NORMAL' && p.alerta !== 'StockNormal') {
        return false;
      }

      // Filtro por búsqueda de texto
      if (q) {
        const coincideNombre = p.nombreProducto.toLowerCase().includes(q);
        const coincideLote = (p.lotes || []).some(
          (l) =>
            l.codigo.toLowerCase().includes(q) ||
            (l.procedencia && l.procedencia.toLowerCase().includes(q))
        );
        return coincideNombre || coincideLote;
      }

      return true;
    });
  },

  totalProductosConStock: () => {
    return productosStock().filter((p) => p.stockTotalGramos > 0).length;
  },

  stockTotalAlmacenKg: () => {
    const totalGramos = productosStock().reduce((acc, p) => acc + p.stockTotalGramos, 0);
    return Math.round((totalGramos / 1000) * 100) / 100;
  },

  totalAlertasStock: () => {
    return productosStock().filter((p) => p.alerta === 'SinStock' || p.alerta === 'BajoStock').length;
  },
};
