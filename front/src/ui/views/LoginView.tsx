import { Component, createSignal, Show } from 'solid-js';
import { AuthUseCases } from '../../application/usecases/AuthUseCases';
import { authStore } from '../store/authStore';

export const LoginView: Component = () => {
  const [username, setUsername] = createSignal('');
  const [password, setPassword] = createSignal('');
  const [error, setError] = createSignal<string | null>(null);
  const [loading, setLoading] = createSignal(false);

  const handleSubmit = async (e: Event) => {
    e.preventDefault();
    setLoading(true);
    setError(null);

    try {
      const res = await AuthUseCases.login({
        nombreUsuarioRaw: username().trim(),
        contrasena: password(),
      });

      authStore.setAuth(res.token, res.nombreUsuario, res.roles);
    } catch (err: any) {
      setError(err.message || 'Error al iniciar sesión');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div class="login-container">
      <div class="login-card">
        <div class="login-header">
          <div class="login-brand-mark">
            <svg viewBox="0 0 24 24" fill="none" stroke="#FFFFFF" stroke-width="2.2">
              <path d="M12 2L2 7l10 5 10-5-10-5zM2 17l10 5 10-5M2 12l10 5 10-5" />
            </svg>
          </div>
          <h1 class="login-title">SINBAS</h1>
          <p class="login-subtitle">Sistema de Semillas y Semilleros</p>
        </div>

        <Show when={error()}>
          <div class="alert-error">{error()}</div>
        </Show>

        <form onSubmit={handleSubmit} style={{ display: 'flex', 'flex-direction': 'column', gap: '16px' }}>
          <div class="field">
            <label>Usuario</label>
            <input
              type="text"
              value={username()}
              onInput={(e) => setUsername(e.currentTarget.value)}
              placeholder="admin"
              required
            />
          </div>

          <div class="field">
            <label>Contraseña</label>
            <input
              type="password"
              value={password()}
              onInput={(e) => setPassword(e.currentTarget.value)}
              placeholder="••••••••"
              required
            />
          </div>

          <button
            type="submit"
            class="btn btn-primary"
            style={{ width: '100%', 'justify-content': 'center', 'margin-top': '8px', padding: '12px' }}
            disabled={loading()}
          >
            {loading() ? 'Autenticando...' : 'Iniciar Sesión'}
          </button>
        </form>
      </div>
    </div>
  );
};
