import { useCallback, useState } from 'react';
import { formatApiError } from '../../../lib/formatApiError';
import { kardexService, type KardexFilter, type KardexResponse } from '../api/kardexService';

export function useKardexConsulta() {
  const [data,      setData]      = useState<KardexResponse | null>(null);
  const [loading,   setLoading]   = useState(false);
  const [error,     setError]     = useState<string | null>(null);
  const [progress,  setProgress]  = useState<string | null>(null);
  const [exporting, setExporting] = useState<'excel' | 'pdf' | null>(null);

  const consultar = useCallback(async (filter: KardexFilter) => {
    setLoading(true);
    setError(null);
    setProgress(null);
    setData(null);

    try {
      const result = await kardexService.getKardex(filter, setProgress);
      setData(result);
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setLoading(false);
      setProgress(null);
    }
  }, []);

  const exportFile = useCallback(
    async (filter: KardexFilter, kind: 'excel' | 'pdf') => {
      setExporting(kind);
      setError(null);
      try {
        const blob = kind === 'excel'
          ? await kardexService.exportExcel(filter)
          : await kardexService.exportPdf(filter);

        const ext  = kind === 'excel' ? 'xlsx' : 'pdf';
        const code = data?.producto.codigo?.replace(/\//g, '-') ?? 'producto';
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = `Kardex_${code}.${ext}`;
        a.click();
        URL.revokeObjectURL(url);
      } catch (e) {
        setError(formatApiError(e));
      } finally {
        setExporting(null);
      }
    },
    [data?.producto.codigo],
  );

  return {
    data,
    loading,
    error,
    progress,
    exporting,
    consultar,
    exportFile,
    clear: () => setData(null),
  };
}
