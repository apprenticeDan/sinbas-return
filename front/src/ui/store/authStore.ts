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
};
