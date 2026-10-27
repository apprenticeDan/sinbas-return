// ─────────────────────────────────────────────────────────────
// Fase 6b: Modelos TypeScript para Solicitud de Egreso Interno
// Reflejo exacto del SolicitudDto del backend (SolicitudService.fs)
// ─────────────────────────────────────────────────────────────

export type TipoEgresoInterno = 'UsoInterno' | 'Ajuste' | 'Merma';
export type SignoEgresoInterno = 'Positivo' | 'Negativo';
export type EstadoSolicitudInterna = 'Pendiente' | 'Aprobada' | 'Rechazada' | 'Confirmada';
export type DestinoUso = 'Vivero' | 'Laboratorio' | 'Otro';

export interface SolicitudDto {
  id: string;
  tipo: TipoEgresoInterno;
  destino: string | null;
  signo: SignoEgresoInterno;
  productoId: string | null;
  nombreProducto: string | null;
  loteId: string | null;
  codigoLote: string | null;
  cantidad: number;
  unidad: string;
  motivo: string;
  solicitanteId: string;
  estado: EstadoSolicitudInterna;
  aprobadorId: string | null;
  fechaResolucion: string | null;
  motivoResolucion: string | null;
  entregadoPor: string | null;
  fechaEntrega: string | null;
  creadoEn: string;
}

export interface CrearSolicitudRequest {
  tipo: TipoEgresoInterno;
  destinoTipo?: DestinoUso | null;
  destinoOtro?: string | null;
  signo: SignoEgresoInterno;
  productoId?: string | null;
  loteId?: string | null;
  cantidad: number;
  unidad: string;
  motivo: string;
}

export interface RechazarSolicitudRequest {
  motivo: string;
}
