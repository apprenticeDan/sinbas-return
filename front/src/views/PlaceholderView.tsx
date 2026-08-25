import { Component } from 'solid-js';

interface PlaceholderViewProps {
  title: string;
  eyebrow: string;
  description: string;
  featureCode: string;
}

export const PlaceholderView: Component<PlaceholderViewProps> = (props) => {
  return (
    <section class="panel">
      <div class="panel-head">
        <p class="panel-eyebrow">{props.eyebrow}</p>
        <h1 class="panel-title">{props.title}</h1>
        <p class="panel-desc">{props.description}</p>
      </div>

      <div class="stack">
        <div class="card">
          <div class="toolbar" style={{ justifyContent: 'space-between' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <span class="pill pill-amber">Módulo de Negocio ({props.featureCode})</span>
              <span style={{ fontSize: '12px', color: 'var(--ink-soft)' }}>
                Especificado en Docs F, G y H. Listo para desarrollo TDD.
              </span>
            </div>
            <button class="btn btn-ghost" disabled>
              + Nueva Entrada (Próximamente)
            </button>
          </div>

          <div style={{ padding: '60px 20px', textAlign: 'center' }}>
            <div
              style={{
                width: '48px',
                height: '48px',
                borderRadius: '50%',
                background: 'var(--amber-tint)',
                color: 'var(--earth)',
                display: 'inline-flex',
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: '12px',
              }}
            >
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" style={{ width: '24px', height: '24px' }}>
                <path d="M12 21V10" />
                <path d="M12 10C12 6 9 4 5 4c0 4.5 2.8 7 7 7Z" />
                <path d="M12 13c0-4 3-6 7-6 0 4.2-2.6 6.6-7 6.6Z" />
              </svg>
            </div>
            <h3 style={{ fontFamily: 'var(--font-display)', margin: '0 0 6px', fontSize: '18px' }}>
              Vista de Wireframe: {props.title}
            </h3>
            <p style={{ color: 'var(--ink-soft)', margin: '0 auto', maxWidth: '500px', fontSize: '13px' }}>
              El backend F# ya cuenta con los contratos API REST (`Doc G`) especificados para esta vista. Se implementará de forma guiada por pruebas (TDD) en el sprint correspondiente.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
};
