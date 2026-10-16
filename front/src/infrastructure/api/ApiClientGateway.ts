import { httpClient } from './HttpClient';
import type {
  ClienteDto,
  CrearClienteNaturalPayload,
  CrearClienteJuridicaPayload,
  ActualizarClientePayload,
} from '../../domain/models/Client';

export type * from '../../domain/models/Client';

export const ApiClientGateway = {
  async listarClientes(termino?: string): Promise<ClienteDto[]> {
    const q = termino && termino.trim() ? `?q=${encodeURIComponent(termino.trim())}` : '';
    return httpClient<ClienteDto[]>(`/clientes${q}`);
  },

  async obtenerClientePorId(id: string): Promise<ClienteDto> {
    return httpClient<ClienteDto>(`/clientes/${id}`);
  },

  async crearClienteNatural(payload: CrearClienteNaturalPayload): Promise<ClienteDto> {
    return httpClient<ClienteDto>('/clientes/natural', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async crearClienteJuridica(payload: CrearClienteJuridicaPayload): Promise<ClienteDto> {
    return httpClient<ClienteDto>('/clientes/juridica', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async actualizarCliente(id: string, payload: ActualizarClientePayload): Promise<ClienteDto> {
    return httpClient<ClienteDto>(`/clientes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },
};
