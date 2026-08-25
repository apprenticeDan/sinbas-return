import { SystemRole } from './Role';
export type { SystemRole };

export interface UserItem {
  id: string;
  empleadoId: string;
  nombreUsuario: string;
  roles: SystemRole[];
  activo: boolean;
}

export interface CreateUserDTO {
  empleadoId?: string;
  nombreUsuario: string;
  contrasena: string;
  roles: SystemRole[];
}

export interface AssignRolesDTO {
  roles: SystemRole[];
}
