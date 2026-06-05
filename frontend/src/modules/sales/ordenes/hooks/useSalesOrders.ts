import { useCallback, useEffect, useState } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  salesOrderService,
  type CreateSalesOrderRequest,
  type SalesOrderDetail,
  type SalesOrdersFilter,
  type SalesOrdersPagedResult,
} from '../api/salesOrderService';

export function useSalesOrdersList(filter: SalesOrdersFilter = {}) {
  const [result, setResult] = useState<SalesOrdersPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    salesOrderService
      .list(filter)
      .then((data) => {
        if (!cancelled) {
          setResult(data);
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
  }, [filterKey, tick]);

  return { result, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useSalesOrderDetail(publicId: string | null) {
  const [data, setData] = useState<SalesOrderDetail | null>(null);
  const [loading, setLoading] = useState(!!publicId);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    if (!publicId) {
      setData(null);
      setLoading(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    salesOrderService
      .getByPublicId(publicId)
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
  }, [publicId, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useSalesOrderActions(onSuccess?: () => void) {
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

  const create = useCallback(
    (payload: CreateSalesOrderRequest) => run(() => salesOrderService.create(payload)),
    [run]
  );

  const confirm = useCallback(
    (publicId: string) => run(() => salesOrderService.confirm(publicId)),
    [run]
  );

  const invoice = useCallback(
    (publicId: string) => run(() => salesOrderService.createInvoice(publicId)),
    [run]
  );

  const cancel = useCallback(
    (publicId: string, reason?: string | null) => run(() => salesOrderService.cancel(publicId, reason)),
    [run]
  );

  return { loading, error, create, confirm, invoice, cancel };
}
