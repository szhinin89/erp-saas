import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  adjustmentService,
  type AdjustmentsFilter,
  type AdjustmentsPagedResult,
  type InventoryAdjustment,
  type CreateAdjustmentRequest,
} from '../api/adjustmentService';

export function useAdjustmentsList(filter: AdjustmentsFilter = {}) {
  const [result,  setResult]  = useState<AdjustmentsPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    adjustmentService
      .getAll(filter)
      .then((data) => { if (!cancelled) { setResult(data); setLoading(false); } })
      .catch((e)   => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, tick]);

  return { result, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useAdjustmentDetail(id: string | null) {
  const [data,    setData]    = useState<InventoryAdjustment | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    adjustmentService
      .getById(id)
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, [id, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useAdjustmentActions(onSuccess?: () => void) {
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

  const crear    = useCallback((p: CreateAdjustmentRequest) => run(() => adjustmentService.create(p)),    [run]);
  const ejecutar = useCallback((id: string) => run(() => adjustmentService.execute(id)), [run]);
  const cancelar = useCallback((id: string) => run(() => adjustmentService.cancel(id)),  [run]);

  return { loading, error, crear, ejecutar, cancelar };
}
