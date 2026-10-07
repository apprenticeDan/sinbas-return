/**
 * Store reactivo para el módulo de Almacén (Ingresos y Egresos).
 *
 * Conectado con la API real para Ingresos (F4 / MF-04-01) y Egresos (F8 / F9).
 * Las listas de productos se obtienen del catálogo real (F1).
 */

import { createSignal } from 'solid-js';
import type {
  IngresoItem,
  EgresoItem,
  CategoriaAlmacen,
  TipoIngreso,
  TipoEgreso,
  RegistrarIngresoPayload,
  RegistrarEgresoPayload,
  MovimientoInventarioDto,
} from '../../domain/models/Almacen';
import { InventoryUseCases } from '../../application/usecases/InventoryUseCases';
import { CatalogUseCases } from '../../application/usecases/CatalogUseCases';
import { ApiClientGateway, type ClienteDto } from '../../infrastructure/api/ApiClientGateway';
import type { Product } from '../../domain/models/Product';

// ─── Signals ──────────────────────────────────────────────────────

const [ingresos, setIngresos] = createSignal<IngresoItem[]>([]);
const [egresos, setEgresos] = createSignal<EgresoItem[]>([]);
const [loadingIngresos, setLoadingIngresos] = createSignal(false);
const [loadingEgresos, setLoadingEgresos] = createSignal(false);
const [errorIngresos, setErrorIngresos] = createSignal<string | null>(null);
const [errorEgresos, setErrorEgresos] = createSignal<string | null>(null);

// Catálogo de productos cargado para resolver categorías y nombres
const [productosCache, setProductosCache] = createSignal<Product[]>([]);
// Clientes registrados en F6 para autocomplete y selección en egresos / ventas
const [clientesCache, setClientesCache] = createSignal<ClienteDto[]>([]);

// Filtros de Ingresos
const [ingresoFiltroCategoria, setIngresoFiltroCategoria] = createSignal<CategoriaAlmacen | ''>('');
const [ingresoFiltroTipo, setIngresoFiltroTipo] = createSignal<TipoIngreso | ''>('');
const [ingresoFiltroSearch, setIngresoFiltroSearch] = createSignal('');

// Filtros de Egresos
const [egresoFiltroCategoria, setEgresoFiltroCategoria] = createSignal<CategoriaAlmacen | ''>('');
const [egresoFiltroTipo, setEgresoFiltroTipo] = createSignal<TipoEgreso | ''>('');
const [egresoFiltroSearch, setEgresoFiltroSearch] = createSignal('');

// ─── Computed (filtrados) ─────────────────────────────────────────

const filteredIngresos = () => {
  let list = ingresos();
  const cat = ingresoFiltroCategoria();
  const tipo = ingresoFiltroTipo();
  const search = ingresoFiltroSearch().toLowerCase();

  if (cat) list = list.filter((i) => i.categoria === cat);
  if (tipo) list = list.filter((i) => i.tipo === tipo);
  if (search) list = list.filter((i) => i.descripcion.toLowerCase().includes(search));

  return list;
};

const filteredEgresos = () => {
  let list = egresos();
  const cat = egresoFiltroCategoria();
  const tipo = egresoFiltroTipo();
  const search = egresoFiltroSearch().toLowerCase();

  if (cat) list = list.filter((e) => e.categoria === cat);
  if (tipo) list = list.filter((e) => e.tipo === tipo);
  if (search) {
    list = list.filter((e) =>
      e.descripcion.toLowerCase().includes(search) ||
      (e.consignatario && e.consignatario.toLowerCase().includes(search))
    );
  }

  return list;
};

// ─── Helpers ──────────────────────────────────────────────────────

/** Carga el catálogo de productos si no está en cache */
async function asegurarCatalogoCargado(): Promise<Product[]> {
  let prods = productosCache();
  if (prods.length === 0) {
    try {
      prods = await CatalogUseCases.fetchCatalog();
      setProductosCache(prods);
    } catch (err) {
      console.warn('[almacenStore] No se pudo cargar el catálogo:', err);
    }
  }
  return prods;
}

