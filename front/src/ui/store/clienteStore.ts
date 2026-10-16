import { createSignal } from 'solid-js';
import type {
  ClienteDto,
  CrearClienteNaturalPayload,
  CrearClienteJuridicaPayload,
  ActualizarClientePayload,
} from '../../domain/models/Client';
import { ClientUseCases } from '../../application/usecases/ClientUseCases';

const [clientes, setClientes] = createSignal<ClienteDto[]>([]);
const [loading, setLoading] = createSignal<boolean>(false);
const [error, setError] = createSignal<string | null>(null);

const [searchTerm, setSearchTerm] = createSignal<string>('');
const [tipoFilter, setTipoFilter] = createSignal<string>('');
const [selectedCliente, setSelectedCliente] = createSignal<ClienteDto | null>(null);

export const filteredClientes = () => {
  const term = searchTerm().toLowerCase().trim();
  const tipo = tipoFilter();
  return clientes().filter((c) => {
    const matchesTipo = !tipo || c.tipo === tipo;
    const matchesSearch =
      !term ||
      c.nombreVisible.toLowerCase().includes(term) ||
      (c.nit && c.nit.toLowerCase().includes(term)) ||
      (c.persona && c.persona.ciFormateado.toLowerCase().includes(term)) ||
      (c.representante && c.representante.nombreCompleto.toLowerCase().includes(term));

    return matchesTipo && matchesSearch;
  });
};

export const clienteStore = {
  clientes,
  loading,
  error,
  searchTerm,
  tipoFilter,
  selectedCliente,
  filteredClientes,

  setSearchTerm,
  setTipoFilter,
  setSelectedCliente,

  async loadClientes(termino?: string): Promise<ClienteDto[]> {
    setLoading(true);
    setError(null);
    try {
      const list = await ClientUseCases.listarClientes(termino);
      setClientes(list);
      return list;
    } catch (err: any) {
      const msg = err.message || 'Error al cargar clientes';
      setError(msg);
      return [];
    } finally {
      setLoading(false);
    }
  },

  async crearClienteNatural(payload: CrearClienteNaturalPayload): Promise<ClienteDto | null> {
    setLoading(true);
    setError(null);
    try {
      const creado = await ClientUseCases.crearClienteNatural(payload);
      setClientes((prev) => [creado, ...prev]);
      return creado;
    } catch (err: any) {
      setError(err.message || 'Error al registrar cliente natural');
      return null;
    } finally {
      setLoading(false);
    }
  },

  async crearClienteJuridica(payload: CrearClienteJuridicaPayload): Promise<ClienteDto | null> {
    setLoading(true);
    setError(null);
    try {
      const creado = await ClientUseCases.crearClienteJuridica(payload);
      setClientes((prev) => [creado, ...prev]);
      return creado;
    } catch (err: any) {
      setError(err.message || 'Error al registrar cliente jurídico');
      return null;
    } finally {
      setLoading(false);
    }
  },

  async actualizarCliente(id: string, payload: ActualizarClientePayload): Promise<ClienteDto | null> {
    setLoading(true);
    setError(null);
    try {
      const actualizado = await ClientUseCases.actualizarCliente(id, payload);
      setClientes((prev) => prev.map((c) => (c.id === actualizado.id ? actualizado : c)));
      if (selectedCliente()?.id === id) {
        setSelectedCliente(actualizado);
      }
      return actualizado;
    } catch (err: any) {
      setError(err.message || 'Error al actualizar cliente');
      return null;
    } finally {
      setLoading(false);
    }
  },
};
