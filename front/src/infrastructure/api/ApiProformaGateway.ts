import { httpClient } from './HttpClient';

import type {
  ProformaDto,
  CrearProformaRequest,
  AnularProformaRequest,
  StockDisponibleDto,
} from '../../domain/models/Proforma';

export type * from '../../domain/models/Proforma';

export const ApiProformaGateway = {
  async listarProformas(estado?: string, clienteId?: string): Promise<ProformaDto[]> {
    const params = new URLSearchParams();
    if (estado && estado.trim()) params.append('estado', estado.trim());
    if (clienteId && clienteId.trim()) params.append('clienteId', clienteId.trim());
    const query = params.toString() ? `?${params.toString()}` : '';
    return httpClient<ProformaDto[]>(`/proformas${query}`);
  },

  async obtenerProformaPorId(id: string): Promise<ProformaDto> {
    return httpClient<ProformaDto>(`/proformas/${id}`);
  },

  async crearProforma(req: CrearProformaRequest): Promise<ProformaDto> {
    return httpClient<ProformaDto>('/proformas', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  async anularProforma(id: string, req: AnularProformaRequest): Promise<ProformaDto> {
    return httpClient<ProformaDto>(`/proformas/${id}/anular`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  async obtenerStockDisponible(productoId: string): Promise<StockDisponibleDto> {
    return httpClient<StockDisponibleDto>(`/inventario/stock-disponible/${productoId}`);
  },
};
