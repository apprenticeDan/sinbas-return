import { httpClient } from './HttpClient';
import {
  LoteItem,
  CreateLotePayload,
  BloquearLotePayload,
  LiberarCuarentenaPayload,
  RechazarLotePayload,
  SolicitarNuevoAnalisisPayload,
} from '../../domain/models/Lote';

export interface AnalisisResumenDto {
  id: string;
  fechaAnalisis: string;
  germinacion: number;
  pureza: number;
  humedad: number;
  viabilidad: number;
  dictamen: string;
  observaciones?: string;
}

export interface FichaTecnicaLoteDto {
  id: string;
  codigo: string;
  productoId: string;
  nombreProducto: string;
  categoria: string;
  genero?: string;
  epiteto?: string;
  procedencia?: string;
  cantidadInicial: number;
  cantidadActual: number;
  unidad: string;
  fechaIngreso: string;
  ubicacion?: string;
  observaciones?: string;
  estado: string;
  requiereLiberacionGerencial?: boolean;
  historialAnalisis: AnalisisResumenDto[];
  ultimoDictamen?: string;
}

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

  async liberarCuarentena(id: string, payload: LiberarCuarentenaPayload): Promise<LoteItem> {
    return httpClient<LoteItem>(`/lotes/${id}/liberar-cuarentena`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async rechazarLote(id: string, payload: RechazarLotePayload): Promise<LoteItem> {
    return httpClient<LoteItem>(`/lotes/${id}/rechazar`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async solicitarNuevoAnalisis(id: string, payload: SolicitarNuevoAnalisisPayload): Promise<LoteItem> {
    return httpClient<LoteItem>(`/lotes/${id}/solicitar-analisis`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async obtenerFichaTecnica(id: string): Promise<FichaTecnicaLoteDto> {
    return httpClient<FichaTecnicaLoteDto>(`/lotes/${id}`);
  },
};

