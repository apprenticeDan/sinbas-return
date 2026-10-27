import { createSignal, createMemo } from 'solid-js';
import { SolicitudUseCases } from '../../application/usecases/SolicitudUseCases';
import type {
  SolicitudDto,
  CrearSolicitudRequest,
  EstadoSolicitudInterna,
} from '../../domain/models/Solicitud';

const [solicitudes, setSolicitudes] = createSignal<SolicitudDto[]>([]);
const [loading, setLoading] = createSignal(false);
const [error, setError] = createSignal<string | null>(null);
const [filtroEstado, setFiltroEstado] = createSignal<EstadoSolicitudInterna | ''>('');
const [filtroTipo, setFiltroTipo] = createSignal<string>('');
const [filtroSearch, setFiltroSearch] = createSignal('');

const filteredSolicitudes = createMemo(() => {
  const list = solicitudes();
  const est = filtroEstado();
  const tipo = filtroTipo();
  const q = filtroSearch().trim().toLowerCase();

  return list.filter((s) => {
    if (est && s.estado !== est) return false;
    if (tipo && s.tipo !== tipo) return false;
    if (q) {
      const hay =
        (s.nombreProducto || '').toLowerCase().includes(q) ||
        (s.codigoLote || '').toLowerCase().includes(q) ||
        s.motivo.toLowerCase().includes(q) ||
        s.id.toLowerCase().includes(q);
      if (!hay) return false;
    }
    return true;
  });
});

const stats = createMemo(() => {
  const list = solicitudes();
  return {
    total: list.length,
    pendientes: list.filter((s) => s.estado === 'Pendiente').length,
    aprobadas: list.filter((s) => s.estado === 'Aprobada').length,
    confirmadas: list.filter((s) => s.estado === 'Confirmada').length,
  };
});

export const solicitudStore = {
  solicitudes,
  loading,
  error,
  filtroEstado,
  filtroTipo,
  filtroSearch,
  filteredSolicitudes,
  stats,

  setFiltroEstado,
  setFiltroTipo,
  setFiltroSearch,

  async cargarSolicitudes() {
    setLoading(true);
    setError(null);
    try {
      const data = await SolicitudUseCases.listar();
      setSolicitudes(data);
    } catch (e: any) {
      setError(e.message || 'Error al cargar solicitudes');
    } finally {
      setLoading(false);
    }
  },

  async crearSolicitud(req: CrearSolicitudRequest): Promise<{ ok: boolean; error?: string; data?: SolicitudDto }> {
    try {
      const data = await SolicitudUseCases.crear(req);
      setSolicitudes((prev) => [data, ...prev]);
      return { ok: true, data };
    } catch (e: any) {
      return { ok: false, error: e.message || 'Error al crear solicitud' };
    }
  },

  async aprobarSolicitud(id: string): Promise<{ ok: boolean; error?: string; data?: SolicitudDto }> {
    try {
      const data = await SolicitudUseCases.aprobar(id);
      setSolicitudes((prev) => prev.map((s) => (s.id === id ? data : s)));
      return { ok: true, data };
    } catch (e: any) {
      return { ok: false, error: e.message || 'Error al aprobar' };
    }
  },

  async rechazarSolicitud(id: string, motivo: string): Promise<{ ok: boolean; error?: string; data?: SolicitudDto }> {
    try {
      const data = await SolicitudUseCases.rechazar(id, motivo);
      setSolicitudes((prev) => prev.map((s) => (s.id === id ? data : s)));
      return { ok: true, data };
    } catch (e: any) {
      return { ok: false, error: e.message || 'Error al rechazar' };
    }
  },

  async confirmarSolicitud(id: string): Promise<{ ok: boolean; error?: string; data?: SolicitudDto }> {
    try {
      const data = await SolicitudUseCases.confirmar(id);
      setSolicitudes((prev) => prev.map((s) => (s.id === id ? data : s)));
      return { ok: true, data };
    } catch (e: any) {
      return { ok: false, error: e.message || 'Error al confirmar' };
    }
  },
};
