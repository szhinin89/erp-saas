import { useState, useEffect, useCallback } from "react";
import { useAuthStore } from "../store/authStore";
import { formatApiError } from "../modules/lib/formatApiError";

interface AsyncState<T> {
  data: T | null;
  loading: boolean;
  error: string | null;
  /** Incrementa el tick para volver a ejecutar la función, útil tras crear/editar datos. */
  refetch: () => void;
}

/**
 * Hook genérico para ejecutar una función async y exponer su estado (loading/error/data).
 * Incluye `companySessionVersion` en deps para refetch tras switch-company.
 */
export function useAsync<T>(
  fn: () => Promise<T>,
  enabled = true,
  extraDeps: readonly unknown[] = [],
): AsyncState<T> {
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(enabled);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const refetch = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    if (!enabled) {
      Promise.resolve().then(() => {
        if (!cancelled) {
          setLoading(false);
          setError(null);
          setData(null);
        }
      });
      return () => {
        cancelled = true;
      };
    }

    Promise.resolve().then(() => {
      if (cancelled) return;
      setLoading(true);
      setError(null);
    });

    fn()
      .then((result) => {
        if (!cancelled) {
          setData(result);
          setLoading(false);
        }
      })
      .catch((err) => {
        if (!cancelled) {
          setError(formatApiError(err));
          setLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tick, enabled, companySessionVersion, ...extraDeps]);

  return { data, loading, error, refetch };
}
