import { useCallback, useEffect, useRef, useState } from "react";
import { receivableService, type SalesReceivableDto } from "../api/receivableService";

export const RECEIVABLES_PAGE_SIZE = 50;

/**
 * CxC de un cliente, paginadas y filtradas en el servidor (`customerId`). Carga la primera página;
 * las siguientes solo cuando el usuario las pide.
 */
export function useCustomerReceivables(customerId: string | null, status?: string) {
  const [receivables, setReceivables] = useState<SalesReceivableDto[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [failed, setFailed] = useState(false);
  const [version, setVersion] = useState(0);
  // Una respuesta de un cliente o recarga anterior nunca se mezcla con la actual.
  const generation = useRef(0);
  const nextPage = useRef(1);
  const busy = useRef(false);

  const fetchPage = useCallback(
    async (id: string, current: number) => {
      busy.current = true;
      setLoading(true);
      setFailed(false);
      const page = nextPage.current;
      try {
        const result = await receivableService.list(status, page, RECEIVABLES_PAGE_SIZE, id);
        if (current !== generation.current) return;
        setReceivables((loaded) => (page === 1 ? result.items : [...loaded, ...result.items]));
        setTotal(result.total);
        nextPage.current = page + 1;
      } catch {
        if (current === generation.current) setFailed(true);
      } finally {
        if (current === generation.current) {
          busy.current = false;
          setLoading(false);
        }
      }
    },
    [status],
  );

  useEffect(() => {
    const current = ++generation.current;
    nextPage.current = 1;
    busy.current = false;
    setReceivables([]);
    setTotal(0);
    if (customerId) void fetchPage(customerId, current);
  }, [customerId, version, fetchPage]);

  const loadMore = useCallback(() => {
    if (customerId && !busy.current) void fetchPage(customerId, generation.current);
  }, [customerId, fetchPage]);
  const reload = useCallback(() => setVersion((v) => v + 1), []);

  return {
    receivables,
    total,
    loading,
    failed,
    hasMore: receivables.length < total,
    loadMore,
    reload,
  };
}
