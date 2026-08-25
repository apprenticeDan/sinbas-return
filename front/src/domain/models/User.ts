import { SystemRole } from './Role';

export interface UserItem {
  id: number;
  empleadoId: number;
  nombreUsuario: string;
  roles: SystemRole[];
  activo: boolean;
}

export interface CreateUserDTO {
  empleadoId: number;
  nombreUsuario: string;
  contrasena: string;
  roles: SystemRole[];
}

export interface AssignRolesDTO {
  roles: SystemRole[];
}
