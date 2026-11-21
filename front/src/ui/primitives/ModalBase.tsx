import { Show, onMount, onCleanup, type JSX } from 'solid-js';

export interface ModalBaseProps {
  open: boolean;
  onClose: () => void;
  confirmClose?: () => boolean | Promise<boolean>;
  title?: string;
  size?: 'sm' | 'md' | 'lg';
  children: JSX.Element;
}

function ModalContent(props: ModalBaseProps) {
  let cardRef: HTMLDivElement | undefined;
  let isClosing = false;
  const previousActiveElement = document.activeElement as HTMLElement | null;
  const previousOverflow = document.body.style.overflow;

  const FOCUSABLE_SELECTOR =
    'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

  const requestClose = async () => {
    if (isClosing) return;
    if (props.confirmClose) {
      isClosing = true;
      try {
        const canClose = await props.confirmClose();
        if (canClose) {
          props.onClose();
        }
      } catch (err: unknown) {
        console.error('Error in confirmClose:', err);
      } finally {
        isClosing = false;
      }
    } else {
      props.onClose();
    }
  };

  const handleKeyDown = (e: KeyboardEvent) => {
    // 1. Escape: listener en window
    if (e.key === 'Escape') {
      e.preventDefault();
      void requestClose();
      return;
    }

    // 4. Focus trap: Tab / Shift+Tab ciclan dentro de modal-card
    if (e.key === 'Tab') {
      if (!cardRef) return;
      const focusables = Array.from(
        cardRef.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)
      ).filter((el) => el.offsetParent !== null);

      if (focusables.length === 0) {
        e.preventDefault();
        return;
      }

      const first = focusables[0];
      const last = focusables[focusables.length - 1];

      if (e.shiftKey) {
        if (document.activeElement === first || !cardRef.contains(document.activeElement)) {
          e.preventDefault();
          last.focus();
        }
      } else {
        if (document.activeElement === last || !cardRef.contains(document.activeElement)) {
          e.preventDefault();
          first.focus();
        }
      }
    }
  };

  onMount(() => {
    // 6. Body scroll lock: guarda overflow previo y setea hidden
    document.body.style.overflow = 'hidden';

    // 1. Escape: agrega listener en window
    window.addEventListener('keydown', handleKeyDown);

    // Initial focus trap
    setTimeout(() => {
      if (cardRef) {
        const focusables = Array.from(
          cardRef.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR)
        ).filter((el) => el.offsetParent !== null);
        if (focusables.length > 0) {
          focusables[0].focus();
        } else {
          cardRef.focus();
        }
      }
    }, 0);
  });

  onCleanup(() => {
    // 6. Restaura overflow previo del body
    document.body.style.overflow = previousOverflow;

    // 1. Remueve listener de window
    window.removeEventListener('keydown', handleKeyDown);

    // 5. Restaura foco previo
    if (previousActiveElement && typeof previousActiveElement.focus === 'function') {
      previousActiveElement.focus();
    }
  });

  const handleBackdropClick = (e: MouseEvent) => {
    // Click directo en backdrop cierra modal
    if (e.target === e.currentTarget) {
      void requestClose();
    }
  };

  return (
    <div class="modal-backdrop" onClick={handleBackdropClick} role="presentation">
      <div
        ref={cardRef}
        class={`modal-card modal-card--${props.size ?? 'md'}`}
        onClick={(e) => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby={props.title ? 'modal-title' : undefined}
        tabIndex={-1}
      >
        <div class="modal-card__header">
          {props.title && (
            <h3 id="modal-title" class="modal-card__title">
              {props.title}
            </h3>
          )}
          <button
            type="button"
            class="modal-card__close"
            onClick={() => void requestClose()}
            aria-label="Cerrar"
          >
            ✕
          </button>
        </div>
        <div class="modal-card__body">{props.children}</div>
      </div>
    </div>
  );
}

export function ModalBase(props: ModalBaseProps): JSX.Element {
  return (
    <Show when={props.open}>
      <ModalContent {...props} />
    </Show>
  );
}
