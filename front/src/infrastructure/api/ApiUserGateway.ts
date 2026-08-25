import { httpClient } from './HttpClient';
import { UserItem, CreateUserDTO, SystemRole } from '../../domain/models/User';

export const ApiUserGateway = {
  getUsuarios: () => httpClient<UserItem[]>('/usuarios'),

  createUsuario: (data: CreateUserDTO) =>
    httpClient<void>('/usuarios', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  assignRoles: (id: number, roles: SystemRole[]) =>
    httpClient<void>(`/usuarios/${id}/roles`, {
      method: 'PUT',
      body: JSON.stringify({ usuarioId: id, roles }),
    }),

  activarUsuario: (id: number) =>
    httpClient<void>(`/usuarios/${id}/activar`, {
      method: 'PUT',
    }),

  desactivarUsuario: (id: number) =>
    httpClient<void>(`/usuarios/${id}/desactivar`, {
      method: 'PUT',
    }),
};
