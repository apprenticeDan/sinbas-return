/**
 * Tipos de dominio para el módulo de Almacén (Ingresos y Egresos).
 *
 * Conectado con el backend real para Ingresos (F4), Egresos (F8/F9)
 * y el catálogo oficial de productos (F1).
 */

// Categorías alineadas con el backend (Sinbas.Domain/Producto.fs)
export type CategoriaAlmacen = 'Semilla' | 'Plantin' | 'Insumo';

export type TipoIngreso = 'Compra' | 'Recoleccion' | 'Intercambio' | 'Devolucion';

export type TipoEgreso = 'Venta' | 'Merma' | 'UsoLabor' | 'UsoVivero' | 'Intercambio';

export interface IngresoItem {
  id: string;
  productoId?: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoIngreso;
  cantidad: number | null;
  unidad?: string;
  procedencia: string;
  observaciones?: string;
}

export interface EgresoItem {
  id: string;
  productoId?: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoEgreso;
  cantidad: number | null;
  unidad?: string;
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
  procedencia?: string;
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

/** Categorías disponibles — alineadas con el backend (Semilla, Plantin, Insumo) */
export const CATEGORIAS: { value: CategoriaAlmacen; label: string; abbr: string }[] = [
  { value: 'Semilla', label: 'Semillas', abbr: 'Sem' },
  { value: 'Plantin', label: 'Plantines', abbr: 'Plan' },
  { value: 'Insumo', label: 'Insumos', abbr: 'Ins' },
];

/** Consignatarios sugeridos para egresos (extensible con F6 Clientes) */
export const CONSIGNATARIOS_MOCK = [
  'Cliente 1',
  'Encargado 1',
  'Auxiliar',
  'Uso interno',
];
