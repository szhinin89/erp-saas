import { useState, useEffect, useCallback } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import {
  ordenCompraService,
  type OrdenesCompraFilter,
  type OrdenesCompraPagedResult,
  type OrdenCompraDetail,
  type CrearOrdenCompraRequest,
} from '../api/ordenCompraService';

export function useOrdenesCompraList(filter: OrdenesCompraFilter = {}) {
  const [result,  setResult]  = useState<OrdenesCompraPagedResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  const filterKey = JSON.stringify(filter);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    ordenCompraService
      .getAll(filter)
      .then((data) => { if (!cancelled) { setResult(data); setLoading(false); } })
      .catch((e)   => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filterKey, tick]);

  return { result, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useOrdenCompraDetalle(id: string | null) {
  const [data,    setData]    = useState<OrdenCompraDetail | null>(null);
  const [loading, setLoading] = useState(!!id);
  const [error,   setError]   = useState<string | null>(null);
  const [tick,    setTick]    = useState(0);

  useEffect(() => {
    if (!id) { setData(null); setLoading(false); return; }
    let cancelled = false;
    setLoading(true);
    ordenCompraService
      .getById(id)
      .then((d) => { if (!cancelled) { setData(d); setLoading(false); } })
      .catch((e) => { if (!cancelled) { setError(formatApiError(e)); setLoading(false); } });
    return () => { cancelled = true; };
  }, [id, tick]);

  return { data, loading, error, refetch: () => setTick((t) => t + 1) };
}

export function useOrdenCompraAcciones(onSuccess?: () => void) {
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

  const crear          = useCallback((p: CrearOrdenCompraRequest) => run(() => ordenCompraService.crear(p)), [run]);
  const enviar         = useCallback((id: string) => run(() => ordenCompraService.enviar(id)),         [run]);
  const aprobar        = useCallback((id: string) => run(() => ordenCompraService.aprobar(id)),        [run]);
  const cancelar       = useCallback((id: string) => run(() => ordenCompraService.cancelar(id)),       [run]);
  const vincularFactura = useCallback(
    (id: string, facturaId: string) => run(() => ordenCompraService.vincularFactura(id, facturaId)),
    [run]
  );

  return { loading, error, crear, enviar, aprobar, cancelar, vincularFactura };
}
