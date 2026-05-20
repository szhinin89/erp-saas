import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  transferenciaService,
  type TransferenciasFilter,
  type TransferenciasPagedResult,
  type TransferenciaDetail,
  type CrearTransferenciaRequest,
} from '../api/transferenciaService';

// ── Lista paginada con filtros reactivos ────────────────────────────────────

export function useTransferenciasList(filter: TransferenciasFilter = {}) {
  const [result,  setResult]  = useState<TransferenciasPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    transferenciaService
      .getAll(filter)
      .then((data) => { if (!cancelled) { setResult(data); setLoading(false); } })
      .catch((e)  => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, tick]);

  return {
    result,
    loading,
    error,
    refetch: () => setTick((t) => t + 1),
  };
}

// ── Detalle de una transferencia ─────────────────────────────────────────────

export function useTransferenciaDetalle(id: string | null) {
  const [data,    setData]    = useState<TransferenciaDetail | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    setError(null);
    transferenciaService
      .getById(id)
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, [id, tick]);

  return {
    data,
    loading,
    error,
    refetch: () => setTick((t) => t + 1),
  };
}

// ── Acciones (crear / confirmar / cancelar) ──────────────────────────────────

export function useTransferenciaAcciones(onSuccess?: () => void) {
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);

  const run = useCallback(
    async (action: () => Promise<unknown>) => {
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
    },
    [onSuccess]
  );

  const crear     = useCallback((p: CrearTransferenciaRequest) => run(() => transferenciaService.crear(p)),     [run]);
  const confirmar = useCallback((id: string) => run(() => transferenciaService.confirmar(id)), [run]);
  const cancelar  = useCallback((id: string) => run(() => transferenciaService.cancelar(id)),  [run]);

  return { loading, error, crear, confirmar, cancelar };
}

// ── Lista de bodegas (para selectores) ──────────────────────────────────────

export function useBodegas() {
  const [data,    setData]    = useState<import('../api/transferenciaService').BodegaOpcion[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    transferenciaService
      .getBodegas()
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, []);

  return { data, loading, error };
}
