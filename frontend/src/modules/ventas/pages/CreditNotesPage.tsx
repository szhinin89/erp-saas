import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { useAuthStore } from '../../../store/authStore';
import { creditNotesService, type SalesNoteDto } from '../api/creditNotesService';
import './credit-notes-page.css';

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
  const { t }     = useI18n();
  const navigate  = useNavigate();
  const hasPerm   = usePermissionsStore((s) => s.has);
  const role      = useAuthStore((s) => s.user?.role ?? '');
  const isAdmin   = role === 'Admin' || role === 'SuperAdmin';
  const canView   = isAdmin || hasPerm('sales.credit-notes.view');
  const canCreate = isAdmin || hasPerm('sales.credit-notes.create');

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
      setSendError(e instanceof Error ? e.message : 'Error al enviar al SRI');
    } finally {
      setSendingId(null);
    }
  };

  if (!canView) return <NoAccessPage title={t('ventas.notas.title')} />;

  return (
    <div className="pg-page">

      {/* ── Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.sales')}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('ventas.notas.title')}</span>
          </nav>
          <h1 className="pg-title">{t('ventas.notas.title')}</h1>
          <p className="pg-subtitle">Notas de crédito y débito emitidas sobre facturas autorizadas.</p>
        </div>
        <div className="pg-header-right">
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            <span className="material-symbols-outlined">refresh</span>
            Actualizar
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
        </div>
      </div>

      {/* ── Alerts ── */}
      {error     && <ZHPageNotice variant="error" message="Error al cargar" detail={error} />}
      {sendError && <ZHPageNotice variant="error" message="Error al enviar" detail={sendError} />}

      {/* ── Table Card ── */}
      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">description</span>
            <span className="pg-section-label">Comprobantes emitidos</span>
          </div>
        </div>

        {/* Controls */}
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder="Buscar por clave de acceso..."
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'all' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('all')}
            >Todos</button>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'CREDITO' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('CREDITO')}
            >Crédito</button>
            <button
              className={`zh-btn zh-btn--ghost zh-btn--sm${typeFilter === 'DEBITO' ? ' is-active' : ''}`}
              type="button"
              onClick={() => setTypeFilter('DEBITO')}
            >Débito</button>
          </div>
          <div className="pg-table-controls-right">
            <span>Mostrando {filtered.length} de {rows.length}</span>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <div style={{ padding: 40 }}><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div style={{ padding: 40 }}><EmptyState message="No hay notas emitidas." /></div>
        ) : filtered.length === 0 ? (
          <div style={{ padding: 40 }}><EmptyState message="Sin resultados para los filtros aplicados." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Tipo</th>
                  <th>Clave de acceso</th>
                  <th>Estado</th>
                  <th className="cn-col-right">Total</th>
                  <th>Fecha</th>
                  <th>Acciones</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const isBorrador = row.status.toLowerCase() === 'borrador';
                  return (
                    <tr key={row.id}>
                      <td className="cn-col-type">{noteTypeLabel(row.noteType, t)}</td>
                      <td className="cn-col-key" title={row.accessKey}>{row.accessKey || '—'}</td>
                      <td>
                        <span className={getStatusBadge(row.status)}>{row.status}</span>
                      </td>
                      <td className="cn-col-total">${row.total.toFixed(2)}</td>
                      <td>{new Date(row.issueDate).toLocaleDateString('es')}</td>
                      <td className="cn-cell-actions">
                        {isBorrador && (
                          <button
                            className="zh-btn zh-btn--primary zh-btn--sm"
                            type="button"
                            disabled={sendingId === row.id}
                            onClick={() => void onSend(row.id)}
                          >
                            <span className="material-symbols-outlined">send</span>
                            {sendingId === row.id ? 'Enviando...' : t('ventas.notas.send')}
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
    </div>
  );
}
