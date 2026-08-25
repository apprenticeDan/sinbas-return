import { Component, createSignal, Show } from 'solid-js';
import { authStore } from './ui/store/authStore';
import { Sidebar } from './ui/components/Sidebar';
import { LoginView } from './ui/views/LoginView';
import { UsersView } from './ui/views/UsersView';
import { PlaceholderView } from './ui/views/PlaceholderView';

export const App: Component = () => {
  const [currentView, setCurrentView] = createSignal('users');

  return (
    <Show when={authStore.isAuthenticated()} fallback={<LoginView />}>
      <div class="app">
        <Sidebar currentView={currentView()} onNavigate={setCurrentView} />
        <main class="content">
          <Show when={currentView() === 'users'}>
            <UsersView />
          </Show>
          <Show when={currentView() === 'productos'}>
            <PlaceholderView
              title="Catálogo de Productos y Precios"
              featureCode="MF-01-01"
              description="Gestión de especies, variedades de semillas y tarifas activas."
            />
          </Show>
          <Show when={currentView() === 'lotes'}>
            <PlaceholderView
              title="Gestión de Lotes de Semillas"
              featureCode="MF-02-01"
              description="Registro y trazabilidad de lotes recibidos y procesados."
            />
          </Show>
          <Show when={currentView() === 'laboratorio'}>
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
