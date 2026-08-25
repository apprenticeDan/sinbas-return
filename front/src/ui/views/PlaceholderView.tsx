import { Component } from 'solid-js';

interface PlaceholderProps {
  title: string;
  featureCode: string;
  description: string;
}

export const PlaceholderView: Component<PlaceholderProps> = (props) => {
  return (
    <section class="panel">
      <div class="panel-head">
        <p class="panel-eyebrow">Módulo {props.featureCode}</p>
        <h1 class="panel-title">{props.title}</h1>
        <p class="panel-desc">{props.description}</p>
      </div>

      <div class="card" style={{ padding: '60px', 'text-align': 'center' }}>
        <div
          style={{
            width: '64px',
            height: '64px',
            'border-radius': '16px',
            background: 'var(--amber-tint)',
            color: 'var(--amber)',
            display: 'flex',
            'align-items': 'center',
            'justify-content': 'center',
            margin: '0 auto 20px',
          }}
        >
          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '32px', height: '32px' }}>
            <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
          </svg>
        </div>

        <h3 style={{ 'font-family': 'var(--font-display)', 'font-size': '20px', margin: '0 0 8px' }}>
          Próxima Etapa de Desarrollo (Feature {props.featureCode})
        </h3>
        <p style={{ color: 'var(--ink-soft)', 'max-width': '480px', margin: '0 auto 24px', 'font-size': '13.5px' }}>
          Este módulo está reservado para la siguiente iteración TDD según la especificación funcional.
        </p>

        <span class="pill pill-amber" style={{ 'font-size': '12px', padding: '6px 14px' }}>
          Estado: Pendiente de Implementación
        </span>
      </div>
    </section>
  );
};
