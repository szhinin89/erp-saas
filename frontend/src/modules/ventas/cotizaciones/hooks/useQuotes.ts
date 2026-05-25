import { useCallback, useEffect, useState } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  quoteService,
  type ConvertQuoteToOrderRequest,
  type CreateQuoteRequest,
  type QuoteDetail,
  type QuotesFilter,
  type QuotesPagedResult,
} from '../api/quoteService';

export function useQuotesList(filter: QuotesFilter = {}) {
  const [result, setResult] = useState<QuotesPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    quoteService
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

export function useQuoteDetail(publicId: string | null) {
  const [data, setData] = useState<QuoteDetail | null>(null);
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
    quoteService
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

export function useQuoteActions(onSuccess?: () => void) {
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
    (payload: CreateQuoteRequest) => run(() => quoteService.create(payload)),
    [run]
  );

  const approve = useCallback(
    (publicId: string) => run(() => quoteService.approve(publicId)),
    [run]
  );

  const convertToOrder = useCallback(
    (payload: ConvertQuoteToOrderRequest) => run(() => quoteService.convertToOrder(payload)),
    [run]
  );

  const cancel = useCallback(
    (publicId: string, reason?: string | null) => run(() => quoteService.cancel(publicId, reason)),
    [run]
  );

  return { loading, error, create, approve, convertToOrder, cancel };
}
