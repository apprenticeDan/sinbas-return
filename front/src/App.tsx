import { Component, Show } from 'solid-js';
import { isLoggedIn, activeTab } from './store/authStore';
import { Sidebar } from './components/Sidebar';
import { LoginView } from './views/LoginView';
import { UsersView } from './views/UsersView';
import { PlaceholderView } from './views/PlaceholderView';

export const App: Component = () => {
  return (
    <Show when={isLoggedIn()} fallback={<LoginView />}>
      <div class="app">
        <Sidebar />
        <main class="content">
          <Show when={activeTab() === 'usuarios'}>
            <UsersView />
          </Show>

          <Show when={activeTab() === 'ingresos'}>
            <PlaceholderView
              title="Registro de Ingresos"
              eyebrow="Almacén"
              description="Cada entrada de semillas, plantas, insumos o servicios al almacén, con su procedencia."
              featureCode="F4 / F-INV-01"
            />
          </Show>

          <Show when={activeTab() === 'proforma'}>
            <PlaceholderView
              title="Cotización (Proforma)"
              eyebrow="Comercial"
              description="Cotización para un cliente con disponibilidad de stock por ítem y estampado sin reserva."
              featureCode="F7 / F-COM-01"
            />
          </Show>

          <Show when={activeTab() === 'pedidos'}>
            <PlaceholderView
              title="Ventas y Pedidos"
              eyebrow="Comercial / Almacén"
              description="Ventas y usos internos por preparar, con sus etiquetas de despacho y selección FIFO."
              featureCode="F8 / F-COM-02"
            />
          </Show>

          <Show when={activeTab() === 'kardex'}>
            <PlaceholderView
              title="Kárdex e Inventario"
              eyebrow="Trazabilidad"
              description="Cálculo puro de existencias por proyección inmutable (fold) e historial completo de movimientos."
              featureCode="F5 / F-INV-08"
            />
          </Show>
        </main>
      </div>
    </Show>
  );
};
export default App;
