import { httpClient } from './HttpClient';

import type {
  SolicitudDto,
  CrearSolicitudRequest,
  RechazarSolicitudRequest,
} from '../../domain/models/Solicitud';

export type * from '../../domain/models/Solicitud';

export const ApiSolicitudGateway = {
  async listarSolicitudes(estado?: string): Promise<SolicitudDto[]> {
    const params = new URLSearchParams();
    if (estado && estado.trim()) params.append('estado', estado.trim());
    const query = params.toString() ? `?${params.toString()}` : '';
    return httpClient<SolicitudDto[]>(`/solicitudes${query}`);
  },

  async obtenerSolicitudPorId(id: string): Promise<SolicitudDto> {
    return httpClient<SolicitudDto>(`/solicitudes/${id}`);
  },

  async crearSolicitud(req: CrearSolicitudRequest): Promise<SolicitudDto> {
    return httpClient<SolicitudDto>('/solicitudes', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  async aprobarSolicitud(id: string): Promise<SolicitudDto> {
    return httpClient<SolicitudDto>(`/solicitudes/${id}/aprobar`, {
      method: 'PUT',
      body: JSON.stringify({}),
    });
  },

  async rechazarSolicitud(id: string, req: RechazarSolicitudRequest): Promise<SolicitudDto> {
    return httpClient<SolicitudDto>(`/solicitudes/${id}/rechazar`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  async confirmarSolicitud(id: string): Promise<SolicitudDto> {
    return httpClient<SolicitudDto>(`/solicitudes/${id}/confirmar`, {
      method: 'POST',
      body: JSON.stringify({}),
    });
  },
};
