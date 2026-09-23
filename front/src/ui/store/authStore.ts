import { createSignal } from 'solid-js';
import { setAuthCallbacks } from '../../infrastructure/api/HttpClient';
import { ApiAuthGateway } from '../../infrastructure/api/ApiAuthGateway';

interface UserState {
  username: string;
  roles: string[];
}

const savedToken = localStorage.getItem('sinbas_token');
const savedUser = localStorage.getItem('sinbas_user');

const [token, setToken] = createSignal<string | null>(savedToken);
const [user, setUser] = createSignal<UserState | null>(
  savedUser ? JSON.parse(savedUser) : null
);

let proactiveRefreshTimer: number | null = null;

/**
 * Decodifica la expiración de un JWT y programa una renovación proactiva
 * 2 minutos antes de su vencimiento (MF-00-06).
 */
function programarRefrescoProactivo(jwtToken: string) {
  if (proactiveRefreshTimer) {
    clearTimeout(proactiveRefreshTimer);
    proactiveRefreshTimer = null;
  }

  if (typeof window === 'undefined') return;

  try {
    const parts = jwtToken.split('.');
    if (parts.length < 2) return;
    const payloadJson = atob(parts[1].replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(payloadJson);
    if (!payload.exp) return;

    const expMs = payload.exp * 1000;
    const ahoraMs = Date.now();
    // Renovar 2 minutos antes, con un piso de 10 segundos
    const margenMs = 2 * 60 * 1000;
    const delayMs = Math.max(10000, (expMs - ahoraMs) - margenMs);

    proactiveRefreshTimer = window.setTimeout(async () => {
      try {
        const res = await ApiAuthGateway.refresh();
        if (res && res.token) {
          authStore.updateToken(res.token);
        }
      } catch (_) {
        // Si falla el timer proactivo, el interceptor 401 actuará como salvaguarda
      }
    }, delayMs);
  } catch (_) {
    // Si el parseo falla, el interceptor 401 reintentará normalmente
  }
}

// Iniciar timer si ya había un token cargado
if (savedToken) {
  programarRefrescoProactivo(savedToken);
}

// Configurar sincronización con HttpClient
setAuthCallbacks({
  onTokenUpdate: (newToken: string) => {
    authStore.updateToken(newToken);
  },
  onSessionExpired: () => {
    authStore.logoutLocal();
  },
});

// ─────────────────────────────────────────────────────────────
// Salvaguarda contra botón "Atrás" del navegador (bfcache)
// Impide que un usuario acceda a datos o sesiones previas
// al navegar hacia atrás tras un logout en equipos compartidos.
// ─────────────────────────────────────────────────────────────
if (typeof window !== 'undefined') {
  window.addEventListener('pageshow', (event) => {
    // event.persisted indica que la página fue cargada desde la caché Back/Forward
    if (event.persisted && !authStore.isAuthenticated()) {
      window.location.replace('/');
    }
  });
}

export const authStore = {
  token,
  user,

  setAuth: (newToken: string, username: string, roles: string[]) => {
    const userData = { username, roles };
    localStorage.setItem('sinbas_token', newToken);
    localStorage.setItem('sinbas_user', JSON.stringify(userData));
    setToken(newToken);
    setUser(userData);
    programarRefrescoProactivo(newToken);
  },

  updateToken: (newToken: string) => {
    localStorage.setItem('sinbas_token', newToken);
    setToken(newToken);
    programarRefrescoProactivo(newToken);
  },

  logoutLocal: () => {
    if (proactiveRefreshTimer) {
      clearTimeout(proactiveRefreshTimer);
      proactiveRefreshTimer = null;
    }
    localStorage.removeItem('sinbas_token');
    localStorage.removeItem('sinbas_user');
    sessionStorage.clear();
    setToken(null);
    setUser(null);
  },

  logout: async () => {
    try {
      await ApiAuthGateway.logout();
    } catch (_) {
      // Ignorar fallos de red durante logout
    } finally {
      authStore.logoutLocal();
      if (typeof window !== 'undefined') {
        // Redirección con replace para evitar retención de historial de navegación sensible
        window.location.replace('/');
      }
    }
  },

  isAuthenticated: () => !!token(),
  hasRole: (role: string) => user()?.roles.includes(role) || false,
  hasAnyRole: (roles: string[]) => user()?.roles.some((r) => roles.includes(r)) || false,

  canAccessView: (view: string): boolean => {
    const userRoles = user()?.roles || [];
    if (userRoles.includes('Administrador')) return true;

    switch (view) {
      case 'users':
        return false;
      case 'productos':
        return userRoles.some((r) => ['Gerencia', 'Comercial', 'Almacen'].includes(r));
      case 'lotes':
        return userRoles.some((r) => ['Almacen', 'Gerencia', 'Laboratorio', 'Comercial'].includes(r));
      case 'laboratorio':
        return userRoles.includes('Laboratorio');
      case 'ingresos':
        return userRoles.some((r) => ['Almacen', 'Comercial', 'Gerencia'].includes(r));
      case 'egresos':
        return userRoles.some((r) => ['Almacen', 'Comercial', 'Gerencia'].includes(r));
      case 'stock':
        return userRoles.some((r) => ['Almacen', 'Comercial', 'Gerencia'].includes(r));
      case 'clientes':
        return userRoles.some((r) => ['Comercial', 'Gerencia', 'Almacen'].includes(r));
      default:
        return false;
    }
  },

  getDefaultView: (): string => {
    const userRoles = user()?.roles || [];
    if (userRoles.includes('Administrador')) return 'users';
    if (userRoles.includes('Almacen')) return 'ingresos';
    if (userRoles.includes('Laboratorio')) return 'laboratorio';
    if (userRoles.some((r) => ['Gerencia', 'Comercial'].includes(r))) return 'productos';
    return 'lotes';
  },
};
