import { Component, Show } from 'solid-js';
import { activeTab, setActiveTab, username, userRoles, logoutUser, hasRole } from '../store/authStore';

export const Sidebar: Component = () => {
  return (
    <aside class="sidebar">
      <div class="brand">
        <div class="brand-mark">
          <svg viewBox="0 0 24 24" fill="none" stroke="#F3F1E4" stroke-width="1.8" stroke-linecap="round">
            <path d="M12 21V10" />
            <path d="M12 10C12 6 9 4 5 4c0 4.5 2.8 7 7 7Z" />
            <path d="M12 13c0-4 3-6 7-6 0 4.2-2.6 6.6-7 6.6Z" />
          </svg>
        </div>
        <div>
          <div class="brand-name">Banco Semillas</div>
          <div class="brand-sub">SINBAS v2.0</div>
        </div>
      </div>

      <nav class="nav-list">
        <span class="nav-eyebrow">Operación</span>
        <button
          class={`nav-item ${activeTab() === 'ingresos' ? 'active' : ''}`}
          onClick={() => setActiveTab('ingresos')}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
            <path d="M12 19V5" />
            <path d="M5 12l7-7 7 7" />
          </svg>
          Ingresos Almacén
        </button>

        <button
          class={`nav-item ${activeTab() === 'proforma' ? 'active' : ''}`}
          onClick={() => setActiveTab('proforma')}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
            <path d="M6 3h9l4 4v14a1 1 0 0 1-1 1H6a1 1 0 0 1-1-1V4a1 1 0 0 1 1-1Z" />
            <path d="M9 12h6M9 16h6" />
          </svg>
          Proformas
        </button>

        <button
          class={`nav-item ${activeTab() === 'pedidos' ? 'active' : ''}`}
          onClick={() => setActiveTab('pedidos')}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
            <path d="M4 7h16l-1.5 12.5a1 1 0 0 1-1 .9H6.5a1 1 0 0 1-1-.9L4 7Z" />
            <path d="M8 7V5a4 4 0 0 1 8 0v2" />
          </svg>
          Ventas y Pedidos
        </button>

        <span class="nav-eyebrow" style={{ marginTop: '10px' }}>Trazabilidad</span>
        <button
          class={`nav-item ${activeTab() === 'kardex' ? 'active' : ''}`}
          onClick={() => setActiveTab('kardex')}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
            <path d="M3 12h4l3 8 4-16 3 8h4" />
          </svg>
          Kárdex e Inventario
        </button>

        <Show when={hasRole('Administrador')}>
          <span class="nav-eyebrow" style={{ marginTop: '10px' }}>Administración</span>
          <button
            class={`nav-item ${activeTab() === 'usuarios' ? 'active' : ''}`}
            onClick={() => setActiveTab('usuarios')}
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
              <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
              <circle cx="9" cy="7" r="4" />
              <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
              <path d="M16 3.13a4 4 0 0 1 0 7.75" />
            </svg>
            Gestión de Usuarios
          </button>
        </Show>
      </nav>

      <div class="sidebar-foot">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
          <div class="user-badge">
            <div class="user-avatar">{username()?.charAt(0).toUpperCase() || 'U'}</div>
            <div>
              <div style={{ color: '#F3F1E4', fontWeight: '600', fontSize: '12.5px' }}>{username()}</div>
              <div style={{ fontSize: '10px', color: 'var(--sidebar-text-soft)' }}>
                {userRoles().join(', ') || 'Usuario'}
              </div>
            </div>
          </div>
          <button
            class="btn btn-ghost"
            style={{
              marginTop: '8px',
              padding: '6px 10px',
              fontSize: '11px',
              width: '100%',
              justifyContent: 'center',
              background: 'rgba(255,255,255,0.08)',
              color: '#E9E7D8',
              border: 'none'
            }}
            onClick={logoutUser}
          >
            Cerrar Sesión
          </button>
        </div>
      </div>
    </aside>
  );
};
