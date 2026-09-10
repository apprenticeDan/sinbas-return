import { Component, createSignal, createEffect, Show } from 'solid-js';
import { authStore } from './ui/store/authStore';
import { Sidebar } from './ui/components/Sidebar';
import { LoginView } from './ui/views/LoginView';
import { UsersView } from './ui/views/UsersView';
import { CatalogView } from './ui/views/CatalogView';
import { LotesView } from './ui/views/LotesView';
import { PlaceholderView } from './ui/views/PlaceholderView';

import { IngresosView } from './ui/views/IngresosView';
import { EgresosView } from './ui/views/EgresosView';
import { StockView } from './ui/views/StockView';

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
            <CatalogView />
          </Show>
          <Show when={currentView() === 'lotes' && authStore.canAccessView('lotes')}>
            <LotesView />
          </Show>
          <Show when={currentView() === 'laboratorio' && authStore.canAccessView('laboratorio')}>
            <PlaceholderView
              title="Análisis de Calidad y Germinación"
              featureCode="MF-03-01"
              description="Certificación de semillas y resultados de laboratorio."
            />
          </Show>
          <Show when={currentView() === 'ingresos' && authStore.canAccessView('ingresos')}>
            <IngresosView />
          </Show>
          <Show when={currentView() === 'egresos' && authStore.canAccessView('egresos')}>
            <EgresosView />
          </Show>
          <Show when={currentView() === 'stock' && authStore.canAccessView('stock')}>
            <StockView />
          </Show>
        </main>
      </div>
    </Show>
  );
};

export default App;

