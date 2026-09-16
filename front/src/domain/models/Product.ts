export type Category = 'Semilla' | 'Plantin' | 'Insumo';
export type Traceability = 'PorLote' | 'Simple';
export type CommercialState = 'PendientePrecioBorrador' | 'ActivoParaVenta' | 'Inactivo';

export type UnidadMedida = 'Kilogramo' | 'Gramo' | 'Mililitro' | 'Litro' | 'UnidadDiscreta';

export interface Product {
  id: string;
  nombreVisible: string;
  categoria: Category;
  genero?: string;
  epiteto?: string;
  nombresComunes: string[];
  unidadManejo: UnidadMedida | string;
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
  unidadManejo: UnidadMedida | string;
  gramosNominales?: number;
  trazabilidad: Traceability;
  observaciones?: string;
}

export interface AssignPricePayload {
  productoId: string;
  monto: number;
  moneda?: string;
}
