import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  purchaseOrderService,
  type PurchaseOrdersFilter,
  type PurchaseOrdersPagedResult,
  type PurchaseOrderDetail,
  type CreatePurchaseOrderRequest,
} from '../api/purchaseOrderService';

export function useOrdenesCompraList(filter: PurchaseOrdersFilter = {}) {
  const [result,  setResult]  = useState<PurchaseOrdersPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    purchaseOrderService
      .getAll(filter)
      .then((data) => { if (!cancelled) { setResult(data); setLoading(false); } })
      .catch((e)   => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, tick]);

  return { result, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function usePurchaseOrderDetail(id: string | null) {
  const [data,    setData]    = useState<PurchaseOrderDetail | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    purchaseOrderService
      .getById(id)
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, [id, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function usePurchaseOrderActions(onSuccess?: () => void) {
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

  const crear          = useCallback((p: CreatePurchaseOrderRequest) => run(() => purchaseOrderService.create(p)), [run]);
  const enviar         = useCallback((id: string) => run(() => purchaseOrderService.send(id)),         [run]);
  const aprobar        = useCallback((id: string) => run(() => purchaseOrderService.approve(id)),        [run]);
  const cancelar       = useCallback((id: string) => run(() => purchaseOrderService.cancel(id)),       [run]);
  const vincularFactura = useCallback(
    (id: string, facturaId: string) => run(() => purchaseOrderService.linkInvoice(id, facturaId)),
    [run]
  );

  return { loading, error, crear, enviar, aprobar, cancelar, vincularFactura };
}
