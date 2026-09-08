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

// ─────────────────────────────────────────────────────────────
// DTOs de Conexión Backend Real (F4 / F5 / F8 / F9)
// ─────────────────────────────────────────────────────────────

export interface RegistrarIngresoPayload {
  productoId?: string;
  loteId?: string;
  descripcion?: string;
  categoria?: string;
  tipoIngreso: string;
  cantidad: number;
  unidad?: string;
  procedencia?: string;
  observaciones?: string;
  fecha?: string;
}

/** Payload para POST /api/inventario/egreso (F8 / MF-08-03) */
export interface RegistrarEgresoPayload {
  /** UUID del producto. Vacío = resolución por descripción. */
  productoId?: string;
  descripcion?: string;
  /** Venta | Merma | UsoLabor | UsoVivero | Intercambio */
  tipoEgreso: string;
  cantidad: number;
  unidad?: string;
  /** Nombre del cliente (Venta). Extensible a ClienteId con F6. */
  contraparteNombre?: string;
  /** Departamento solicitante (Uso Interno / F9). */
  departamento?: string;
  /** Nombre del solicitante interno (F9). */
  solicitante?: string;
  observaciones?: string;
  fecha?: string;
}

export interface LineaMovimientoDto {
  loteId: string;
  codigoLote: string;
  cantidad: number;
  unidad: string;
}

export interface MovimientoInventarioDto {
  id: string;
  fecha: string;
  responsableId: string;
  tipo: string;
  motivo: string;
  contraparteNombre: string;
  departamento: string;
  solicitante: string;
  observaciones: string;
  lineas: LineaMovimientoDto[];
}

export interface StockLoteItem {
  loteId: string;
  codigo: string;
  productoId: string;
  nombreProducto: string;
  fechaIngreso: string;
  estado: string;
  stockGramos: number;
  stockDisplay: number;
  unidad: string;
}

export interface StockProductoDto {
  productoId: string;
  nombreProducto: string;
  stockTotalGramos: number;
  stockDisponibleVentaGramos: number;
  lotes: StockLoteItem[];
}

// ─────────────────────────────────────────────────────────────
// EXTENSIBILITY NOTES:
// 1. Clientes (F6): La búsqueda de destinatario/consignatario en egresos
//    debe permitir filtro multivariable por nombre, apellido, NIT, teléfono y email.
// 2. Uso Interno (F9): Egresos internos registrarán departamento/área y solicitante.
// ─────────────────────────────────────────────────────────────

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
