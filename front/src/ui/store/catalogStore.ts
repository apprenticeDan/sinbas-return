import { createSignal, createMemo } from 'solid-js';
import { Product, CreateProductPayload, AssignPricePayload } from '../../domain/models/Product';
import { CatalogUseCases } from '../../application/usecases/CatalogUseCases';

const [products, setProducts] = createSignal<Product[]>([]);
const [loading, setLoading] = createSignal<boolean>(false);
const [error, setError] = createSignal<string | null>(null);

const [categoryFilter, setCategoryFilter] = createSignal<string>('');
const [stateFilter, setStateFilter] = createSignal<string>('');
const [searchTerm, setSearchTerm] = createSignal<string>('');

const [createModalOpen, setCreateModalOpen] = createSignal<boolean>(false);
const [priceModalOpen, setPriceModalOpen] = createSignal<boolean>(false);
const [selectedProductForPrice, setSelectedProductForPrice] = createSignal<Product | null>(null);

export const filteredProducts = createMemo(() => {
  const term = searchTerm().toLowerCase().trim();
  return products().filter((p) => {
    const matchesSearch =
      !term ||
      p.nombreVisible.toLowerCase().includes(term) ||
      p.nombresComunes.some((n) => n.toLowerCase().includes(term)) ||
      p.unidadManejo.toLowerCase().includes(term);

    const matchesCategory = !categoryFilter() || p.categoria === categoryFilter();
    const matchesState = !stateFilter() || p.estadoComercial === stateFilter();

    return matchesSearch && matchesCategory && matchesState;
  });
});

export const catalogStore = {
  products,
  loading,
  error,
  categoryFilter,
  stateFilter,
  searchTerm,
  createModalOpen,
  priceModalOpen,
  selectedProductForPrice,
  filteredProducts,

  setCategoryFilter,
  setStateFilter,
  setSearchTerm,
  setCreateModalOpen,
  setPriceModalOpen,

  async loadCatalog() {
    setLoading(true);
    setError(null);
    try {
      const list = await CatalogUseCases.fetchCatalog();
      setProducts(list);
    } catch (err: any) {
      setError(err.message || 'Error al cargar catálogo de productos');
    } finally {
      setLoading(false);
    }
  },

  async createProduct(payload: CreateProductPayload): Promise<boolean> {
    setLoading(true);
    setError(null);
    try {
      const created = await CatalogUseCases.createNewProduct(payload);
      setProducts((prev) => [created, ...prev]);
      setCreateModalOpen(false);
      return true;
    } catch (err: any) {
      setError(err.message || 'Error al crear producto');
      return false;
    } finally {
      setLoading(false);
    }
  },

  openAssignPriceModal(product: Product) {
    setSelectedProductForPrice(product);
    setPriceModalOpen(true);
  },

  async assignPrice(payload: AssignPricePayload): Promise<boolean> {
    setLoading(true);
    setError(null);
    try {
      const updated = await CatalogUseCases.assignOfficialPrice(payload);
      setProducts((prev) => prev.map((p) => (p.id === updated.id ? updated : p)));
      setPriceModalOpen(false);
      setSelectedProductForPrice(null);
      return true;
    } catch (err: any) {
      setError(err.message || 'Error al asignar precio oficial');
      return false;
    } finally {
      setLoading(false);
    }
  },
};
