/**
 * Store reactivo para el módulo de Almacén (Ingresos y Egresos).
 *
 * Conectado con la API real para Ingresos (F4 / MF-04-01).
 * Mantiene mock y fallback para Egresos hasta la implementación de F8/F9.
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

// ─── Datos iniciales / Fallback ───────────────────────────────────

const INGRESOS_FALLBACK: IngresoItem[] = [
  {
    id: '01917f3a-0004-7000-8000-000000000001',
    fecha: '2026/08/15',
    categoria: 'Semillas',
    descripcion: 'SWIETMAC-02608-01',
    tipo: 'Recoleccion',
    cantidad: 50,
    procedencia: 'Bosque Chiquitano - Don Mario',
  },
  {
    id: '01917f3a-0004-7000-8000-000000000002',
    fecha: '2026/08/20',
    categoria: 'Semillas',
    descripcion: 'HANDIMPE-02608-01',
    tipo: 'Compra',
    cantidad: 2.5,
    procedencia: 'Vivero Municipal Santa Cruz',
  },
];

const EGRESOS_MOCK: EgresoItem[] = [
  {
    id: crypto.randomUUID(),
    fecha: '2026/09/02',
    categoria: 'Semillas',
    descripcion: 'Tipuana tipu',
    tipo: 'Venta',
    cantidad: 5,
    consignatario: 'Cliente 1',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2026/09/02',
    categoria: 'Plantas',
    descripcion: 'Pinus canariensis',
    tipo: 'Merma',
    cantidad: 5,
    consignatario: '',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2026/09/02',
    categoria: 'Semillas',
    descripcion: 'Swietenia macrophylla',
    tipo: 'Venta',
    cantidad: 2,
    consignatario: 'Encargado 1',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2026/09/02',
    categoria: 'Servicios',
    descripcion: '-',
    tipo: 'UsoVivero',
    cantidad: null,
    consignatario: '',
  },
];

// ─── Signals ──────────────────────────────────────────────────────

const [ingresos, setIngresos] = createSignal<IngresoItem[]>(INGRESOS_FALLBACK);
const [egresos, setEgresos] = createSignal<EgresoItem[]>(EGRESOS_MOCK);
const [loadingIngresos, setLoadingIngresos] = createSignal(false);
const [loadingEgresos, setLoadingEgresos] = createSignal(false);
const [errorIngresos, setErrorIngresos] = createSignal<string | null>(null);
const [errorEgresos, setErrorEgresos] = createSignal<string | null>(null);

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

// ─── Transformadores y Acciones ───────────────────────────────────

function transformarMovimientoAIngreso(mov: MovimientoInventarioDto): IngresoItem {
  let tipo: TipoIngreso = 'Recoleccion';
  const m = mov.motivo.toLowerCase();
  if (m.includes('compra')) tipo = 'Compra';
  else if (m.includes('devolu')) tipo = 'Devolucion';
  else if (m.includes('intercambio') || m.includes('trueque')) tipo = 'Intercambio';

  const cantTotal = mov.lineas.reduce((acc, l) => acc + l.cantidad, 0);
  const codigos = mov.lineas.map((l) => l.codigoLote).filter(Boolean).join(', ');

  return {
    id: mov.id,
    fecha: mov.fecha.split(' ')[0].replace(/-/g, '/'),
    categoria: 'Semillas',
    descripcion: codigos || mov.contraparteNombre || 'Lote ingresado',
    tipo,
    cantidad: cantTotal > 0 ? cantTotal : null,
    procedencia: mov.contraparteNombre || '',
    observaciones: mov.observaciones,
  };
}

async function cargarIngresos() {
  setLoadingIngresos(true);
  setErrorIngresos(null);
  try {
    const movs = await InventoryUseCases.fetchMovimientos('Entrada');
    if (movs && movs.length > 0) {
      const items = movs.map(transformarMovimientoAIngreso);
      setIngresos(items);
    }
  } catch (err: any) {
    console.warn('[almacenStore] Usando datos locales para ingresos:', err.message);
    setErrorIngresos(err.message || 'Error al conectar con la API de inventario');
  } finally {
    setLoadingIngresos(false);
  }
}

async function registrarIngreso(item: Omit<IngresoItem, 'id'>) {
  setLoadingIngresos(true);
  try {
    const payload: RegistrarIngresoPayload = {
      descripcion: item.descripcion,
      categoria: item.categoria,
      tipoIngreso: item.tipo,
      cantidad: item.cantidad ?? 0,
      unidad: 'Kilogramo',
      procedencia: item.procedencia,
      observaciones: item.observaciones,
      fecha: item.fecha.replace(/\//g, '-'),
    };

    const movResult = await InventoryUseCases.registrarIngreso(payload);
    const nuevoItem = transformarMovimientoAIngreso(movResult);
    setIngresos((prev) => [nuevoItem, ...prev]);
    return nuevoItem;
  } catch (err: any) {
    console.error('[almacenStore] Fallback al guardar ingreso:', err);
    const fallbackItem: IngresoItem = { ...item, id: crypto.randomUUID() };
    setIngresos((prev) => [fallbackItem, ...prev]);
    throw err;
  } finally {
    setLoadingIngresos(false);
  }
}

function transformarMovimientoAEgreso(mov: MovimientoInventarioDto): EgresoItem {
  let tipo: TipoEgreso = 'Venta';
  const m = mov.motivo.toLowerCase();
  if (m.includes('merma')) tipo = 'Merma';
  else if (m.includes('muestra') || m.includes('labor')) tipo = 'UsoLabor';
  else if (m.includes('uso') || m.includes('interno') || m.includes('vivero')) tipo = 'UsoVivero';
  else if (m.includes('truque') || m.includes('intercambio')) tipo = 'Intercambio';

  const cantTotal = mov.lineas.reduce((acc, l) => acc + l.cantidad, 0);

  return {
    id: mov.id,
    fecha: mov.fecha.split(' ')[0].replace(/-/g, '/'),
    categoria: 'Semillas',
    descripcion: mov.lineas.map((l) => l.codigoLote).filter(Boolean).join(', ') || mov.contraparteNombre || 'Lote egresado',
    tipo,
    cantidad: cantTotal > 0 ? cantTotal : null,
    consignatario: mov.contraparteNombre || mov.solicitante || '',
    observaciones: mov.observaciones,
  };
}

async function cargarEgresos() {
  setLoadingEgresos(true);
  setErrorEgresos(null);
  try {
    const movs = await InventoryUseCases.fetchMovimientos('Salida');
    if (movs && movs.length > 0) {
      const items = movs.map(transformarMovimientoAEgreso);
      setEgresos(items);
    }
  } catch (err: any) {
    console.warn('[almacenStore] Usando datos locales para egresos:', err.message);
    setErrorEgresos(err.message || 'Error al conectar con la API de inventario');
  } finally {
    setLoadingEgresos(false);
  }
}

async function registrarEgreso(item: Omit<EgresoItem, 'id'>) {
  setLoadingEgresos(true);
  try {
    const isUsoInterno = item.tipo === 'UsoLabor' || item.tipo === 'UsoVivero';
    const payload: RegistrarEgresoPayload = {
      descripcion: item.descripcion,
      tipoEgreso: item.tipo,
      cantidad: item.cantidad ?? 0,
      unidad: 'Kilogramo',
      contraparteNombre: !isUsoInterno ? item.consignatario : undefined,
      departamento: isUsoInterno ? (item.consignatario || 'Vivero/Laboratorio') : undefined,
      solicitante: isUsoInterno ? 'Responsable de área' : undefined,
      observaciones: item.observaciones,
      fecha: item.fecha.replace(/\//g, '-'),
    };

    const movResult = await InventoryUseCases.registrarEgreso(payload);
    const nuevoItem = transformarMovimientoAEgreso(movResult);
    setEgresos((prev) => [nuevoItem, ...prev]);
    return nuevoItem;
  } catch (err: any) {
    console.error('[almacenStore] Fallback al guardar egreso:', err);
    const fallbackItem: EgresoItem = { ...item, id: crypto.randomUUID() };
    setEgresos((prev) => [fallbackItem, ...prev]);
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
