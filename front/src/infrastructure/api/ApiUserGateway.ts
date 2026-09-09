import { httpClient } from './HttpClient';
import { UserItem, CreateUserDTO, UpdateUserDTO, SystemRole } from '../../domain/models/User';

export const ApiUserGateway = {
  getUsuarios: () => httpClient<UserItem[]>('/usuarios'),

  createUsuario: (data: CreateUserDTO) =>
    httpClient<void>('/usuarios', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  updateUsuario: (id: string, data: UpdateUserDTO) =>
    httpClient<void>(`/usuarios/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  assignRoles: (id: string, roles: SystemRole[]) =>
    httpClient<void>(`/usuarios/${id}/roles`, {
      method: 'PUT',
      body: JSON.stringify({ usuarioId: id, roles }),
    }),

  activarUsuario: (id: string) =>
    httpClient<void>(`/usuarios/${id}/activar`, {
      method: 'PUT',
    }),

  desactivarUsuario: (id: string) =>
    httpClient<void>(`/usuarios/${id}/desactivar`, {
      method: 'PUT',
    }),
};
