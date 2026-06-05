import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { creditNotesService, type SalesNoteDto } from '../api/creditNotesService';
import './credit-notes-page.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';

function getStatusBadge(status: string) {
  const s = status.toLowerCase();
  const base = 'badge badge--md badge--upper';
  if (s === 'autorizado')                                          return `${base} badge--green`;
  if (s === 'borrador' || s === 'validado' || s === 'procesando') return `${base} badge--orange`;
  if (s === 'anulado' || s === 'rechazado' || s === 'errorenvio') return `${base} badge--red`;
  return `${base} badge--gray`;
}

function noteTypeLabel(noteType: string, t: (k: string) => string) {
  const nt = noteType.toUpperCase();
  if (nt === 'CREDITO' || nt === 'CREDIT') return t('ventas.notas.typeCredit');
  if (nt === 'DEBITO'  || nt === 'DEBIT')  return t('ventas.notas.typeDebit');
  return noteType;
}

export function CreditNotesPage() {
  const { canShow } = usePermissionsUi();
  const { t }     = useI18n();
  const navigate  = useNavigate();
  const canView   = canShow('sales.credit-notes.view');
  const canCreate = canShow('sales.credit-notes.create');
  const canSend   = canShow('sales.credit-notes.send');

  const [rows,      setRows]      = useState<SalesNoteDto[]>([]);
  const [loading,   setLoading]   = useState(true);
  const [error,     setError]     = useState<string | null>(null);
  const [sendError, setSendError] = useState<string | null>(null);
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [typeFilter, setTypeFilter] = useState<'all' | 'CREDITO' | 'DEBITO'>('all');
  const [q, setQ] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await creditNotesService.list();
      setRows(data);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const filtered = useMemo(() => {
    let result = rows;
    if (typeFilter !== 'all') {
      result = result.filter((r) => r.noteType.toUpperCase() === typeFilter);
    }
    const query = q.trim().toLowerCase();
    if (query) {
      result = result.filter(
        (r) => r.accessKey.toLowerCase().includes(query) || r.originalInvoiceId.toLowerCase().includes(query),
      );
    }
    return result;
  }, [rows, typeFilter, q]);

  const onSend = async (id: string) => {
    setSendError(null);
    setSendingId(id);
    try {
      await creditNotesService.send(id);
      await load();
    } catch (e) {
      setSendError(e instanceof Error ? e.message : t('ventas.notas.sendError'));
    } finally {
      setSendingId(null);
    }
  };

  if (!canView) return <NoAccessPage title={t('ventas.notas.title')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.notas.title')}
      subtitle={t('ventas.notas.subtitle')}
      action={
        <>
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            <span className="material-symbols-outlined">refresh</span>
            {t('ventas.notas.refresh')}
          </button>
          {canCreate && (
            <button
              className="zh-btn zh-btn--primary"
              type="button"
              onClick={() => navigate('/sales/credit-notes/new')}
            >
              <span className="material-symbols-outlined">add</span>
              {t('ventas.notas.new')}
            </button>
          )}
        </>
      }
    >
      {error     && <ZHPageNotice variant="error" message={t('ventas.notas.loadError')} detail={error} />}
      {sendError && <ZHPageNotice variant="error" message={t('ventas.notas.sendError')} detail={sendError} />}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">description</span>
            <span className="pg-section-label">{t('ventas.notas.sectionTitle')}</span>
          </div>
        </div>

        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder={t('ventas.notas.searchPlaceholder')}
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'all' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('all')}
            >
              {t('ventas.notas.filter.all')}
            </button>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'CREDITO' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('CREDITO')}
            >
              {t('ventas.notas.filter.credit')}
            </button>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'DEBITO' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('DEBITO')}
            >
              {t('ventas.notas.filter.debit')}
            </button>
          </div>
          <div className="pg-table-controls-right">
            <span>{t('ventas.notas.showing', { shown: filtered.length, total: rows.length })}</span>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message={t('ventas.notas.empty')} /></div>
        ) : filtered.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message={t('ventas.notas.noMatch')} /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('ventas.notas.col.type')}</th>
                  <th>{t('ventas.notas.col.accessKey')}</th>
                  <th>{t('ventas.notas.col.status')}</th>
                  <th className="cn-col-right">{t('ventas.notas.col.total')}</th>
                  <th className="cn-col-right">{t('ventas.notas.col.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const isBorrador = row.status.toLowerCase() === 'borrador';
                  return (
                    <tr key={row.id}>
                      <td>{noteTypeLabel(row.noteType, t)}</td>
                      <td className="mono">{row.accessKey || '—'}</td>
                      <td><span className={getStatusBadge(row.status)}>{row.status}</span></td>
                      <td className="cn-col-right">${row.total.toFixed(2)}</td>
                      <td className="cn-col-right">
                        {isBorrador && canSend && (
                          <button
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            type="button"
                            disabled={sendingId === row.id}
                            onClick={() => void onSend(row.id)}
                          >
                            {t('ventas.notas.send')}
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
