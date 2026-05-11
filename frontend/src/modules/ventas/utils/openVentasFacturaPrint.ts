import { api } from '../../lib/api';

export type OpenVentasFacturaPrintResult =
  | { ok: true }
  | { ok: false; reason: 'popup_blocked' | 'request_failed'; message?: string };

/**
 * Descarga el HTML de tirilla con la sesión actual (Bearer / cookies) y abre una ventana para imprimir.
 */
export async function openVentasFacturaPrint(facturaId: string): Promise<OpenVentasFacturaPrintResult> {
  try {
    const res = await api.get<string>(`/api/ventas/${facturaId}/imprimir`, {
      responseType: 'text',
      headers: { Accept: 'text/html' },
    });
    const html = typeof res.data === 'string' ? res.data : String(res.data ?? '');
    const blob = new Blob([html], { type: 'text/html;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const printWindow = window.open(url, '_blank', 'noopener,noreferrer');
    if (!printWindow) {
      URL.revokeObjectURL(url);
      return { ok: false, reason: 'popup_blocked' };
    }

    const runPrint = () => {
      try {
        printWindow.focus();
        printWindow.print();
      } catch {
        /* ignore */
      }
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
    };

    if (printWindow.document.readyState === 'complete') {
      window.setTimeout(runPrint, 200);
    } else {
      printWindow.addEventListener('load', () => window.setTimeout(runPrint, 200), { once: true });
    }

    return { ok: true };
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : undefined;
    return { ok: false, reason: 'request_failed', message };
  }
}
