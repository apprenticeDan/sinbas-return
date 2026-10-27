import { ApiSolicitudGateway } from '../../infrastructure/api/ApiSolicitudGateway';
import type {
  SolicitudDto,
  CrearSolicitudRequest,
} from '../../domain/models/Solicitud';

export const SolicitudUseCases = {
  listar: (estado?: string): Promise<SolicitudDto[]> =>
    ApiSolicitudGateway.listarSolicitudes(estado),

  obtenerPorId: (id: string): Promise<SolicitudDto> =>
    ApiSolicitudGateway.obtenerSolicitudPorId(id),

  crear: (req: CrearSolicitudRequest): Promise<SolicitudDto> =>
    ApiSolicitudGateway.crearSolicitud(req),

  aprobar: (id: string): Promise<SolicitudDto> =>
    ApiSolicitudGateway.aprobarSolicitud(id),

  rechazar: (id: string, motivo: string): Promise<SolicitudDto> =>
    ApiSolicitudGateway.rechazarSolicitud(id, { motivo }),

  confirmar: (id: string): Promise<SolicitudDto> =>
    ApiSolicitudGateway.confirmarSolicitud(id),
};
