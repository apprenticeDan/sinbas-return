/**
 * Store reactivo para el módulo de Almacén (Ingresos y Egresos).
 *
 * MOCK: Todos los datos son estáticos y las acciones operan solo en memoria.
 * Cuando se implemente el backend (MF-04-01, MF-08-03), las funciones
 * registrarIngreso/registrarEgreso deben llamar a la API real.
 */

import { createSignal, createMemo } from 'solid-js';
import type {
  IngresoItem,
  EgresoItem,
  CategoriaAlmacen,
  TipoIngreso,
  TipoEgreso,
} from '../../domain/models/Almacen';

// ─── Datos mock iniciales (fieles a los wireframes) ───────────────

const INGRESOS_MOCK: IngresoItem[] = [
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Semillas',
    descripcion: 'Anona cherimoya',
    tipo: 'Compra',
    cantidad: null,
    procedencia: '',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Semillas',
    descripcion: 'Prunus persica',
    tipo: 'Recoleccion',
    cantidad: 5,
    procedencia: '',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Plantas',
    descripcion: 'Puya raymondi',
    tipo: 'Recoleccion',
    cantidad: 2,
    procedencia: '',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Semillas',
    descripcion: 'Casuarina spp',
    tipo: 'Compra',
    cantidad: 4,
    procedencia: '',
  },
];

const EGRESOS_MOCK: EgresoItem[] = [
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Semillas',
    descripcion: 'Tipuana tipu',
    tipo: 'Venta',
    cantidad: 5,
    consignatario: 'Cliente 1',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Plantas',
    descripcion: 'Pinus canariensis',
    tipo: 'Merma',
    cantidad: 5,
    consignatario: '',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Semillas',
    descripcion: 'Swietenia macrophylla',
    tipo: 'Venta',
    cantidad: 2,
    consignatario: 'Encargado 1',
  },
  {
    id: crypto.randomUUID(),
    fecha: '2024/09/02',
    categoria: 'Servicios',
    descripcion: '-',
    tipo: 'UsoVivero',
    cantidad: null,
    consignatario: '',
  },
];

// ─── Signals ──────────────────────────────────────────────────────

const [ingresos, setIngresos] = createSignal<IngresoItem[]>(INGRESOS_MOCK);
const [egresos, setEgresos] = createSignal<EgresoItem[]>(EGRESOS_MOCK);

// Filtros de Ingresos
const [ingresoFiltroCategoria, setIngresoFiltroCategoria] = createSignal<CategoriaAlmacen | ''>('');
const [ingresoFiltroTipo, setIngresoFiltroTipo] = createSignal<TipoIngreso | ''>('');
const [ingresoFiltroSearch, setIngresoFiltroSearch] = createSignal('');

// Filtros de Egresos
const [egresoFiltroCategoria, setEgresoFiltroCategoria] = createSignal<CategoriaAlmacen | ''>('');
const [egresoFiltroTipo, setEgresoFiltroTipo] = createSignal<TipoEgreso | ''>('');
const [egresoFiltroSearch, setEgresoFiltroSearch] = createSignal('');

// ─── Computed (filtrados) ─────────────────────────────────────────

const filteredIngresos = createMemo(() => {
  let list = ingresos();
  const cat = ingresoFiltroCategoria();
  const tipo = ingresoFiltroTipo();
  const search = ingresoFiltroSearch().toLowerCase();

  if (cat) list = list.filter((i) => i.categoria === cat);
  if (tipo) list = list.filter((i) => i.tipo === tipo);
  if (search) list = list.filter((i) => i.descripcion.toLowerCase().includes(search));

  return list;
});

const filteredEgresos = createMemo(() => {
  let list = egresos();
  const cat = egresoFiltroCategoria();
  const tipo = egresoFiltroTipo();
  const search = egresoFiltroSearch().toLowerCase();

  if (cat) list = list.filter((e) => e.categoria === cat);
  if (tipo) list = list.filter((e) => e.tipo === tipo);
  if (search) list = list.filter((e) => e.descripcion.toLowerCase().includes(search));

  return list;
});

// ─── Acciones mock ────────────────────────────────────────────────

function registrarIngreso(item: Omit<IngresoItem, 'id'>) {
  // TODO: Reemplazar con llamada a POST /api/inventario/ingresos (MF-04-01)
  setIngresos((prev) => [{ ...item, id: crypto.randomUUID() }, ...prev]);
}

function registrarEgreso(item: Omit<EgresoItem, 'id'>) {
  // TODO: Reemplazar con llamada a POST /api/inventario/salidas (MF-08-03)
  setEgresos((prev) => [{ ...item, id: crypto.randomUUID() }, ...prev]);
}

// ─── API pública ──────────────────────────────────────────────────

export const almacenStore = {
  // Signals
  ingresos,
  egresos,
  filteredIngresos,
  filteredEgresos,

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
  registrarIngreso,
  registrarEgreso,
};
