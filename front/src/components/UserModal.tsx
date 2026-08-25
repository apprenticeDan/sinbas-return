import { Component, createSignal, For, Show } from 'solid-js';
import { api, UserItem } from '../services/api';

interface UserModalProps {
  userToEdit?: UserItem | null;
  onClose: () => void;
  onSuccess: () => void;
}

const AVAILABLE_ROLES = [
  'Administrador',
  'Gerencia',
  'Comercial',
  'Almacen',
  'Laboratorio',
];

export const UserModal: Component<UserModalProps> = (props) => {
  const isEditing = !!props.userToEdit;

  const [empleadoId, setEmpleadoId] = createSignal(props.userToEdit?.empleadoId || 1);
  const [username, setUsername] = createSignal(props.userToEdit?.nombreUsuario || '');
  const [password, setPassword] = createSignal('');
  const [selectedRoles, setSelectedRoles] = createSignal<string[]>(
    props.userToEdit?.roles || ['Almacen']
  );
  const [error, setError] = createSignal<string | null>(null);
  const [loading, setLoading] = createSignal(false);

  const toggleRole = (role: string) => {
    const current = selectedRoles();
    if (current.includes(role)) {
      setSelectedRoles(current.filter((r) => r !== role));
    } else {
      setSelectedRoles([...current, role]);
    }
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
        console.log('[UserModal] Actualizando roles:', props.userToEdit.id, selectedRoles());
        await api.assignRoles(props.userToEdit.id, selectedRoles());
      } else {
        console.log('[UserModal] Creando usuario:', username(), selectedRoles());
        await api.createUsuario({
          empleadoId: empleadoId(),
          nombreUsuario: username(),
          contrasena: password(),
          roles: selectedRoles(),
        });
      }
      console.log('[UserModal] Operación exitosa, llamando onSuccess y onClose');
      props.onSuccess();
      props.onClose();
    } catch (err: any) {
      console.error('[UserModal] Error al guardar:', err);
      setError(err.message || 'Error al guardar el usuario');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="modal-overlay">
      <div class="modal-card">
        <div class="modal-header">
          <h2 class="modal-title">
            {isEditing ? `Editar Roles: ${props.userToEdit?.nombreUsuario}` : 'Nuevo Usuario del Sistema'}
          </h2>
          <button class="btn btn-ghost" style={{ padding: '4px 8px' }} onClick={props.onClose}>
            ✕
          </button>
        </div>

        <Show when={error()}>
          <div class="alert-error">{error()}</div>
        </Show>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
          <Show when={!isEditing}>


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
            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', marginTop: '4px' }}>
              <For each={AVAILABLE_ROLES}>
                {(role) => {
                  const isChecked = () => selectedRoles().includes(role);
                  return (
                    <button
                      type="button"
                      class={`btn ${isChecked() ? 'btn-primary' : 'btn-ghost'}`}
                      style={{ padding: '6px 12px', fontSize: '12px' }}
                      onClick={() => toggleRole(role)}
                    >
                      {isChecked() ? '✓ ' : '+ '} {role}
                    </button>
                  );
                }}
              </For>
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '10px', marginTop: '16px' }}>
            <button type="button" class="btn btn-ghost" onClick={props.onClose}>
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
