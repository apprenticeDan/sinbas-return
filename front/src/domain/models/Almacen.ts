/**
 * Tipos de dominio para el módulo de Almacén (Ingresos y Egresos).
 *
 * Mock UI — estos tipos modelan la estructura visual de los wireframes
 * y serán refinados cuando se implemente el backend (F4 / F8 / F9).
 */

export type CategoriaAlmacen = 'Semillas' | 'Plantas' | 'Insumos' | 'Servicios';

export type TipoIngreso = 'Compra' | 'Recoleccion' | 'Intercambio' | 'Devolucion';

export type TipoEgreso = 'Venta' | 'Merma' | 'UsoLabor' | 'UsoVivero' | 'Intercambio';

export interface IngresoItem {
  id: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoIngreso;
  cantidad: number | null;
  procedencia: string;
  observaciones?: string;
}

export interface EgresoItem {
  id: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoEgreso;
  cantidad: number | null;
  consignatario?: string;
  costoAdicional?: number;
  observaciones?: string;
}

/** Etiquetas amigables para los tipos de ingreso */
export const TIPOS_INGRESO: { value: TipoIngreso; label: string }[] = [
  { value: 'Compra', label: 'Compra' },
  { value: 'Recoleccion', label: 'Recolección' },
  { value: 'Intercambio', label: 'Intercambio' },
  { value: 'Devolucion', label: 'Devolución' },
];

/** Etiquetas amigables para los tipos de egreso */
export const TIPOS_EGRESO: { value: TipoEgreso; label: string }[] = [
  { value: 'Venta', label: 'Venta' },
  { value: 'Merma', label: 'Merma' },
  { value: 'UsoLabor', label: 'Uso Labor' },
  { value: 'UsoVivero', label: 'Uso Vivero' },
  { value: 'Intercambio', label: 'Intercambio' },
];

/** Categorías disponibles */
export const CATEGORIAS: { value: CategoriaAlmacen; label: string; abbr: string }[] = [
  { value: 'Semillas', label: 'Semillas', abbr: 'Sem' },
  { value: 'Plantas', label: 'Plantas', abbr: 'Plan' },
  { value: 'Insumos', label: 'Insumos', abbr: 'Ins' },
  { value: 'Servicios', label: 'Servicios', abbr: 'Servi' },
];

/** Descripciones de productos mock, agrupadas por categoría */
export const DESCRIPCIONES_POR_CATEGORIA: Record<CategoriaAlmacen, string[]> = {
  Semillas: ['Anona cherimoya', 'Prunus persica', 'Swietenia macrophylla', 'Casuarina spp', 'Pinus canariensis'],
  Plantas: ['Pinus canariensis', 'Puya raymondi', 'Tipuana tipu', 'Jacaranda mimosifolia'],
  Insumos: ['Sustrato orgánico', 'Fertilizante NPK', 'Fungicida sistémico'],
  Servicios: ['Servicio laboratorio', 'Transporte semillas'],
};

/** Consignatarios mock para egresos */
export const CONSIGNATARIOS_MOCK = [
  'Cliente 1',
  'Encargado 1',
  'Auxiliar',
  'Uso interno',
];
