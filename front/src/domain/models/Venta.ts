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

export interface ConfirmarVentaRequest {
  clienteId?: string;
}
