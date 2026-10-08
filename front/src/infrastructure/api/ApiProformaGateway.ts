import { httpClient } from './HttpClient';

export interface LineaCotizadaDto {
  productoId: string;
  nombreProducto: string;
  cantidad: number;
  unidad: string;
  precioUnitario: number;
  subtotal: number;
}

export interface ProformaDto {
  id: string;
  fecha: string;
  creadoEn: string;
  responsableId: string;
  clienteId?: string;
  clienteNombre: string;
  estado: string;
  estadoProyectado: string;
  fechaVencimiento?: string;
  moneda: string;
  lineas: LineaCotizadaDto[];
  total: number;
  leyenda: string;
  ordenVentaId?: string;
  anulacionMotivo?: string;
  anuladoPor?: string;
  anuladoEn?: string;
  observaciones?: string;
}

export interface LineaCotizadaRequest {
  productoId: string;
  cantidad: number;
  unidad: string;
}

export interface CrearProformaRequest {
  clienteId?: string;
  clienteNombreLibre?: string;
  lineas: LineaCotizadaRequest[];
  fechaVencimiento?: string;
  moneda?: string;
  observaciones?: string;
}

export interface AnularProformaRequest {
  motivo: string;
}

export interface StockDisponibleDto {
  productoId: string;
  nombreProducto: string;
  stockDisponibleBase: number;
  stockDisponible: number;
  unidad: string;
  unidadEtiqueta: string;
  precioOficial: number;
  moneda: string;
  aptoParaVenta: boolean;
}

export const ApiProformaGateway = {
  async listarProformas(estado?: string, clienteId?: string): Promise<ProformaDto[]> {
    const params = new URLSearchParams();
    if (estado && estado.trim()) params.append('estado', estado.trim());
    if (clienteId && clienteId.trim()) params.append('clienteId', clienteId.trim());
    const query = params.toString() ? `?${params.toString()}` : '';
    return httpClient<ProformaDto[]>(`/proformas${query}`);
  },

  async obtenerProformaPorId(id: string): Promise<ProformaDto> {
    return httpClient<ProformaDto>(`/proformas/${id}`);
  },

  async crearProforma(req: CrearProformaRequest): Promise<ProformaDto> {
    return httpClient<ProformaDto>('/proformas', {
      method: 'POST',
      body: JSON.stringify(req),
    });
  },

  async anularProforma(id: string, req: AnularProformaRequest): Promise<ProformaDto> {
    return httpClient<ProformaDto>(`/proformas/${id}/anular`, {
      method: 'PUT',
      body: JSON.stringify(req),
    });
  },

  async obtenerStockDisponible(productoId: string): Promise<StockDisponibleDto> {
    return httpClient<StockDisponibleDto>(`/inventario/stock-disponible/${productoId}`);
  },
};
