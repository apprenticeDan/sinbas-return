import { ApiUserGateway } from '../../infrastructure/api/ApiUserGateway';
import { CreateUserDTO, SystemRole } from '../../domain/models/User';

export const UserUseCases = {
  listarUsuarios: () => ApiUserGateway.getUsuarios(),

  crearUsuario: (data: CreateUserDTO) => ApiUserGateway.createUsuario(data),

  asignarRoles: (id: number, roles: SystemRole[]) => ApiUserGateway.assignRoles(id, roles),

  cambiarEstado: async (id: number, estaActivo: boolean) => {
    if (estaActivo) {
      await ApiUserGateway.desactivarUsuario(id);
    } else {
      await ApiUserGateway.activarUsuario(id);
    }
  },
};
