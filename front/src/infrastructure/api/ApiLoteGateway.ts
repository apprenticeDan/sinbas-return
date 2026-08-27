import { httpClient } from './HttpClient';
import { LoteItem, CreateLotePayload, BloquearLotePayload } from '../../domain/models/Lote';

export const ApiLoteGateway = {
  async listarLotes(productoId?: string, estado?: string): Promise<LoteItem[]> {
    const params = new URLSearchParams();
    if (productoId) params.append('productoId', productoId);
    if (estado) params.append('estado', estado);
    const queryString = params.toString() ? `?${params.toString()}` : '';
    return httpClient<LoteItem[]>(`/lotes${queryString}`);
  },

  async crearLote(payload: CreateLotePayload): Promise<LoteItem> {
    return httpClient<LoteItem>('/lotes', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async bloquearLote(id: string, payload: BloquearLotePayload): Promise<LoteItem> {
    return httpClient<LoteItem>(`/lotes/${id}/bloquear`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },
};
