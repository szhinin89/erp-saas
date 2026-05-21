import { useState, useEffect } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import { stockService, type StockFilter, type StockListItem } from '../api/stockService';

export function useStockList(filter: StockFilter | null) {
  const [items,   setItems]   = useState<StockListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = filter ? JSON.stringify(filter) : '';

  useEffect(() => {
    if (!filter?.warehouseId) {
      setItems([]);
      setLoading(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    stockService
      .getByWarehouse(filter)
      .then((rows) => {
        if (!cancelled) {
          setItems(rows);
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

  return {
    items,
    loading,
    error,
    refetch: () => setTick((t) => t + 1),
  };
}
