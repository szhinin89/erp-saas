import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  ajusteService,
  type AjustesFilter,
  type AjustesPagedResult,
  type AjusteInventario,
  type CrearAjusteRequest,
} from '../api/ajusteService';

export function useAjustesList(filter: AjustesFilter = {}) {
  const [result,  setResult]  = useState<AjustesPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    ajusteService
      .getAll(filter)
      .then((data) => { if (!cancelled) { setResult(data); setLoading(false); } })
      .catch((e)   => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, tick]);

  return { result, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useAjusteDetalle(id: string | null) {
  const [data,    setData]    = useState<AjusteInventario | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    ajusteService
      .getById(id)
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, [id, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useAjusteAcciones(onSuccess?: () => void) {
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);

  const run = useCallback(async (action: () => Promise<unknown>) => {
    setError(null);
    setLoading(true);
    try {
      await action();
      onSuccess?.();
      return true;
    } catch (e) {
      setError(formatApiError(e));
      return false;
    } finally {
      setLoading(false);
    }
  }, [onSuccess]);

  const crear    = useCallback((p: CrearAjusteRequest) => run(() => ajusteService.crear(p)),    [run]);
  const ejecutar = useCallback((id: string) => run(() => ajusteService.ejecutar(id)), [run]);
  const cancelar = useCallback((id: string) => run(() => ajusteService.cancelar(id)),  [run]);

  return { loading, error, crear, ejecutar, cancelar };
}
