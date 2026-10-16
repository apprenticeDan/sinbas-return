import { httpClient } from './HttpClient';
import type {
  OrdenDespachoDto,
  OrdenDespachoDetalleDto,
  DespachoConfirmadoDto,
  ConfirmarDespachoRequest,
} from '../../domain/models/Despacho';

export type * from '../../domain/models/Despacho';

export const ApiDespachoGateway = {
  async listarDespachosPendientes(): Promise<OrdenDespachoDto[]> {
    return httpClient<OrdenDespachoDto[]>('/despachos/pendientes');
  },

  async obtenerDespachoPorId(id: string): Promise<OrdenDespachoDetalleDto> {
    return httpClient<OrdenDespachoDetalleDto>(`/despachos/${id}`);
  },

  async confirmarDespacho(id: string, req?: ConfirmarDespachoRequest): Promise<DespachoConfirmadoDto> {
    return httpClient<DespachoConfirmadoDto>(`/despachos/${id}/confirmar`, {
      method: 'POST',
      body: JSON.stringify(req || {}),
    });
  },
};
