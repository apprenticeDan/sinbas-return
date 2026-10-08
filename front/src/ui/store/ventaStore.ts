import { createSignal } from 'solid-js';
import {
  ApiVentaGateway,
  type OrdenVentaDto,
  type OrdenDespachoDto,
  type OrdenDespachoDetalleDto,
  type VentaConfirmadaDto,
  type DespachoConfirmadoDto,
} from '../../infrastructure/api/ApiVentaGateway';

const [ventas, setVentas] = createSignal<OrdenVentaDto[]>([]);
const [despachosPendientes, setDespachosPendientes] = createSignal<OrdenDespachoDto[]>([]);
const [despachoSeleccionado, setDespachoSeleccionado] = createSignal<OrdenDespachoDetalleDto | null>(null);
const [loadingVentas, setLoadingVentas] = createSignal(false);
const [loadingDespachos, setLoadingDespachos] = createSignal(false);
const [error, setError] = createSignal<string | null>(null);

export const ventaStore = {
  ventas,
  despachosPendientes,
  despachoSeleccionado,
  loadingVentas,
  loadingDespachos,
  error,

  async cargarVentas(filtros?: { clienteId?: string; estado?: string; desde?: string; hasta?: string }) {
    setLoadingVentas(true);
    setError(null);
    try {
      const data = await ApiVentaGateway.listarVentas(filtros);
      setVentas(data);
    } catch (err: any) {
      setError(err.message || 'Error al cargar listado de ventas');
    } finally {
      setLoadingVentas(false);
    }
  },

  async cargarDespachosPendientes() {
    setLoadingDespachos(true);
    setError(null);
    try {
      const data = await ApiVentaGateway.listarDespachosPendientes();
      setDespachosPendientes(data);
    } catch (err: any) {
      setError(err.message || 'Error al cargar despachos pendientes');
    } finally {
      setLoadingDespachos(false);
    }
  },

  async cargarDespachoDetalle(id: string): Promise<OrdenDespachoDetalleDto | null> {
    setError(null);
    try {
      const data = await ApiVentaGateway.obtenerDespachoPorId(id);
      setDespachoSeleccionado(data);
      return data;
    } catch (err: any) {
      setError(err.message || 'Error al cargar detalle del despacho');
      return null;
    }
  },

  async confirmarVenta(proformaId: string, clienteId?: string): Promise<VentaConfirmadaDto> {
    setError(null);
    try {
      const result = await ApiVentaGateway.confirmarVenta(proformaId, { clienteId });
      // Añadir la venta a la lista o refrescar
      await ventaStore.cargarVentas();
      return result;
    } catch (err: any) {
      setError(err.message || 'Error al confirmar la venta');
      throw err;
    }
  },

  async confirmarDespacho(id: string, observaciones?: string): Promise<DespachoConfirmadoDto> {
    setError(null);
    try {
      const result = await ApiVentaGateway.confirmarDespacho(id, { observaciones });
      // Remover de despachos pendientes y recargar
      await ventaStore.cargarDespachosPendientes();
      await ventaStore.cargarVentas();
      return result;
    } catch (err: any) {
      setError(err.message || 'Error al confirmar despacho físico');
      throw err;
    }
  },

  limpiarSeleccion() {
    setDespachoSeleccionado(null);
  },
};
