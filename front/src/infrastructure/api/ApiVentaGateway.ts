import { httpClient } from './HttpClient';

import type {
  LineaVentaDto,
  OrdenVentaDto,
  ConfirmarVentaRequest,
  VentaConfirmadaDto,
} from '../../domain/models/Venta';

export type * from '../../domain/models/Venta';
export type * from '../../domain/models/Despacho';

export const ApiVentaGateway = {
  async confirmarVenta(proformaId: string, req: ConfirmarVentaRequest): Promise<VentaConfirmadaDto> {
    return httpClient<VentaConfirmadaDto>(`/proformas/${proformaId}/confirmar-venta`, {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  async listarVentas(filtros?: { clienteId?: string; estado?: string; desde?: string; hasta?: string }): Promise<OrdenVentaDto[]> {
    const params = new URLSearchParams();
    if (filtros?.clienteId) params.append('clienteId', filtros.clienteId);
    if (filtros?.estado) params.append('estado', filtros.estado);
    if (filtros?.desde) params.append('desde', filtros.desde);
    if (filtros?.hasta) params.append('hasta', filtros.hasta);
    const qs = params.toString() ? `?${params.toString()}` : '';
    return httpClient<OrdenVentaDto[]>(`/ventas${qs}`);
  },

  async obtenerVentaPorId(id: string): Promise<OrdenVentaDto> {
    return httpClient<OrdenVentaDto>(`/ventas/${id}`);
  },

  async anularVenta(id: string, motivo: string): Promise<OrdenVentaDto> {
    return httpClient<OrdenVentaDto>(`/ventas/${id}/anular`, {
      method: 'PUT',
      body: JSON.stringify({ motivo }),
    });
  },
};
