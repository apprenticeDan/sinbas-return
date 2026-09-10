import { StockLoteItem } from './Almacen';

export type NivelAlertaStock = 'SinStock' | 'BajoStock' | 'StockNormal';

export interface StockConsolidadoProducto {
  productoId: string;
  nombreProducto: string;
  categoria: string;
  unidadMedida: string;
  stockTotalGramos: number;
  stockTotalDisplay: number;
  stockDisponibleVentaGramos: number;
  stockDisponibleVentaDisplay: number;
  alerta: NivelAlertaStock;
  lotes: StockLoteItem[];
}

export interface KardexItem {
  movimientoId: string;
  fecha: string;
  tipo: 'Entrada' | 'Salida';
  motivo: string;
  productoId: string;
  nombreProducto: string;
  loteId: string;
  codigoLote: string;
  cantidad: number;
  unidad: string;
  saldoResultanteGramos: number;
  saldoResultanteDisplay: number;
  responsableId: string;
  observaciones: string;
}
