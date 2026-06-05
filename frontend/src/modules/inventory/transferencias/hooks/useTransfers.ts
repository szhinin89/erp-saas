import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  transferService,
  type TransfersFilter,
  type TransfersPagedResult,
  type TransferDetail,
  type CreateTransferRequest,
} from '../api/transferService';

// ── Lista paginada con filtros reactivos ────────────────────────────────────

export function useTransfersList(filter: TransfersFilter = {}) {
  const [result,  setResult]  = useState<TransfersPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    transferService
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

export function useTransferDetail(id: string | null) {
  const [data,    setData]    = useState<TransferDetail | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    setError(null);
    transferService
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

export function useTransferActions(onSuccess?: () => void) {
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

  const crear     = useCallback((p: CreateTransferRequest) => run(() => transferService.create(p)),     [run]);
  const confirmar = useCallback((id: string) => run(() => transferService.confirm(id)), [run]);
  const cancelar  = useCallback((id: string) => run(() => transferService.cancel(id)),  [run]);

  return { loading, error, crear, confirmar, cancelar };
}

// ── Lista de bodegas (para selectores) ──────────────────────────────────────

export function useBodegas() {
  const [data,    setData]    = useState<import('../api/transferService').WarehouseOption[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    transferService
      .getBodegas()
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, []);

  return { data, loading, error };
}
