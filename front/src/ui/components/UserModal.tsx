import { Component, createSignal, createEffect, Show } from 'solid-js';
import { UserItem, SystemRole } from '../../domain/models/User';
import { UserUseCases } from '../../application/usecases/UserUseCases';
import { RoleSelect } from './RoleSelect';

interface UserModalProps {
  userToEdit?: UserItem | null;
  onClose: () => void;
  onSuccess: () => void;
}

export const UserModal: Component<UserModalProps> = (props) => {
  const isEditing = () => !!props.userToEdit;

  // Datos personales del empleado
  const [nombres, setNombres] = createSignal('');
  const [apellidoPaterno, setApellidoPaterno] = createSignal('');
  const [apellidoMaterno, setApellidoMaterno] = createSignal('');
  const [ciNumero, setCiNumero] = createSignal('');
  const [ciComplemento, setCiComplemento] = createSignal('');
  const [telefono, setTelefono] = createSignal('');
  const [email, setEmail] = createSignal('');

  // Cuenta de usuario
  const [username, setUsername] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [selectedRoles, setSelectedRoles] = createSignal<SystemRole[]>(['Almacen']);

  const [error, setError] = createSignal<string | null>(null);
  const [loading, setLoading] = createSignal(false);

  const resetForm = () => {
    setNombres('');
    setApellidoPaterno('');
    setApellidoMaterno('');
    setCiNumero('');
    setCiComplemento('');
    setTelefono('');
    setEmail('');
    setUsername('');
    setPassword('');
    setSelectedRoles(['Almacen']);
    setError(null);
  };

  createEffect(() => {
    const u = props.userToEdit;
    if (u) {
      setNombres(u.nombres || '');
      setApellidoPaterno(u.apellidoPaterno || '');
      setApellidoMaterno(u.apellidoMaterno || '');
      setCiNumero(u.ciNumero || '');
      setCiComplemento(u.ciComplemento || '');
      setTelefono(u.telefono || '');
      setEmail(u.email || '');
      setUsername(u.nombreUsuario || '');
      setPassword('');
      setSelectedRoles(u.roles ? [...u.roles] : ['Almacen']);
      setError(null);
    } else {
      resetForm();
    }
  });

  const handleCancel = () => {
    resetForm();
    props.onClose();
  };

  const handleSubmit = async (e: Event) => {
    e.preventDefault();

    // Validar nombres
    if (!nombres().trim()) {
      setError('El campo "Nombres" es obligatorio.');
      return;
    }

    // Regla de dominio: al menos un apellido debe estar presente
    if (!apellidoPaterno().trim() && !apellidoMaterno().trim()) {
      setError('Debe registrar al menos un apellido (Paterno o Materno).');
      return;
    }

    // Validar CI
    if (!ciNumero().trim()) {
      setError('El número de C.I. es obligatorio.');
      return;
    }

    // Validar roles
    if (selectedRoles().length === 0) {
      setError('Debe seleccionar al menos un rol de sistema.');
      return;
    }

    // Validar contraseña en creación
    if (!isEditing() && (!password() || password().length < 8)) {
      setError('La contraseña inicial debe tener al menos 8 caracteres.');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      if (isEditing() && props.userToEdit) {
        await UserUseCases.actualizarUsuario(props.userToEdit.id, {
          nombres: nombres().trim(),
          apellidoPaterno: apellidoPaterno().trim() || undefined,
          apellidoMaterno: apellidoMaterno().trim() || undefined,
          ciNumero: ciNumero().trim(),
          ciComplemento: ciComplemento().trim() || undefined,
          telefono: telefono().trim() || undefined,
          email: email().trim() || undefined,
          nombreUsuario: username().trim(),
          roles: selectedRoles(),
          nuevaContrasena: password().trim() || undefined,
        });
      } else {
        await UserUseCases.crearUsuario({
          nombres: nombres().trim(),
          apellidoPaterno: apellidoPaterno().trim() || undefined,
          apellidoMaterno: apellidoMaterno().trim() || undefined,
          ciNumero: ciNumero().trim(),
          ciComplemento: ciComplemento().trim() || undefined,
          telefono: telefono().trim() || undefined,
          email: email().trim() || undefined,
          nombreUsuario: username().trim(),
          contrasena: password(),
          roles: selectedRoles(),
        });
      }

      resetForm();
      props.onSuccess();
      props.onClose();
    } catch (err: any) {
      setError(err.message || 'Error al guardar los datos del usuario');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="modal-overlay" onClick={props.onClose}>
      <div class="modal-card" style={{ 'max-width': '580px', width: '95%' }} onClick={(e) => e.stopPropagation()}>
        <div class="modal-header">
          <h2 class="modal-title">
            {isEditing() ? `Editar Usuario: ${props.userToEdit?.nombreUsuario}` : 'Nuevo Usuario y Empleado'}
          </h2>
          <button type="button" class="btn btn-ghost" style={{ padding: '4px 8px' }} onClick={props.onClose}>
            ✕
          </button>
        </div>

        <Show when={error()}>
          <div class="alert-error" style={{ 'margin-bottom': '12px' }}>{error()}</div>
        </Show>

        <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '16px' }}>
          
          {/* SECCIÓN: DATOS DEL PERSONAL / EMPLEADO */}
          <div style={{
            background: 'var(--surface-sunken)',
            padding: '14px',
            'border-radius': 'var(--radius-md)',
            border: '1px solid var(--border-soft)',
            display: 'flex',
            'flex-direction': 'column',
            gap: '12px'
          }}>
            <span style={{ 'font-size': '11.5px', 'font-weight': '700', 'text-transform': 'uppercase', color: 'var(--ink-soft)', 'letter-spacing': '0.5px' }}>
              👤 Datos del Empleado / Personal
            </span>

            <div class="field">
              <label>Nombres <span style={{ color: 'var(--rust)' }}>*</span></label>
              <input
                type="text"
                value={nombres()}
                onInput={(e) => setNombres(e.currentTarget.value)}
                placeholder="ej. Juan Daniel, Gloria de Jesús"
                required
              />
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '10px' }}>
              <div class="field">
                <label>Apellido Paterno <span style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>(opcional)</span></label>
                <input
                  type="text"
                  value={apellidoPaterno()}
                  onInput={(e) => setApellidoPaterno(e.currentTarget.value)}
                  placeholder="ej. Pérez, de la Riva"
                />
              </div>
              <div class="field">
                <label>Apellido Materno <span style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>(opcional)</span></label>
                <input
                  type="text"
                  value={apellidoMaterno()}
                  onInput={(e) => setApellidoMaterno(e.currentTarget.value)}
                  placeholder="ej. Martínez, Torrico"
                />
              </div>
            </div>
            <span style={{ 'font-size': '11px', color: 'var(--ink-soft)', 'margin-top': '-6px' }}>
              ℹ️ Se requiere al menos un apellido (paterno o materno según documento de identidad).
            </span>

            <div style={{ display: 'grid', 'grid-template-columns': '2fr 1fr', gap: '10px' }}>
              <div class="field">
                <label>C.I. (Carnet de Identidad) <span style={{ color: 'var(--rust)' }}>*</span></label>
                <input
                  type="text"
                  value={ciNumero()}
                  onInput={(e) => setCiNumero(e.currentTarget.value)}
                  placeholder="ej. 1234567"
                  required
                />
              </div>
              <div class="field">
                <label>Complemento <span style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>(opc.)</span></label>
                <input
                  type="text"
                  value={ciComplemento()}
                  onInput={(e) => setCiComplemento(e.currentTarget.value)}
                  placeholder="ej. LP, 1A"
                  maxlength="5"
                />
              </div>
            </div>

            <div style={{ display: 'grid', 'grid-template-columns': '1fr 1fr', gap: '10px' }}>
              <div class="field">
                <label>Teléfono / Celular</label>
                <input
                  type="tel"
                  value={telefono()}
                  onInput={(e) => setTelefono(e.currentTarget.value)}
                  placeholder="ej. +591 71234567"
                />
              </div>
              <div class="field">
                <label>Correo Electrónico</label>
                <input
                  type="email"
                  value={email()}
                  onInput={(e) => setEmail(e.currentTarget.value)}
                  placeholder="ej. usuario@empresa.com"
                />
              </div>
            </div>
          </div>

          {/* SECCIÓN: CUENTA DE USUARIO */}
          <div style={{
            background: 'var(--surface-sunken)',
            padding: '14px',
            'border-radius': 'var(--radius-md)',
            border: '1px solid var(--border-soft)',
            display: 'flex',
            'flex-direction': 'column',
            gap: '12px'
          }}>
            <span style={{ 'font-size': '11.5px', 'font-weight': '700', 'text-transform': 'uppercase', color: 'var(--ink-soft)', 'letter-spacing': '0.5px' }}>
              🔐 Credenciales de Acceso
            </span>

            <div class="field">
              <label>Nombre de Usuario <span style={{ color: 'var(--rust)' }}>*</span></label>
              <input
                type="text"
                value={username()}
                onInput={(e) => setUsername(e.currentTarget.value)}
                placeholder="ej. juan.perez"
                required
              />
            </div>

            <div class="field">
              <label>
                {isEditing() ? 'Nueva Contraseña (opcional)' : 'Contraseña Inicial *'}
              </label>
              <input
                type="password"
                value={password()}
                onInput={(e) => setPassword(e.currentTarget.value)}
                placeholder={isEditing() ? 'Dejar en blanco para conservar actual' : 'Mínimo 8 caracteres'}
                required={!isEditing()}
              />
              <Show when={isEditing()}>
                <span style={{ 'font-size': '11px', color: 'var(--ink-soft)' }}>
                  Solo ingrese un valor si desea cambiar o resetear la contraseña del usuario.
                </span>
              </Show>
            </div>

            <div class="field">
              <label>Roles de Sistema <span style={{ color: 'var(--rust)' }}>*</span></label>
              <RoleSelect
                selected={selectedRoles()}
                onChange={(roles) => setSelectedRoles(roles)}
              />
            </div>
          </div>

          {/* BOTONES DE ACCIÓN */}
          <div style={{ display: 'flex', 'justify-content': 'flex-end', gap: '10px', 'margin-top': '8px' }}>
            <button type="button" class="btn btn-ghost" onClick={handleCancel}>
              Cancelar
            </button>
            <button type="submit" class="btn btn-primary" disabled={loading()}>
              {loading() ? 'Guardando...' : (isEditing() ? 'Actualizar Usuario' : 'Crear Usuario')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};

