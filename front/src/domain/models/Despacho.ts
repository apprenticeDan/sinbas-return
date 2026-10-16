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

export interface DespachoConfirmadoDto {
  despachoId: string;
  codigoDespacho: string;
  movimientoId: string;
  fecha: string;
  lineasDespachadas: LineaDespachadaDto[];
  mensaje: string;
}

export interface ConfirmarDespachoRequest {
  observaciones?: string;
}
