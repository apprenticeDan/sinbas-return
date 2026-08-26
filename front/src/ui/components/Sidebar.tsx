import { Component, Show } from 'solid-js';
import { authStore } from '../store/authStore';

interface SidebarProps {
  currentView: string;
  onNavigate: (view: string) => void;
}

export const Sidebar: Component<SidebarProps> = (props) => {
  return (
    <aside class="sidebar">
      <div class="brand">
        <div class="brand-mark">
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2">
            <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
          </svg>
        </div>
        <div>
          <div class="brand-name">SINBAS</div>
          <div class="brand-sub">Semillas & Semilleros</div>
        </div>
      </div>

      <nav class="nav-list">
        <div class="nav-eyebrow">Módulos de Sistema</div>

        <Show when={authStore.canAccessView('users')}>
          <button
            class={`nav-item ${props.currentView === 'users' ? 'active' : ''}`}
            onClick={() => props.onNavigate('users')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
              <path d="M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
            Usuarios & Roles
          </button>
        </Show>

        <Show when={authStore.canAccessView('productos')}>
          <button
            class={`nav-item ${props.currentView === 'productos' ? 'active' : ''}`}
            onClick={() => props.onNavigate('productos')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
            </svg>
            Productos & Precios
          </button>
        </Show>

        <Show when={authStore.canAccessView('lotes')}>
          <button
            class={`nav-item ${props.currentView === 'lotes' ? 'active' : ''}`}
            onClick={() => props.onNavigate('lotes')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="3" y="3" width="18" height="18" rx="2" />
              <path d="M3 9h18M9 21V9" />
            </svg>
            Lotes de Semilla
          </button>
        </Show>

        <Show when={authStore.canAccessView('laboratorio')}>
          <button
            class={`nav-item ${props.currentView === 'laboratorio' ? 'active' : ''}`}
            onClick={() => props.onNavigate('laboratorio')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M9 3h6M10 3v6.5L4 19.5A1 1 0 0 0 5 21h14a1 1 0 0 0 1-1.5L14 9.5V3" />
            </svg>
            Análisis Calidad
          </button>
        </Show>
      </nav>

      <div class="sidebar-foot">
        <div class="user-badge">
          <div class="user-avatar">
            {authStore.user()?.username.charAt(0).toUpperCase() || 'U'}
          </div>
          <div style={{ 'line-height': '1.2' }}>
            <div style={{ 'font-weight': '600', color: 'var(--sidebar-text)' }}>
              {authStore.user()?.username || 'Usuario'}
            </div>
            <div style={{ 'font-size': '10px', color: 'var(--sidebar-text-soft)' }}>
              {authStore.user()?.roles.join(', ') || 'Sin Rol'}
            </div>
          </div>
        </div>

        <button
          class="btn btn-ghost"
          style={{ padding: '6px', color: 'var(--sidebar-text-soft)' }}
          onClick={() => authStore.logout()}
          title="Cerrar Sesión"
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '16px', height: '16px' }}>
            <path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9" />
          </svg>
        </button>
      </div>
    </aside>
  );
};
