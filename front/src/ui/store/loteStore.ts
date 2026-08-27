import { createSignal, createMemo } from 'solid-js';
import { LoteItem, CreateLotePayload, BloquearLotePayload } from '../../domain/models/Lote';
import { ApiLoteGateway } from '../../infrastructure/api/ApiLoteGateway';

const [lotes, setLotes] = createSignal<LoteItem[]>([]);
const [loading, setLoading] = createSignal<boolean>(false);
const [searchTerm, setSearchTerm] = createSignal<string>('');
const [stateFilter, setStateFilter] = createSignal<string>('');
const [createModalOpen, setCreateModalOpen] = createSignal<boolean>(false);

const [selectedLote, setSelectedLote] = createSignal<LoteItem | null>(null);
const [blockModalOpen, setBlockModalOpen] = createSignal<boolean>(false);

export const loteStore = {
  lotes,
  loading,
  searchTerm,
  stateFilter,
  createModalOpen,
  selectedLote,
  blockModalOpen,

  setSearchTerm,
  setStateFilter,
  setCreateModalOpen,
  setBlockModalOpen,
  setSelectedLote,

  async loadLotes() {
    setLoading(true);
    try {
      const data = await ApiLoteGateway.listarLotes();
      setLotes(data);
    } catch (err: any) {
      console.error('Error cargando lotes:', err);
    } finally {
      setLoading(false);
    }
  },

  async crearLote(payload: CreateLotePayload) {
    const nuevo = await ApiLoteGateway.crearLote(payload);
    setLotes((prev) => [nuevo, ...prev]);
    setCreateModalOpen(false);
    return nuevo;
  },

  async bloquearLote(id: string, payload: BloquearLotePayload) {
    const actualizado = await ApiLoteGateway.bloquearLote(id, payload);
    setLotes((prev) => prev.map((l) => (l.id === id ? actualizado : l)));
    setBlockModalOpen(false);
    setSelectedLote(null);
    return actualizado;
  },

  filteredLotes: createMemo(() => {
    let list = lotes();
    const query = searchTerm().toLowerCase().trim();
    const st = stateFilter();

    if (query) {
      list = list.filter(
        (l) =>
          l.codigo.toLowerCase().includes(query) ||
          l.nombreProducto.toLowerCase().includes(query) ||
          (l.procedencia && l.procedencia.toLowerCase().includes(query)) ||
          (l.ubicacion && l.ubicacion.toLowerCase().includes(query))
      );
    }

    if (st) {
      list = list.filter((l) => l.estado === st);
    }

    return list;
  }),
};
