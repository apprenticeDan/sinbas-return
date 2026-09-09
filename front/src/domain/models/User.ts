import { SystemRole } from './Role';
export type { SystemRole };

export interface UserItem {
  id: string;
  empleadoId: string;
  nombreUsuario: string;
  nombres: string;
  apellidoPaterno?: string | null;
  apellidoMaterno?: string | null;
  nombreCompleto: string;
  ci: string;
  ciNumero: string;
  ciComplemento?: string | null;
  telefono?: string | null;
  email?: string | null;
  roles: SystemRole[];
  activo: boolean;
}

export interface CreateUserDTO {
  empleadoId?: string;
  nombres: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  ciNumero: string;
  ciComplemento?: string;
  telefono?: string;
  email?: string;
  nombreUsuario: string;
  contrasena: string;
  roles: SystemRole[];
}

export interface UpdateUserDTO {
  usuarioId?: string;
  nombres: string;
  apellidoPaterno?: string;
  apellidoMaterno?: string;
  ciNumero: string;
  ciComplemento?: string;
  telefono?: string;
  email?: string;
  nombreUsuario: string;
  roles: SystemRole[];
  nuevaContrasena?: string;
}

export interface AssignRolesDTO {
  roles: SystemRole[];
}

