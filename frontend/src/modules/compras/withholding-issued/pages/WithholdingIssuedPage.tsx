import { useCallback, useEffect, useMemo, useState } from 'react';
import { EmptyState, LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ErpPageTemplate } from '../../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../../i18n/i18n';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import {
  withholdingIssuedService,
  type WithholdingIssuedItem,
} from '../api/withholdingIssuedService';
import { formatDate } from '../../../../lib/formatters/dateFormatters';

function statusBadge(status: string): string {
  const s = status.toLowerCase();
  const base = 'badge badge--md badge--upper';
  if (s === 'sent' || s === 'enviado' || s === 'autorizado') return `${base} badge--green`;
  if (s === 'draft' || s === 'borrador') return `${base} badge--orange`;
  return `${base} badge--gray`;
}

export function WithholdingIssuedPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow('purchases.withholding-issued.view');
  const canSend = canShow('purchases.withholding-issued.send');

  const [rows,      setRows]      = useState<WithholdingIssuedItem[]>([]);
  const [loading,   setLoading]   = useState(true);
  const [error,     setError]     = useState<string | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [q,         setQ]         = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRows(await withholdingIssuedService.list());
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

  const onSend = async (id: string) => {
    setSendError(null);
    setSendingId(id);
    try {
      await withholdingIssuedService.send(id);
      await load();
    } catch (e) {
      setSendError(e instanceof Error ? e.message : 'Error al enviar');
    } finally {
      setSendingId(null);
    }
  };

  if (!canView) return <NoAccessPage title={t('app.nav.item.purchases.withholding-issued')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.purchases')}
      title={t('app.nav.item.purchases.withholding-issued')}
      subtitle="Comprobantes de retención emitidos a proveedores."
      action={
        <button className="zh-btn zh-btn--secondary" type="button" disabled={loading} onClick={() => void load()}>
          <span className="material-symbols-outlined">refresh</span>
          Actualizar
        </button>
      }
    >
      {error     && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}
      {sendError && <ZHPageNotice variant="error" message="Error al enviar" detail={sendError} />}

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
          <div className="pg-pad-40"><EmptyState message="No hay retenciones emitidas." /></div>
        ) : filtered.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message="Sin resultados para la búsqueda." /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>Clave de acceso</th>
                  <th>Estado</th>
                  <th className="pg-th-right">Retenido</th>
                  <th>Fecha</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const isDraft = row.status.toLowerCase() === 'draft' || row.status.toLowerCase() === 'borrador';
                  return (
                    <tr key={row.id}>
                      <td className="mono" title={row.accessKey}>{row.accessKey || '—'}</td>
                      <td><span className={statusBadge(row.status)}>{row.status}</span></td>
                      <td className="pg-td-right">${row.totalRetained.toFixed(2)}</td>
                      <td>{formatDate(row.issueDate)}</td>
                      <td>
                        {isDraft && canSend && (
                          <button
                            className="zh-btn zh-btn--primary zh-btn--sm"
                            type="button"
                            disabled={sendingId === row.id}
                            onClick={() => void onSend(row.id)}
                          >
                            {sendingId === row.id ? 'Enviando…' : 'Enviar'}
                          </button>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </ErpPageTemplate>
  );
}
