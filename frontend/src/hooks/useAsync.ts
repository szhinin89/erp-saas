import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../lib/formatApiError';

interface AsyncState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  /** Incrementa el tick para volver a ejecutar la función, útil tras crear/editar datos. */
  refetch: () => void;
}

/**
 * Hook genérico para ejecutar una función async y exponer su estado (loading/error/data).
 * La función `fn` se vuelve a ejecutar cada vez que se llama a `refetch`.
 *
 * Cancela la actualización de estado si el componente se desmonta antes de que
 * la promise resuelva, evitando el warning "Can't perform state update on unmounted component".
 */
export function useAsync<T>(fn: () => Promise<T>): AsyncState<T> {
  const [data, setData]       = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError]     = useState<string | null>(null);
  const [tick, setTick]       = useState(0);

  const refetch = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    // Avoid synchronous setState inside effect body (perf/lint rule).
    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
    });

    fn()
      .then((result) => { if (!cancelled) { setData(result); setLoading(false); } })
      .catch((err) => {
        if (!cancelled) {
          setError(formatApiError(err));
          setLoading(false);
        }
      });

    return () => { cancelled = true; };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tick]);

  return { data, loading, error, refetch };
}
