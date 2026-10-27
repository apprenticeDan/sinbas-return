export interface LineaCotizadaDto {
  productoId: string;
  nombreProducto: string;
  cantidad: number;
  unidad: string;
  precioUnitario: number;
  subtotal: number;
  precioVigente?: number | null;
  stockDisponibleActual?: number | null;
  stockSuficiente?: boolean | null;
  stockUnidad?: string | null;
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
  puedeConfirmarse?: boolean | null;
  advertencias?: string[];
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
