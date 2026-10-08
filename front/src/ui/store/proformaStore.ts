/**
 * Store reactivo para el módulo Comercial: Proformas y Cotizaciones (Feature F7).
 */

import { createSignal } from 'solid-js';
import {
  ApiProformaGateway,
  type ProformaDto,
  type CrearProformaRequest,
  type StockDisponibleDto,
} from '../../infrastructure/api/ApiProformaGateway';
import { ApiClientGateway, type ClienteDto } from '../../infrastructure/api/ApiClientGateway';
import { CatalogUseCases } from '../../application/usecases/CatalogUseCases';
import type { Product } from '../../domain/models/Product';

// ─── Signals ──────────────────────────────────────────────────────

const [proformas, setProformas] = createSignal<ProformaDto[]>([]);
const [loadingProformas, setLoadingProformas] = createSignal(false);
const [errorProformas, setErrorProformas] = createSignal<string | null>(null);
const [selectedProforma, setSelectedProforma] = createSignal<ProformaDto | null>(null);

const [filtroEstado, setFiltroEstado] = createSignal('');
const [filtroSearch, setFiltroSearch] = createSignal('');

const [clientesCache, setClientesCache] = createSignal<ClienteDto[]>([]);
const [productosCache, setProductosCache] = createSignal<Product[]>([]);
const [stockCache, setStockCache] = createSignal<Record<string, StockDisponibleDto>>({});

// ─── Computed ─────────────────────────────────────────────────────

const filteredProformas = () => {
  let list = proformas();
  const estado = filtroEstado();
  const search = filtroSearch().toLowerCase().trim();

  if (estado) {
    list = list.filter((p) => p.estadoProyectado === estado || p.estado === estado);
  }
  if (search) {
    list = list.filter((p) =>
      p.clienteNombre.toLowerCase().includes(search) ||
      p.id.toLowerCase().includes(search) ||
      (p.observaciones && p.observaciones.toLowerCase().includes(search))
    );
  }
  return list;
};

// ─── Actions ──────────────────────────────────────────────────────

async function cargarProformas() {
  setLoadingProformas(true);
  setErrorProformas(null);
  try {
    const list = await ApiProformaGateway.listarProformas();
    setProformas(list);
  } catch (err: any) {
    setErrorProformas(err.message || 'Error al cargar las proformas');
  } finally {
    setLoadingProformas(false);
  }
}

async function cargarAuxiliares() {
  try {
    const [clientes, productos] = await Promise.all([
      ApiClientGateway.listarClientes(),
      CatalogUseCases.fetchCatalog(),
    ]);
    setClientesCache(clientes.filter((c: ClienteDto) => c.estado === 'Activo'));
    // RN16: Solo productos activos para venta con precio oficial
    setProductosCache(
      productos.filter((p: Product) => p.activo && p.estadoComercial === 'ActivoParaVenta' && p.precioOficial && p.precioOficial > 0)
    );
  } catch (err: any) {
    console.error('Error al cargar clientes o catálogo para proformas:', err);
  }
}

async function consultarStock(productoId: string): Promise<StockDisponibleDto | null> {
  if (!productoId) return null;
  const current = stockCache();
  if (current[productoId]) return current[productoId];

  try {
    const stockInfo = await ApiProformaGateway.obtenerStockDisponible(productoId);
    setStockCache((prev) => ({ ...prev, [productoId]: stockInfo }));
    return stockInfo;
  } catch (err) {
    console.error(`Error al consultar stock del producto ${productoId}:`, err);
    return null;
  }
}

async function crearProforma(req: CrearProformaRequest): Promise<{ ok: boolean; data?: ProformaDto; error?: string }> {
  try {
    const nueva = await ApiProformaGateway.crearProforma(req);
    setProformas((prev) => [nueva, ...prev]);
    return { ok: true, data: nueva };
  } catch (err: any) {
    return { ok: false, error: err.message || 'Error al crear la proforma' };
  }
}

async function anularProforma(id: string, motivo: string): Promise<{ ok: boolean; data?: ProformaDto; error?: string }> {
  try {
    const anulada = await ApiProformaGateway.anularProforma(id, { motivo });
    setProformas((prev) => prev.map((p) => (p.id === id ? anulada : p)));
    if (selectedProforma()?.id === id) {
      setSelectedProforma(anulada);
    }
    return { ok: true, data: anulada };
  } catch (err: any) {
    return { ok: false, error: err.message || 'Error al anular la proforma' };
  }
}

export const proformaStore = {
  proformas,
  filteredProformas,
  loadingProformas,
  errorProformas,
  selectedProforma,
  setSelectedProforma,
  filtroEstado,
  setFiltroEstado,
  filtroSearch,
  setFiltroSearch,
  clientesCache,
  productosCache,
  stockCache,
  cargarProformas,
  cargarAuxiliares,
  consultarStock,
  crearProforma,
  anularProforma,
};
