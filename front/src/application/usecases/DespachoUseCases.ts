import { ApiDespachoGateway } from '../../infrastructure/api/ApiDespachoGateway';
import type {
  OrdenDespachoDto,
  OrdenDespachoDetalleDto,
  DespachoConfirmadoDto,
  ConfirmarDespachoRequest,
} from '../../domain/models/Despacho';

export const DespachoUseCases = {
  async listarDespachosPendientes(): Promise<OrdenDespachoDto[]> {
    return ApiDespachoGateway.listarDespachosPendientes();
  },

  async obtenerDespachoPorId(id: string): Promise<OrdenDespachoDetalleDto> {
    return ApiDespachoGateway.obtenerDespachoPorId(id);
  },

  async confirmarDespacho(id: string, req?: ConfirmarDespachoRequest): Promise<DespachoConfirmadoDto> {
    return ApiDespachoGateway.confirmarDespacho(id, req);
  },
};
