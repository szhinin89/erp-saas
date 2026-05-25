import { useCallback, useEffect, useState } from 'react';
import { formatApiError } from '../../lib/formatApiError';
import {
  ventasFacturasService,
  type VentasFacturaDetailDto,
} from '../api/ventasFacturasService';

export function useVentasFacturaDetail(id: string | null) {
  const [data, setData] = useState<VentasFacturaDetailDto | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!id) {
      setData(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    ventasFacturasService
      .getById(id)
      .then((d) => {
        if (!cancelled) {
          setData(d);
          setLoading(false);
        }
      })
      .catch((e) => {
        if (!cancelled) {
          setError(formatApiError(e));
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [id, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useVentasFacturaActions(onSuccess?: () => void) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const run = useCallback(
    async (action: () => Promise<unknown>) => {
      setError(null);
      setLoading(true);
      try {
        const result = await action();
        onSuccess?.();
        return result;
      } catch (e) {
        setError(formatApiError(e));
        return null;
      } finally {
        setLoading(false);
      }
    },
    [onSuccess]
  );

  const validate = useCallback((id: string) => run(() => ventasFacturasService.validate(id)), [run]);
  const issue = useCallback((id: string) => run(() => ventasFacturasService.issue(id)), [run]);
  const voidInvoice = useCallback((id: string) => run(() => ventasFacturasService.void(id)), [run]);
  const retry = useCallback((id: string) => run(() => ventasFacturasService.retry(id)), [run]);

  return { loading, error, validate, issue, voidInvoice, retry };
}