/** Carga los clientes registrados si no están en cache */
async function asegurarClientesCargados(): Promise<ClienteDto[]> {
  let clis = clientesCache();
  if (clis.length === 0) {
    try {
      clis = await ApiClientGateway.listarClientes();
      setClientesCache(clis);
    } catch (err) {
      console.warn('[almacenStore] No se pudo cargar clientes:', err);
    }
  }
  return clis;
}

/** Resuelve la categoría de un producto por su nombre visible */
function resolverCategoria(nombreProducto: string, productos: Product[]): CategoriaAlmacen {
  const prod = productos.find(
    (p) => p.nombreVisible.toLowerCase() === nombreProducto.toLowerCase()
  );
  if (prod) return prod.categoria as CategoriaAlmacen;
  return 'Semilla'; // default razonable para BASFOR
}

// ─── Transformadores ──────────────────────────────────────────────

function transformarMovimientoAIngreso(mov: MovimientoInventarioDto, productos: Product[]): IngresoItem {
  let tipo: TipoIngreso = 'Recoleccion';
  const m = mov.motivo.toLowerCase();
  if (m.includes('compra')) tipo = 'Compra';
  else if (m.includes('devolu')) tipo = 'Devolucion';
  else if (m.includes('intercambio') || m.includes('trueque')) tipo = 'Intercambio';

  const cantTotal = mov.lineas.reduce((acc, l) => acc + l.cantidad, 0);
  const codigos = mov.lineas.map((l) => l.codigoLote).filter(Boolean).join(', ');
  const unidad = mov.lineas.length > 0 ? mov.lineas[0].unidad : undefined;

  // Intentar resolver la categoría real del producto
  const descripcion = codigos || mov.contraparteNombre || 'Lote ingresado';
  const categoria = resolverCategoria(descripcion, productos);

  return {
    id: mov.id,
    fecha: mov.fecha.split(' ')[0].replace(/-/g, '/'),
    categoria,
    descripcion,
    tipo,
    cantidad: cantTotal > 0 ? cantTotal : null,
    unidad,
    procedencia: mov.contraparteNombre || '',
    observaciones: mov.observaciones,
  };
}

function transformarMovimientoAEgreso(mov: MovimientoInventarioDto, productos: Product[]): EgresoItem {
  let tipo: TipoEgreso = 'Venta';
  const m = mov.motivo.toLowerCase();
  if (m.includes('merma')) tipo = 'Merma';
  else if (m.includes('muestra') || m.includes('labor')) tipo = 'UsoLabor';
  else if (m.includes('uso') || m.includes('interno') || m.includes('vivero')) tipo = 'UsoVivero';
  else if (m.includes('trueque') || m.includes('intercambio')) tipo = 'Intercambio';

  const cantTotal = mov.lineas.reduce((acc, l) => acc + l.cantidad, 0);
  const codigos = mov.lineas.map((l) => l.codigoLote).filter(Boolean).join(', ');
  const unidad = mov.lineas.length > 0 ? mov.lineas[0].unidad : undefined;
  const descripcion = codigos || mov.contraparteNombre || 'Lote egresado';
  const categoria = resolverCategoria(descripcion, productos);

  return {
    id: mov.id,
    fecha: mov.fecha.split(' ')[0].replace(/-/g, '/'),
    categoria,
    descripcion,
    tipo,
    cantidad: cantTotal > 0 ? cantTotal : null,
    unidad,
    consignatario: mov.contraparteNombre || mov.solicitante || '',
    observaciones: mov.observaciones,
  };
}

// ─── Acciones ─────────────────────────────────────────────────────

async function cargarIngresos() {
  setLoadingIngresos(true);
  setErrorIngresos(null);
  try {
    const productos = await asegurarCatalogoCargado();
    const movs = await InventoryUseCases.fetchMovimientos('Entrada');
    const items = (movs || []).map((m) => transformarMovimientoAIngreso(m, productos));
    setIngresos(items);
  } catch (err: any) {
    console.warn('[almacenStore] Error cargando ingresos:', err.message);
    setErrorIngresos(err.message || 'Error al conectar con la API de inventario');
  } finally {
    setLoadingIngresos(false);
  }
}

interface RegistrarIngresoParams {
  productoId: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoIngreso;
  cantidad: number;
  unidad: string;
  procedencia: string;
  observaciones?: string;
}

