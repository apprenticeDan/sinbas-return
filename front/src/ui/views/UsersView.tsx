import { Component, createSignal, onMount, For, Show } from 'solid-js';
import { UserItem } from '../../domain/models/User';
import { UserUseCases } from '../../application/usecases/UserUseCases';
import { UserModal } from '../components/UserModal';

export const UsersView: Component = () => {
  const [users, setUsers] = createSignal<UserItem[]>([]);
  const [loading, setLoading] = createSignal(true);
  const [error, setError] = createSignal<string | null>(null);
  const [showModal, setShowModal] = createSignal(false);
  const [editingUser, setEditingUser] = createSignal<UserItem | null>(null);

  const loadUsers = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await UserUseCases.listarUsuarios();
      setUsers(data);
    } catch (err: any) {
      setError(err.message || 'Error al cargar usuarios');
    } finally {
      setLoading(false);
    }
  };

  onMount(() => {
    loadUsers();
  });

  const handleToggleState = async (user: UserItem) => {
    try {
      await UserUseCases.cambiarEstado(user.id, user.activo);
      await loadUsers();
    } catch (err: any) {
      alert(err.message || 'Error al cambiar estado del usuario');
    }
  };

  const openCreateModal = () => {
    setEditingUser(null);
    setShowModal(true);
  };

  const openEditModal = (user: UserItem) => {
    setEditingUser(user);
    setShowModal(true);
  };

  return (
    <section class="panel">
      <div class="panel-head">
        <p class="panel-eyebrow">Administración</p>
        <h1 class="panel-title">Gestión de Usuarios y Roles</h1>
        <p class="panel-desc">
          Administración de cuentas de acceso, roles asignados y estado de actividad del personal.
        </p>
      </div>

      <div class="stack">
        <div class="card">
          <div class="toolbar" style={{ 'justify-content': 'space-between' }}>
            <div style={{ display: 'flex', 'align-items': 'center', gap: '10px' }}>
              <span style={{ 'font-weight': '600', color: 'var(--ink-soft)' }}>
                Total Usuarios: {users().length}
              </span>
            </div>
            <button class="btn btn-primary" onClick={openCreateModal}>
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
                <path d="M12 5v14M5 12h14" />
              </svg>
              Nuevo Usuario
            </button>
          </div>

          <Show when={error()}>
            <div class="alert-error" style={{ margin: '16px 20px 0' }}>
              {error()}
            </div>
          </Show>

          <Show when={loading()}>
            <div style={{ padding: '40px', 'text-align': 'center', color: 'var(--ink-soft)' }}>
              Cargando usuarios...
            </div>
          </Show>

          <Show when={!loading()}>
            <table>
              <thead>
                <tr>
                  <th>ID</th>
                  <th>Empleado</th>
                  <th>Usuario</th>
                  <th>Roles Asignados</th>
                  <th>Estado</th>
                  <th style={{ 'text-align': 'right' }}>Acciones</th>
                </tr>
              </thead>
              <tbody>
                <For each={users()}>
                  {(u) => (
                    <tr>
                      <td class="muted">#{u.id}</td>
                      <td>Empleado #{u.empleadoId}</td>
                      <td style={{ 'font-weight': '600' }}>{u.nombreUsuario}</td>
                      <td>
                        <div style={{ display: 'flex', 'flex-wrap': 'wrap', gap: '4px' }}>
                          <For each={u.roles}>
                            {(r) => (
                              <span class="pill pill-green" style={{ 'font-size': '10.5px' }}>
                                {r}
                              </span>
                            )}
                          </For>
                        </div>
                      </td>
                      <td>
                        <Show
                          when={u.activo}
                          fallback={<span class="pill pill-rust">Bloqueado</span>}
                        >
                          <span class="pill pill-green">Activo</span>
                        </Show>
                      </td>
                      <td style={{ 'text-align': 'right' }}>
                        <div style={{ display: 'inline-flex', gap: '6px' }}>
                          <button
                            class="btn btn-ghost"
                            style={{ padding: '4px 10px', 'font-size': '11.5px' }}
                            onClick={() => openEditModal(u)}
                          >
                            Roles
                          </button>
                          <button
                            class={`btn ${u.activo ? 'btn-ghost' : 'btn-primary'}`}
                            style={{ padding: '4px 10px', 'font-size': '11.5px' }}
                            onClick={() => handleToggleState(u)}
                          >
                            {u.activo ? 'Desactivar' : 'Activar'}
                          </button>
                        </div>
                      </td>
                    </tr>
                  )}
                </For>
              </tbody>
            </table>
          </Show>
        </div>
      </div>

      <Show when={showModal()}>
        <UserModal
          userToEdit={editingUser()}
          onClose={() => setShowModal(false)}
          onSuccess={loadUsers}
        />
      </Show>
    </section>
  );
};
