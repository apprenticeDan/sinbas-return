import { ApiInventoryGateway } from '../../infrastructure/api/ApiInventoryGateway';
import type {
  RegistrarIngresoPayload,
  RegistrarEgresoPayload,
  MovimientoInventarioDto,
  StockProductoDto,
} from '../../domain/models/Almacen';

export const InventoryUseCases = {
  async registrarIngreso(payload: RegistrarIngresoPayload): Promise<MovimientoInventarioDto> {
    return ApiInventoryGateway.registrarIngreso(payload);
  },

  /** Registra un egreso con resolución FIFO en el backend (F8 / MF-08-03). */
  async registrarEgreso(payload: RegistrarEgresoPayload): Promise<MovimientoInventarioDto> {
    return ApiInventoryGateway.registrarEgreso(payload);
  },

  async fetchMovimientos(tipo?: string): Promise<MovimientoInventarioDto[]> {
    return ApiInventoryGateway.listarMovimientos(tipo);
  },

  async consultarStock(productoId: string): Promise<StockProductoDto> {
    return ApiInventoryGateway.consultarStock(productoId);
  },
};
