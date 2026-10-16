import { ApiVentaGateway } from '../../infrastructure/api/ApiVentaGateway';
import type {
  OrdenVentaDto,
  ConfirmarVentaRequest,
  VentaConfirmadaDto,
} from '../../domain/models/Venta';

export const VentaUseCases = {
  async confirmarVenta(proformaId: string, req: ConfirmarVentaRequest): Promise<VentaConfirmadaDto> {
    return ApiVentaGateway.confirmarVenta(proformaId, req);
  },

  async listarVentas(filtros?: { clienteId?: string; estado?: string; desde?: string; hasta?: string }): Promise<OrdenVentaDto[]> {
    return ApiVentaGateway.listarVentas(filtros);
  },

  async obtenerVentaPorId(id: string): Promise<OrdenVentaDto> {
    return ApiVentaGateway.obtenerVentaPorId(id);
  },
};
