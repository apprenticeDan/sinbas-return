import { ApiUserGateway } from '../../infrastructure/api/ApiUserGateway';
import { CreateUserDTO, UpdateUserDTO, SystemRole } from '../../domain/models/User';

export const UserUseCases = {
  listarUsuarios: () => ApiUserGateway.getUsuarios(),

  crearUsuario: (data: CreateUserDTO) => ApiUserGateway.createUsuario(data),

  actualizarUsuario: (id: string, data: UpdateUserDTO) => ApiUserGateway.updateUsuario(id, data),

  asignarRoles: (id: string, roles: SystemRole[]) => ApiUserGateway.assignRoles(id, roles),

  cambiarEstado: async (id: string, estaActivo: boolean) => {
    if (estaActivo) {
      await ApiUserGateway.desactivarUsuario(id);
    } else {
      await ApiUserGateway.activarUsuario(id);
    }
  },
};
