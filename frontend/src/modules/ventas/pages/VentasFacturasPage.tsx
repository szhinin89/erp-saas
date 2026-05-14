import { useCallback, useEffect, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage, PageShell } from '../../../components/PageShell';
import { Card } from '../../../components/ui/Card';
import { ZHBtn } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { ventasFacturasService, type VentasFacturaDto } from '../api/ventasFacturasService';
import { openVentasFacturaPrint } from '../utils/openVentasFacturaPrint';
import './ventas-facturas-page.css';

export function VentasFacturasPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const canView = hasPerm('ventas.facturas.view');

  const [rows, setRows] = useState<VentasFacturaDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [printError, setPrintError] = useState<string | null>(null);
  const [printingId, setPrintingId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const page = await ventasFacturasService.list({ pageNumber: 1, pageSize: 100 });
      setRows(page.items);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    if (canView) void load();
  }, [canView, load]);

  const onPrint = async (id: string) => {
    setPrintError(null);
    setPrintingId(id);
    const result = await openVentasFacturaPrint(id);
    setPrintingId(null);
    if (!result.ok) {
      if (result.reason === 'popup_blocked') {
        setPrintError(t('ventas.facturas.printPopupBlocked'));
      } else {
        setPrintError(result.message ?? t('ventas.facturas.printFailed'));
      }
    }
  };

  if (!canView) {
    return <NoAccessPage title={t('ventas.facturas.title')} />;
  }

  return (
    <PageShell kicker={t('app.nav.group.sales')} title={t('ventas.facturas.title')}>
      <Card>
        {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
        {printError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={printError} /> : null}

        <div className="vf-refresh-wrap">
          <ZHBtn variant="secondary" size="md" type="button" disabled={loading} onClick={() => void load()}>
            {t('ventas.facturas.refresh')}
          </ZHBtn>
        </div>

        {loading ? (
          <LoadingState />
        ) : rows.length === 0 ? (
          <EmptyState message={t('common.noData')} />
        ) : (
          <table className="table vf-responsive-table">
            <thead>
              <tr>
                <th>{t('ventas.facturas.col.number')}</th>
                <th>{t('ventas.facturas.col.customer')}</th>
                <th>{t('ventas.facturas.col.date')}</th>
                <th>{t('ventas.facturas.col.status')}</th>
                <th className="vf-col-right">{t('ventas.facturas.col.total')}</th>
                <th>{t('ventas.facturas.col.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((row) => {
                const numero = `${row.establecimiento}-${row.puntoEmision}-${row.secuencial}`;
                const canPrint = row.estado === 'Autorizado';
                return (
                  <tr key={row.id}>
                    <td data-label={t('ventas.facturas.col.number')}>{numero}</td>
                    <td data-label={t('ventas.facturas.col.customer')}>{row.clienteNombre}</td>
                    <td data-label={t('ventas.facturas.col.date')}>{new Date(row.fechaEmision).toLocaleString()}</td>
                    <td data-label={t('ventas.facturas.col.status')}>{row.estado}</td>
                    <td data-label={t('ventas.facturas.col.total')} className="vf-cell-right">{row.total.toFixed(2)}</td>
                    <td data-label={t('ventas.facturas.col.actions')} className="vf-cell-actions">
                      <ZHBtn
                        variant="primary"
                        size="sm"
                        type="button"
                        disabled={!canPrint || printingId === row.id}
                        onClick={() => void onPrint(row.id)}
                      >
                        {printingId === row.id ? t('common.loading') : t('ventas.facturas.print')}
                      </ZHBtn>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
      </Card>
    </PageShell>
  );
}
