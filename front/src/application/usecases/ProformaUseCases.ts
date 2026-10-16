import { ApiProformaGateway } from '../../infrastructure/api/ApiProformaGateway';
import type {
  ProformaDto,
  CrearProformaRequest,
  AnularProformaRequest,
  StockDisponibleDto,
} from '../../domain/models/Proforma';

export const ProformaUseCases = {
  async listarProformas(estado?: string, clienteId?: string): Promise<ProformaDto[]> {
    return ApiProformaGateway.listarProformas(estado, clienteId);
  },

  async obtenerProformaPorId(id: string): Promise<ProformaDto> {
    return ApiProformaGateway.obtenerProformaPorId(id);
  },

  async crearProforma(req: CrearProformaRequest): Promise<ProformaDto> {
    return ApiProformaGateway.crearProforma(req);
  },

  async anularProforma(id: string, req: AnularProformaRequest): Promise<ProformaDto> {
    return ApiProformaGateway.anularProforma(id, req);
  },

  async obtenerStockDisponible(productoId: string): Promise<StockDisponibleDto> {
    return ApiProformaGateway.obtenerStockDisponible(productoId);
  },
};
