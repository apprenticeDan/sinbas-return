export type Category = 'Semilla' | 'Plantin' | 'Insumo' | 'Otro';
export type Traceability = 'PorLote' | 'Simple';
export type CommercialState = 'PendientePrecioBorrador' | 'ActivoParaVenta' | 'Inactivo';

export interface Product {
  id: string;
  nombreVisible: string;
  categoria: Category;
  genero?: string;
  epiteto?: string;
  nombresComunes: string[];
  unidadManejo: string;
  trazabilidad: Traceability;
  precioOficial?: number;
  moneda?: string;
  estadoComercial: CommercialState;
  activo: boolean;
  esAptoParaVenta: boolean;
  observaciones?: string;
}

export interface CreateProductPayload {
  categoria: Category;
  genero?: string;
  epiteto?: string;
  observacionesNc?: string;
  nombresComunes: string[];
  etapaDesarrollo?: string;
  nombreInsumo?: string;
  marcaInsumo?: string;
  descripcionInsumo?: string;
  unidadManejo: string;
  gramosNominales?: number;
  trazabilidad: Traceability;
  observaciones?: string;
}

export interface AssignPricePayload {
  productoId: string;
  monto: number;
  moneda?: string;
}
