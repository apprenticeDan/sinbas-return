import { createSignal } from 'solid-js';

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

export const authStore = {
  token,
  user,
  setAuth: (newToken: string, username: string, roles: string[]) => {
    const userData = { username, roles };
    localStorage.setItem('sinbas_token', newToken);
    localStorage.setItem('sinbas_user', JSON.stringify(userData));
    setToken(newToken);
    setUser(userData);
  },
  logout: () => {
    localStorage.removeItem('sinbas_token');
    localStorage.removeItem('sinbas_user');
    setToken(null);
    setUser(null);
  },
  isAuthenticated: () => !!token(),
  hasRole: (role: string) => user()?.roles.includes(role) || false,
  hasAnyRole: (roles: string[]) => user()?.roles.some((r) => roles.includes(r)) || false,

  canAccessView: (view: string): boolean => {
    const userRoles = user()?.roles || [];
    if (userRoles.includes('Administrador')) return true;

    switch (view) {
      case 'users':
        return false; // Solo Administrador
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

