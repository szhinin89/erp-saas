import { useCallback, useEffect, useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { withholdingReceivedService, type WithholdingReceivedItem } from '../api/withholdingReceivedService';

export function WithholdingReceivedPage() {
  const { t } = useI18n();
  const hasPerm = usePermissionsStore((s) => s.has);
  const role    = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm('sales.withholding-received.view');

  const [rows,    setRows]    = useState<WithholdingReceivedItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error,   setError]   = useState<string | null>(null);
  const [q,       setQ]       = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await withholdingReceivedService.list());
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return rows;
    return rows.filter((r) => r.accessKey.toLowerCase().includes(query));
  }, [rows, q]);

  if (!canView) return <NoAccessPage title={t('app.nav.item.sales.withholding-received')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('app.nav.item.sales.withholding-received')}
      subtitle="Retenciones en la fuente recibidas de clientes sobre facturas de venta."
      action={
        <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void load()}>
          <span className="material-symbols-outlined">refresh</span>
          Actualizar
        </button>
      }
    >
      {error && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Buscar por clave de acceso…"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
          </div>
          <div className="pg-table-controls-right">
            <span>{filtered.length} de {rows.length}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="No hay retenciones recibidas registradas." /></div>
        ) : filtered.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="Sin resultados para la búsqueda." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Clave de acceso</th>
                  <th>Fecha</th>
                  <th className="pg-th-right">Retenido</th>
                  <th>Factura venta</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => (
                  <tr key={row.id}>
                    <td className="mono" title={row.accessKey}>{row.accessKey || '—'}</td>
                    <td>{new Date(row.issueDate).toLocaleDateString('es')}</td>
                    <td className="pg-td-right">${row.retainedAmount.toFixed(2)}</td>
                    <td className="mono">{row.salesBillId ?? '—'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}
