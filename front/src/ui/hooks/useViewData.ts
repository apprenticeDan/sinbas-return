import { createSignal, onMount, onCleanup, type Accessor } from 'solid-js';

export interface UseViewDataOptions {
  interval?: number; // ms; 0/ausente = sin auto-refresh. Default: 0.
  enabled?: Accessor<boolean>; // default: () => true
}

export interface ViewDataResult<T> {
  data: Accessor<T | null>;
  loading: Accessor<boolean>;
  error: Accessor<string | null>;
  refresh: () => void;
}

export function useViewData<T>(
  loader: () => Promise<T>,
  options?: UseViewDataOptions
): ViewDataResult<T> {
  const [data, setData] = createSignal<T | null>(null);
  const [loading, setLoading] = createSignal<boolean>(true);
  const [error, setError] = createSignal<string | null>(null);

  const interval = options?.interval ?? 0;
  const enabled = options?.enabled ?? (() => true);

  let timerId: ReturnType<typeof setInterval> | null = null;
  let isMounted = true;

  const stopInterval = () => {
    if (timerId !== null) {
      clearInterval(timerId);
      timerId = null;
    }
  };

  const startInterval = () => {
    stopInterval();
    if (interval > 0 && enabled()) {
      timerId = setInterval(() => {
        if (isMounted && enabled()) {
          void executeLoader();
        }
      }, interval);
    }
  };

  const executeLoader = async () => {
    if (!isMounted) return;
    setLoading(true);
    try {
      const result = await loader();
      if (!isMounted) return;
      setData(() => result);
      setError(null);
    } catch (err: unknown) {
      if (!isMounted) return;
      const message = err instanceof Error ? err.message : String(err);
      setError(message);
    } finally {
      if (isMounted) {
        setLoading(false);
      }
    }
  };

  const refresh = () => {
    stopInterval();
    void executeLoader().then(() => {
      if (isMounted) {
        startInterval();
      }
    });
  };

  onMount(() => {
    isMounted = true;
    void executeLoader().then(() => {
      if (isMounted) {
        startInterval();
      }
    });
  });

  onCleanup(() => {
    isMounted = false;
    stopInterval();
  });

  return {
    data,
    loading,
    error,
    refresh,
  };
}
