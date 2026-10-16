import { ApiClientGateway } from '../../infrastructure/api/ApiClientGateway';
import type {
  ClienteDto,
  CrearClienteNaturalPayload,
  CrearClienteJuridicaPayload,
  ActualizarClientePayload,
} from '../../domain/models/Client';

export const ClientUseCases = {
  async listarClientes(termino?: string): Promise<ClienteDto[]> {
    return ApiClientGateway.listarClientes(termino);
  },

  async obtenerClientePorId(id: string): Promise<ClienteDto> {
    return ApiClientGateway.obtenerClientePorId(id);
  },

  async crearClienteNatural(payload: CrearClienteNaturalPayload): Promise<ClienteDto> {
    return ApiClientGateway.crearClienteNatural(payload);
  },

  async crearClienteJuridica(payload: CrearClienteJuridicaPayload): Promise<ClienteDto> {
    return ApiClientGateway.crearClienteJuridica(payload);
  },

  async actualizarCliente(id: string, payload: ActualizarClientePayload): Promise<ClienteDto> {
    return ApiClientGateway.actualizarCliente(id, payload);
  },
};
