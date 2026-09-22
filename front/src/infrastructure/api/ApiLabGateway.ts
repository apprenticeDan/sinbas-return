import { httpClient } from './HttpClient';

export interface RegistrarAnalisisPayload {
  germinacion: number;
  pureza: number;
  humedad: number;
  viabilidad: number;
  semillasPurasKg?: number;
  semillasImpurezasKg?: number;
  dictamenManual?: string;
  fechaAnalisis?: string;
  observaciones?: string;
}

export interface AnalisisDto {
  id: string;
  loteId: string;
  fechaAnalisis: string;
  germinacion: number;
  pureza: number;
  humedad: number;
  viabilidad: number;
  semillasPurasKg: number;
  semillasImpurezasKg: number;
  dictamen: string;
  observaciones?: string;
}

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080/api';

export const ApiLabGateway = {
  async registrarAnalisis(loteId: string, payload: RegistrarAnalisisPayload): Promise<AnalisisDto> {
    return httpClient<AnalisisDto>(`/lotes/${loteId}/analisis`, {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async listarAnalisisPorLote(loteId: string): Promise<AnalisisDto[]> {
    return httpClient<AnalisisDto[]>(`/lotes/${loteId}/analisis`);
  },

  async descargarEtiquetaPdf(loteId: string, codigoLote: string): Promise<void> {
    const token = localStorage.getItem('sinbas_token');
    const headers: Record<string, string> = {};
    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    const response = await fetch(`${API_BASE_URL}/lotes/${loteId}/etiqueta`, {
      headers,
    });

    if (!response.ok) {
      let err = 'Error al descargar etiqueta PDF';
      try {
        const json = await response.json();
        if (json.error) err = json.error;
      } catch (_) {}
      throw new Error(err);
    }

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `etiqueta_${codigoLote}.pdf`;
    document.body.appendChild(a);
    a.click();
    a.remove();
    window.URL.revokeObjectURL(url);
  },
};
