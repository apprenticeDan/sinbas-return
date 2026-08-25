const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:8080/api';

export interface LoginRequest {
  nombreUsuarioRaw: string;
  contrasena: string;
}

export interface LoginResponse {
  token: string;
  nombreUsuario: string;
  roles: string[];
}

export interface UserItem {
  id: number;
  empleadoId: number;
  nombreUsuario: string;
  roles: string[];
  activo: boolean;
}

export interface CreateUserRequest {
  empleadoId: number;
  nombreUsuario: string;
  contrasena: string;
  roles: string[];
}

export interface AssignRolesRequest {
  roles: string[];
}

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

async function request<T>(endpoint: string, options: RequestInit = {}): Promise<T> {
  const token = localStorage.getItem('sinbas_token');

  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string>),
  };

  if (token) {
    headers['Authorization'] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, {
    ...options,
    headers,
  });

  if (!response.ok) {
    let errorMsg = `Error ${response.status}: ${response.statusText}`;
    try {
      const errData = await response.json();
      if (errData.error) errorMsg = errData.error;
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


export const api = {
  login: (data: LoginRequest) =>
    request<LoginResponse>('/auth/login', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  getUsuarios: () => request<UserItem[]>('/usuarios'),

  createUsuario: (data: CreateUserRequest) =>
    request<void>('/usuarios', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  assignRoles: (id: number, roles: string[]) =>
    request<void>(`/usuarios/${id}/roles`, {
      method: 'PUT',
      body: JSON.stringify({ usuarioId: id, roles }),
    }),

  activarUsuario: (id: number) =>
    request<void>(`/usuarios/${id}/activar`, {
      method: 'PUT',
    }),

  desactivarUsuario: (id: number) =>
    request<void>(`/usuarios/${id}/desactivar`, {
      method: 'PUT',
    }),
};