async function registrarIngreso(item: RegistrarIngresoParams) {
  setLoadingIngresos(true);
  try {
    const payload: RegistrarIngresoPayload = {
      productoId: item.productoId,
      descripcion: item.descripcion,
      categoria: item.categoria,
      tipoIngreso: item.tipo,
      cantidad: item.cantidad,
      unidad: item.unidad || 'Kilogramo',
      procedencia: item.procedencia,
      observaciones: item.observaciones,
      fecha: item.fecha.replace(/\//g, '-'),
    };

    const movResult = await InventoryUseCases.registrarIngreso(payload);
    const productos = productosCache();
    const nuevoItem = transformarMovimientoAIngreso(movResult, productos);
    setIngresos((prev) => [nuevoItem, ...prev]);
    return nuevoItem;
  } catch (err: any) {
    console.error('[almacenStore] Error al registrar ingreso:', err);
    throw err;
  } finally {
    setLoadingIngresos(false);
  }
}

async function cargarEgresos() {
  setLoadingEgresos(true);
  setErrorEgresos(null);
  try {
    const productos = await asegurarCatalogoCargado();
    const movs = await InventoryUseCases.fetchMovimientos('Salida');
    const items = (movs || []).map((m) => transformarMovimientoAEgreso(m, productos));
    setEgresos(items);
  } catch (err: any) {
    console.warn('[almacenStore] Error cargando egresos:', err.message);
    setErrorEgresos(err.message || 'Error al conectar con la API de inventario');
  } finally {
    setLoadingEgresos(false);
  }
}

interface RegistrarEgresoParams {
  productoId: string;
  fecha: string;
  categoria: CategoriaAlmacen;
  descripcion: string;
  tipo: TipoEgreso;
  cantidad: number;
  unidad: string;
  consignatario?: string;
  costoAdicional?: number;
  observaciones?: string;
}

async function registrarEgreso(item: RegistrarEgresoParams) {
  setLoadingEgresos(true);
  try {
    const isUsoInterno = item.tipo === 'UsoLabor' || item.tipo === 'UsoVivero';
    const payload: RegistrarEgresoPayload = {
      productoId: item.productoId,
      descripcion: item.descripcion,
      tipoEgreso: item.tipo,
      cantidad: item.cantidad,
      unidad: item.unidad || 'Kilogramo',
      contraparteNombre: !isUsoInterno ? item.consignatario : undefined,
      departamento: isUsoInterno ? (item.consignatario || 'Vivero/Laboratorio') : undefined,
      solicitante: isUsoInterno ? 'Responsable de área' : undefined,
      observaciones: item.observaciones,
      fecha: item.fecha.replace(/\//g, '-'),
    };

    const movResult = await InventoryUseCases.registrarEgreso(payload);
    const productos = productosCache();
    const nuevoItem = transformarMovimientoAEgreso(movResult, productos);
    setEgresos((prev) => [nuevoItem, ...prev]);
    return nuevoItem;
  } catch (err: any) {
    console.error('[almacenStore] Error al registrar egreso:', err);
    throw err;
  } finally {
    setLoadingEgresos(false);
  }
}

// ─── API pública ──────────────────────────────────────────────────

export const almacenStore = {
  // Signals
  ingresos,
  egresos,
  filteredIngresos,
  filteredEgresos,
  loadingIngresos,
  loadingEgresos,
  errorIngresos,
  errorEgresos,

  // Catálogo de productos
  productosCache,
  asegurarCatalogoCargado,

  // Clientes registrados
  clientesCache,
  asegurarClientesCargados,

  // Filtros Ingresos
  ingresoFiltroCategoria,
  setIngresoFiltroCategoria,
  ingresoFiltroTipo,
  setIngresoFiltroTipo,
  ingresoFiltroSearch,
  setIngresoFiltroSearch,

  // Filtros Egresos
  egresoFiltroCategoria,
  setEgresoFiltroCategoria,
  egresoFiltroTipo,
  setEgresoFiltroTipo,
  egresoFiltroSearch,
  setEgresoFiltroSearch,

  // Acciones
  cargarIngresos,
  cargarEgresos,
  registrarIngreso,
  registrarEgreso,
};
