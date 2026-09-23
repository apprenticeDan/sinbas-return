const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080/api';

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

// ─────────────────────────────────────────────────────────────
// Mutex de Refresco Criptográfico (MF-00-06)
// Si múltiples peticiones reciben HTTP 401 simultáneamente,
// se encolan compartiendo una única promesa de renovación.
// ─────────────────────────────────────────────────────────────
let refreshPromise: Promise<string | null> | null = null;

type TokenUpdateCallback = (token: string) => void;
type SessionExpiredCallback = () => void;

let onTokenUpdateCallback: TokenUpdateCallback | null = null;
let onSessionExpiredCallback: SessionExpiredCallback | null = null;

export function setAuthCallbacks(callbacks: {
  onTokenUpdate: TokenUpdateCallback;
  onSessionExpired: SessionExpiredCallback;
}) {
  onTokenUpdateCallback = callbacks.onTokenUpdate;
  onSessionExpiredCallback = callbacks.onSessionExpired;
}

/**
 * Cliente HTTP resiliente con:
 * 1. Envío automático de cookies HttpOnly (`credentials: 'include'`).
 * 2. Inyección de Bearer Token en cabecera Authorization.
 * 3. Interceptor transparente ante HTTP 401 con mutex de refresco y reintento.
 * 4. Soporte nativo para cabecera Idempotency-Key en transacciones críticas.
 */
export async function httpClient<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem('sinbas_token');

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };

  if (token && !headers['Authorization']) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    credentials: 'include', // Imprescindible para el transporte seguro del refresh token en cookie HttpOnly
    headers,
  });

  // Interceptor de expiración: si la petición devuelve 401 y no es ya una llamada al subsistema de auth
  if (response.status === 401 && !endpoint.includes('/auth/login') && !endpoint.includes('/auth/refresh') && !endpoint.includes('/auth/logout')) {
    const nuevoToken = await renovarTokenSilencioso();

    if (nuevoToken) {
      // Reintentar la petición original con el nuevo token de acceso rotado
      const retryHeaders = {
        ...headers,
        Authorization: `Bearer ${nuevoToken}`,
      };

      const retryResponse = await fetch(`${API_BASE_URL}${endpoint}`, {
        ...options,
        credentials: 'include',
        headers: retryHeaders,
      });

      return procesarRespuesta<T>(retryResponse);
    } else {
      // La renovación falló (refresh token expirado o revocado): disparar callback de sesión expirada
      if (onSessionExpiredCallback) {
        onSessionExpiredCallback();
      }
      throw new ApiError(401, 'Su sesión ha expirado. Por favor, vuelva a identificarse.');
    }
  }

  return procesarRespuesta<T>(response);
}

/**
 * Realiza la llamada a /api/auth/refresh bajo un patrón mutex.
 * Si ya hay una renovación en curso, reutiliza la misma promesa.
 */
async function renovarTokenSilencioso(): Promise<string | null> {
  if (refreshPromise) {
    return refreshPromise;
  }

  refreshPromise = (async () => {
    try {
      const res = await fetch(`${API_BASE_URL}/auth/refresh`, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
      });

      if (!res.ok) {
        return null;
      }

      const data = await res.json();
      if (data && data.token) {
        localStorage.setItem('sinbas_token', data.token);
        if (onTokenUpdateCallback) {
          onTokenUpdateCallback(data.token);
        }
        return data.token as string;
      }
      return null;
    } catch (_) {
      return null;
    } finally {
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

async function procesarRespuesta<T>(response: Response): Promise<T> {
  if (!response.ok) {
    if (response.status === 409) {
      const errorData = await response.json().catch(() => ({}));
      throw new ApiError(409, errorData.error || 'Conflicto: El recurso ya existe.');
    }
    let errorMsg = `Error ${response.status}: ${response.statusText}`;
    try {
      const errData = await response.json();
      if (errData.error) {
        errorMsg = errData.detalle ? `${errData.error} (${errData.detalle})` : errData.error;
      }
    } catch (_) {}
    throw new ApiError(response.status, errorMsg);
  }

  if (response.status === 204) {
    return {} as T;
  }

  const text = await response.text();
  if (!text || text.trim().length === 0) {
    return {} as T;
  }

  try {
    return JSON.parse(text);
  } catch (_) {
    return {} as T;
  }
}
