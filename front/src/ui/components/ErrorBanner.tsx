import { Component, Show } from 'solid-js';

interface ErrorBannerProps {
  message: string | null;
  onClose?: () => void;
  type?: 'error' | 'warning' | 'info';
}

export const ErrorBanner: Component<ErrorBannerProps> = (props) => {
  return (
    <Show when={props.message}>
      <div
        class={props.type === 'warning' ? 'alert-warning' : 'alert-error'}
        style={{
          display: 'flex',
          'justify-content': 'space-between',
          'align-items': 'center',
          'margin-bottom': '14px',
          padding: '10px 14px',
          'border-radius': 'var(--radius-s, 6px)',
        }}
      >
        <span style={{ 'font-size': '13px' }}>{props.message}</span>
        <Show when={props.onClose}>
          <button
            type="button"
            onClick={props.onClose}
            class="btn btn-ghost"
            style={{
              padding: '2px 8px',
              'font-size': '14px',
              'line-height': '1',
              border: 'none',
              background: 'transparent',
              cursor: 'pointer',
            }}
            title="Cerrar"
          >
            ✕
          </button>
        </Show>
      </div>
    </Show>
  );
};

