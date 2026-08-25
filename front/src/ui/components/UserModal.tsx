import { Component, createSignal, Show } from 'solid-js';
import { UserItem, SystemRole } from '../../domain/models/User';
import { UserUseCases } from '../../application/usecases/UserUseCases';
import { RoleSelect } from './RoleSelect';

interface UserModalProps {
  userToEdit?: UserItem | null;
  onClose: () => void;
  onSuccess: () => void;
}

export const UserModal: Component<UserModalProps> = (props) => {
  const isEditing = !!props.userToEdit;

  const [nombreEmpleado, setNombreEmpleado] = createSignal('');
  const [username, setUsername] = createSignal(props.userToEdit?.nombreUsuario || '');
  const [password, setPassword] = createSignal('');
  const [selectedRoles, setSelectedRoles] = createSignal<SystemRole[]>(
    props.userToEdit?.roles || ['Almacen']
  );
  const [error, setError] = createSignal<string | null>(null);
  const [loading, setLoading] = createSignal(false);

  const resetForm = () => {
    setNombreEmpleado('');
    setUsername('');
    setPassword('');
    setSelectedRoles(['Almacen']);
    setError(null);
  };

  const handleCancel = () => {
    resetForm();
    props.onClose();
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    if (selectedRoles().length === 0) {
      setError('Debe seleccionar al menos un rol.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (isEditing && props.userToEdit) {
        await UserUseCases.asignarRoles(props.userToEdit.id, selectedRoles());
      } else {
        await UserUseCases.crearUsuario({
          nombreUsuario: username().trim(),
          contrasena: password(),
          roles: selectedRoles(),
        });
      }
      resetForm();
      props.onSuccess();
      props.onClose();
    } catch (err: any) {
      setError(err.message || 'Error al guardar el usuario');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="modal-overlay" onClick={props.onClose}>
      <div class="modal-card" onClick={(e) => e.stopPropagation()}>
        <div class="modal-header">
          <h2 class="modal-title">
            {isEditing ? `Editar Roles: ${props.userToEdit?.nombreUsuario}` : 'Nuevo Usuario del Sistema'}
          </h2>
          <button type="button" class="btn btn-ghost" style={{ padding: '4px 8px' }} onClick={props.onClose}>
            ✕
          </button>
        </div>

        <Show when={error()}>
          <div class="alert-error">{error()}</div>
        </Show>

        <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '14px' }}>
          <Show when={!isEditing}>
            <div class="field">
              <label>Nombre Completo del Empleado</label>
              <input
                type="text"
                value={nombreEmpleado()}
                onInput={(e) => setNombreEmpleado(e.currentTarget.value)}
                placeholder="ej. Juan Carlos Pérez"
                required
              />
            </div>

            <div class="field">
              <label>Nombre de Usuario</label>
              <input
                type="text"
                value={username()}
                onInput={(e) => setUsername(e.currentTarget.value)}
                placeholder="ej. juan.perez"
                required
              />
            </div>

            <div class="field">
              <label>Contraseña Inicial</label>
              <input
                type="password"
                value={password()}
                onInput={(e) => setPassword(e.currentTarget.value)}
                placeholder="••••••••"
                required
              />
            </div>
          </Show>

          <div class="field">
            <label>Roles de Sistema</label>
            <RoleSelect
              selected={selectedRoles()}
              onChange={(roles) => setSelectedRoles(roles)}
            />
          </div>

          <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '16px' }}>
            <button type="button" class="btn btn-ghost" onClick={handleCancel}>
              Cancelar
            </button>
            <button type="submit" class="btn btn-primary" disabled={loading()}>
              {loading() ? 'Guardando...' : 'Guardar Usuario'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
