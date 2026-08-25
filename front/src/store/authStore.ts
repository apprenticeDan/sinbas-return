import { createSignal, createMemo } from 'solid-js';
import { api, LoginRequest, LoginResponse } from '../services/api';

const initialToken = localStorage.getItem('sinbas_token') || null;
const initialUser = localStorage.getItem('sinbas_username') || null;
const initialRolesRaw = localStorage.getItem('sinbas_roles');
const initialRoles: string[] = initialRolesRaw ? JSON.parse(initialRolesRaw) : [];

export const [token, setToken] = createSignal<string | null>(initialToken);
export const [username, setUsername] = createSignal<string | null>(initialUser);
export const [userRoles, setUserRoles] = createSignal<string[]>(initialRoles);
export const [activeTab, setActiveTab] = createSignal<string>('usuarios');

export const isLoggedIn = createMemo(() => !!token());

export const hasRole = (role: string) => userRoles().includes(role) || userRoles().includes('Administrador');

export async function loginUser(credentials: LoginRequest): Promise<void> {
  const res: LoginResponse = await api.login(credentials);
  setToken(res.token);
  setUsername(res.nombreUsuario);
  setUserRoles(res.roles);

  localStorage.setItem('sinbas_token', res.token);
  localStorage.setItem('sinbas_username', res.nombreUsuario);
  localStorage.setItem('sinbas_roles', JSON.stringify(res.roles));

  // Set default view according to role
  if (res.roles.includes('Administrador')) {
    setActiveTab('usuarios');
  } else if (res.roles.includes('Almacen')) {
    setActiveTab('ingresos');
  } else if (res.roles.includes('Laboratorio')) {
    setActiveTab('lotes');
  } else if (res.roles.includes('Comercial')) {
    setActiveTab('proforma');
  } else {
    setActiveTab('kardex');
  }
}

export function logoutUser(): void {
  setToken(null);
  setUsername(null);
  setUserRoles([]);
  localStorage.removeItem('sinbas_token');
  localStorage.removeItem('sinbas_username');
  localStorage.removeItem('sinbas_roles');
}
