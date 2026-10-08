import { httpClient } from './HttpClient';

export interface LineaVentaDto {
  productoId: string;
  nombreProducto: string;
  cantidad: number;
  unidad: string;
  precioUnitario: number;
  subtotal: number;
}

export interface OrdenVentaDto {
  id: string;
  codigo: string;
  proformaOrigenId: string;
  clienteId: string;
  clienteNombre: string;
  fecha: string;
  responsableId: string;
  lineas: LineaVentaDto[];
  total: number;
  moneda: string;
  estado: string; // 'Confirmada' | 'Despachada' | 'Anulada'
  creadoEn: string;
  anulacionMotivo?: string;
  anuladoPor?: string;
  anuladoEn?: string;
}

export interface LineaDespachoDto {
  productoId: string;
  nombreProducto: string;
  cantidad: number;
  unidad: string;
}

export interface OrdenDespachoDto {
  id: string;
  codigo: string;
  ordenVentaId?: string;
  codigoVenta?: string;
  origenTipo: string;
  clienteId?: string;
  clienteNombre?: string;
  estado: string; // 'Pendiente' | 'Despachado' | 'Anulado'
  movimientoId?: string;
  creadoEn: string;
  lineas: LineaDespachoDto[];
}

export interface LineaDespachadaDto {
  loteId: string;
  codigoLote: string;
  cantidadGramos: number;
  saldoRestanteGramos: number;
}

export interface SugerenciaLoteDto {
  productoId: string;
  nombreProducto: string;
  cantidadRequerida: number;
  unidad: string;
  lotesSugeridos: LineaDespachadaDto[];
}

export interface OrdenDespachoDetalleDto extends OrdenDespachoDto {
  sugerenciasFifo?: SugerenciaLoteDto[];
}

export interface VentaConfirmadaDto {
  venta: OrdenVentaDto;
  despacho: OrdenDespachoDto;
}

export interface DespachoConfirmadoDto {
  despachoId: string;
  codigoDespacho: string;
  movimientoId: string;
  fecha: string;
  lineasDespachadas: LineaDespachadaDto[];
  mensaje: string;
}

export interface ConfirmarVentaRequest {
  clienteId?: string;
}

export interface ConfirmarDespachoRequest {
  observaciones?: string;
}

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

  async listarDespachosPendientes(): Promise<OrdenDespachoDto[]> {
    return httpClient<OrdenDespachoDto[]>('/despachos/pendientes');
  },

  async obtenerDespachoPorId(id: string): Promise<OrdenDespachoDetalleDto> {
    return httpClient<OrdenDespachoDetalleDto>(`/despachos/${id}`);
  },

  async confirmarDespacho(id: string, req?: ConfirmarDespachoRequest): Promise<DespachoConfirmadoDto> {
    return httpClient<DespachoConfirmadoDto>(`/despachos/${id}/confirmar`, {
      method: 'POST',
      body: JSON.stringify(req || {}),
    });
  },
};
