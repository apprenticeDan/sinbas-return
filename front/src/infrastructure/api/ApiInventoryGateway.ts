import { httpClient } from './HttpClient';
import type {
  RegistrarIngresoPayload,
  RegistrarEgresoPayload,
  MovimientoInventarioDto,
  StockProductoDto,
} from '../../domain/models/Almacen';

export const ApiInventoryGateway = {
  /**
   * Registra un ingreso de almacén en el backend real (F4 / MF-04-01).
   */
  async registrarIngreso(payload: RegistrarIngresoPayload): Promise<MovimientoInventarioDto> {
    return httpClient<MovimientoInventarioDto>('/inventario/ingreso', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  /**
   * Registra un egreso de almacén con resolución FIFO en el backend (F8 / MF-08-03).
   */
  async registrarEgreso(payload: RegistrarEgresoPayload): Promise<MovimientoInventarioDto> {
    return httpClient<MovimientoInventarioDto>('/inventario/egreso', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  /**
   * Obtiene la lista cronológica de movimientos de inventario (F4 / F5 / F8).
   * @param tipo 'Entrada' | 'Salida' opcional
   */
  async listarMovimientos(tipo?: string): Promise<MovimientoInventarioDto[]> {
    const query = tipo ? `?tipo=${encodeURIComponent(tipo)}` : '';
    return httpClient<MovimientoInventarioDto[]>(`/inventario/movimientos${query}`);
  },

  /**
   * Consulta el stock real de un producto y el desglose de sus lotes (F5 / MF-05-01).
   */
  async consultarStock(productoId: string): Promise<StockProductoDto> {
    return httpClient<StockProductoDto>(`/inventario/stock/${productoId}`);
  },
};
