export interface LoteItem {
  id: string;
  codigo: string;
  productoId: string;
  nombreProducto: string;
  procedencia?: string;
  cantidadInicial: number;
  cantidadActual: number;
  unidad: string;
  fechaIngreso: string;
  ubicacion?: string;
  estado: 'Activo' | 'Agotado' | 'Bloqueado' | 'Archivado';
  observaciones?: string;
}

export interface CreateLotePayload {
  productoId: string;
  codigoPersonalizado?: string;
  procedencia?: string;
  cantidad: number;
  unidad: string;
  fechaIngreso: string;
  ubicacion?: string;
  observaciones?: string;
}

export interface BloquearLotePayload {
  motivo: string;
}
