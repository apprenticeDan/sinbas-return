import { Component, createSignal, createEffect, Show } from 'solid-js';
import { authStore } from './ui/store/authStore';
import { Sidebar } from './ui/components/Sidebar';
import { LoginView } from './ui/views/LoginView';
import { UsersView } from './ui/views/UsersView';
import { PlaceholderView } from './ui/views/PlaceholderView';

export const App: Component = () => {
  const [currentView, setCurrentView] = createSignal(authStore.getDefaultView());

  createEffect(() => {
    if (authStore.isAuthenticated()) {
      if (!authStore.canAccessView(currentView())) {
        setCurrentView(authStore.getDefaultView());
      }
    }
  });

  return (
    <Show when={authStore.isAuthenticated()} fallback={<LoginView />}>
      <div class="app">
        <Sidebar currentView={currentView()} onNavigate={setCurrentView} />
        <main class="content">
          <Show when={currentView() === 'users' && authStore.canAccessView('users')}>
            <UsersView />
          </Show>
          <Show when={currentView() === 'productos' && authStore.canAccessView('productos')}>
            <PlaceholderView
              title="Catálogo de Productos y Precios"
              featureCode="MF-01-01"
              description="Gestión de especies, variedades de semillas y tarifas activas."
            />
          </Show>
          <Show when={currentView() === 'lotes' && authStore.canAccessView('lotes')}>
            <PlaceholderView
              title="Gestión de Lotes de Semillas"
              featureCode="MF-02-01"
              description="Registro y trazabilidad de lotes recibidos y procesados."
            />
          </Show>
          <Show when={currentView() === 'laboratorio' && authStore.canAccessView('laboratorio')}>
            <PlaceholderView
              title="Análisis de Calidad y Germinación"
              featureCode="MF-03-01"
              description="Certificación de semillas y resultados de laboratorio."
            />
          </Show>
        </main>
      </div>
    </Show>
  );
};

export default App;

