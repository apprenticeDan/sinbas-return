import { Component, createSignal, Show } from 'solid-js';
import { loginUser } from '../store/authStore';

export const LoginView: Component = () => {
  const [usernameInput, setUsernameInput] = createSignal('admin');
  const [passwordInput, setPasswordInput] = createSignal('admin123');
  const [loading, setLoading] = createSignal(false);
  const [errorMsg, setErrorMsg] = createSignal<string | null>(null);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setLoading(true);
    setErrorMsg(null);

    try {
      await loginUser({
        nombreUsuarioRaw: usernameInput(),
        contrasena: passwordInput(),
      });
    } catch (err: any) {
      setErrorMsg(err.message || 'Error de autenticación');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="login-container">
      <div class="login-card">
        <div class="login-header">
          <div class="login-brand-mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="#F3F1E4" stroke-width="1.8" stroke-linecap="round">
              <path d="M12 21V10" />
              <path d="M12 10C12 6 9 4 5 4c0 4.5 2.8 7 7 7Z" />
              <path d="M12 13c0-4 3-6 7-6 0 4.2-2.6 6.6-7 6.6Z" />
            </svg>
          </div>
          <h1 class="login-title">Banco Semillas</h1>
          <p class="login-subtitle">Sistema de Información SINBAS</p>
        </div>

        <Show when={errorMsg()}>
          <div class="alert-error">{errorMsg()}</div>
        </Show>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div class="field">
            <label>Nombre de Usuario</label>
            <input
              type="text"
              value={usernameInput()}
              onInput={(e) => setUsernameInput(e.currentTarget.value)}
              placeholder="Ej. admin"
              required
            />
          </div>

          <div class="field">
            <label>Contraseña</label>
            <input
              type="password"
              value={passwordInput()}
              onInput={(e) => setPasswordInput(e.currentTarget.value)}
              placeholder="••••••••"
              required
            />
          </div>

          <button
            type="submit"
            class="btn btn-primary"
            disabled={loading()}
            style={{ justifyContent: 'center', marginTop: '8px', padding: '11px' }}
          >
            {loading() ? 'Iniciando sesión...' : 'Iniciar Sesión'}
          </button>
        </form>

        <div style={{ marginTop: '24px', borderTop: '1px solid var(--border-soft)', paddingTop: '16px', textAlign: 'center' }}>
          <span style={{ fontSize: '11px', color: 'var(--ink-soft)' }}>
            Cuenta inicial demo: <strong>admin</strong> / <strong>admin123</strong>
          </span>
        </div>
      </div>
    </div>
  );
};
