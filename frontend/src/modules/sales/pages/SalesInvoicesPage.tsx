import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { useI18n } from '../../../i18n/i18n';
import { salesInvoicesService, type SalesInvoiceDto } from '../api/salesInvoicesService';
import { invoiceNumber } from ../api/salesInvoicesMapper';
import { openSalesInvoicePrint } from '../utils/openSalesInvoicePrint';
import './ventas-facturas-page.css';
import { usePermissionsUi } from '../../../access/usePermissionsUi';
import { formatDate } from '../../../lib/formatters/dateFormatters';

type BadgeInfo = { cls: string; label: string };

function getStatusBadge(estado: string): BadgeInfo {
  const e = estado.toLowerCase();
  const base = 'badge badge--md badge--upper';
  if (e === 'autorizado')                                                    return { cls: `${base} badge--green`,  label: estado };
  if (e === 'borrador' || e === 'validado' || e === 'procesando')            return { cls: `${base} badge--orange`, label: estado };
  if (e === 'anulado' || e === 'rechazado' || e === 'errorenvio')            return { cls: `${base} badge--red`,    label: estado };
  return                                                                            { cls: `${base} badge--gray`,   label: estado };
}

export function VentasFacturasPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const navigate = useNavigate();
  const canView  = canShow('sales.invoices.view');
  const canCreate = canShow('sales.invoices.create');

  const [rows,       setRows]       = useState<SalesInvoiceDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [pageNumber, setPageNumber] = useState(1);
  const pageSize = 20;
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState<string | null>(null);
  const [printError,  setPrintError]  = useState<string | null>(null);
  const [printingId,  setPrintingId]  = useState<string | null>(null);
  const [rideId,      setRideId]      = useState<string | null>(null);
  const [retryingId,  setRetryingId]  = useState<string | null>(null);
  const [retryMsg,    setRetryMsg]    = useState<string | null>(null);
  const [q,           setQ]           = useState('');
  const [statusFilter, setStatusFilter] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const pageResult = await salesInvoicesService.list({
        pageNumber,
        pageSize,
        status: statusFilter || undefined,
        search: q.trim() || undefined,
      });
      setRows(pageResult.items);
      setTotalCount(pageResult.totalCount);
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
      setRows([]);
      setTotalCount(0);
    } finally {
      setLoading(false);
    }
  }, [pageNumber, statusFilter, q, pageSize]);

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const applyFilters = () => {
    if (pageNumber === 1) void load();
    else setPageNumber(1);
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const filtered = rows;

  const stats = useMemo(() => {
    const total       = rows.length;
    const autorizadas = rows.filter((r) => r.status.toLowerCase() === 'autorizado').length;
    const pendientes  = rows.filter((r) =>
      !['autorizado', 'anulado', 'rechazado', 'errorenvio'].includes(r.status.toLowerCase()),
    ).length;
    const valorTotal  = rows.reduce((sum, r) => sum + r.total, 0);
    return { total, autorizadas, pendientes, valorTotal };
  }, [rows]);

  const onPrint = async (id: string) => {
    setPrintError(null);
    setPrintingId(id);
    const result = await openSalesInvoicePrint(id);
    setPrintingId(null);
    if (!result.ok) {
      setPrintError(
        result.reason === 'popup_blocked'
          ? t('ventas.facturas.printPopupBlocked')
          : (result.message ?? t('ventas.facturas.printFailed')),
      );
    }
  };

  const onDownloadRide = async (id: string, numero: string) => {
    setRideId(id);
    try {
      const blob = await salesInvoicesService.downloadRide(id);
      const url  = URL.createObjectURL(blob);
      const a    = document.createElement('a');
      a.href     = url;
      a.download = `RIDE-${numero}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setPrintError(e instanceof Error ? e.message : 'Error al descargar RIDE');
    } finally {
      setRideId(null);
    }
  };

  const onRetry = async (id: string) => {
    setRetryMsg(null);
    setRetryingId(id);
    try {
      await salesInvoicesService.retry(id);
      setRetryMsg('Reintento enviado. Actualizando lista...');
      await load();
    } catch (e) {
      setRetryMsg(e instanceof Error ? e.message : 'Error al reintentar');
    } finally {
      setRetryingId(null);
    }
  };

  if (!canView) return <NoAccessPage title={t('ventas.facturas.title')} />;

  return (
    <ErpPageTemplate
      kicker={t('app.nav.group.sales')}
      title={t('ventas.facturas.title')}
      subtitle={t('ventas.facturas.subtitle')}
      action={
        <>
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            <span className="material-symbols-outlined">refresh</span>
            {t('ventas.facturas.refresh')}
          </button>
          {canCreate && (
            <button
              className="zh-btn zh-btn--primary"
              type="button"
              onClick={() => navigate('/sales/invoices/new')}
            >
              <span className="material-symbols-outlined">add</span>
              {t('ventas.facturas.newInvoice')}
            </button>
          )}
        </>
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      {printError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={printError} /> : null}
      {retryMsg ? <ZHPageNotice variant="success" message={retryMsg} /> : null}

      {/* ── KPI Cards ── */}
      {!loading && (
        <div className="pg-kpis">
          {/* Total facturas */}
          <div className="pg-kpi">
            <div className="pg-kpi-top">
              <div className="pg-kpi-icon pg-kpi-icon--primary">
                <span className="material-symbols-outlined">receipt_long</span>
              </div>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Facturas</p>
              <p className="pg-kpi-value">
                {stats.total}
                <span className="pg-kpi-unit">docs</span>
              </p>
            </div>
          </div>

          {/* Autorizadas */}
          <div className="pg-kpi">
            <div className="pg-kpi-top">
              <div className="pg-kpi-icon pg-kpi-icon--success">
                <span className="material-symbols-outlined">task_alt</span>
              </div>
              <span className="pg-kpi-badge pg-kpi-badge--success">
                {stats.total ? Math.round((stats.autorizadas / stats.total) * 100) : 0}%
              </span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Autorizadas</p>
              <p className="pg-kpi-value">
                {stats.autorizadas}
                <span className="pg-kpi-unit">facturas</span>
              </p>
            </div>
          </div>

          {/* Pendientes */}
          <div className="pg-kpi">
            <div className="pg-kpi-top">
              <div className="pg-kpi-icon pg-kpi-icon--warning">
                <span className="material-symbols-outlined">pending</span>
              </div>
              {stats.pendientes > 0 && (
                <span className="pg-kpi-badge pg-kpi-badge--warning">Alerta</span>
              )}
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Pendientes</p>
              <p className="pg-kpi-value">
                {stats.pendientes}
                <span className="pg-kpi-unit">facturas</span>
              </p>
            </div>
          </div>

          {/* Valor total */}
          <div className="pg-kpi">
            <div className="pg-kpi-top">
              <div className="pg-kpi-icon pg-kpi-icon--secondary">
                <span className="material-symbols-outlined">payments</span>
              </div>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Valor Total</p>
              <p className="pg-kpi-value">
                ${stats.valorTotal.toLocaleString('es', { minimumFractionDigits: 2 })}
                <span className="pg-kpi-unit">USD</span>
              </p>
            </div>
          </div>
        </div>
      )}

      {/* ── Table Card ── */}
      <div className="pg-section">

        {/* Section Header */}
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">receipt</span>
            <span className="pg-section-label">Comprobantes Emitidos</span>
          </div>
          <div className="pg-section-actions">
            <button className="zh-btn zh-btn--ghost zh-btn--sm" type="button">
              <span className="material-symbols-outlined">download</span>
              Exportar
            </button>
          </div>
        </div>

        {/* Table Controls */}
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                type="text"
                placeholder={t('ventas.facturas.searchPlaceholder')}
                value={q}
                onChange={(e) => setQ(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') applyFilters();
                }}
                disabled={loading}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="">{t('ventas.facturas.filter.allStatuses')}</option>
              <option value="Borrador">{t('ventas.facturas.status.borrador')}</option>
              <option value="Validado">{t('ventas.facturas.status.validado')}</option>
              <option value="Autorizado">{t('ventas.facturas.status.autorizado')}</option>
              <option value="Rechazado">{t('ventas.facturas.status.rechazado')}</option>
              <option value="ErrorEnvio">{t('ventas.facturas.status.errorenvio')}</option>
              <option value="Anulado">{t('ventas.facturas.status.anulado')}</option>
            </select>
            <button className="zh-btn zh-btn--ghost zh-btn--md" type="button" onClick={applyFilters}>
              <span className="material-symbols-outlined">filter_list</span>
              {t('ventas.facturas.applyFilters')}
            </button>
          </div>
          <div className="pg-table-controls-right">
            <span>{t('ventas.facturas.records', { count: totalCount })}</span>
            {totalCount > pageSize && (
              <div className="pg-pagination-controls">
                <button
                  className="pg-pagination-btn"
                  type="button"
                  disabled={pageNumber <= 1 || loading}
                  onClick={() => setPageNumber((p) => Math.max(1, p - 1))}
                >
                  <span className="material-symbols-outlined">chevron_left</span>
                </button>
                <span>{t('ventas.facturas.page', { page: pageNumber, total: totalPages })}</span>
                <button
                  className="pg-pagination-btn"
                  type="button"
                  disabled={pageNumber >= totalPages || loading}
                  onClick={() => setPageNumber((p) => p + 1)}
                >
                  <span className="material-symbols-outlined">chevron_right</span>
                </button>
              </div>
            )}
          </div>
        </div>

        {/* Table Body */}
        {loading ? (
          <div className="pg-pad-40"><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message={t('common.noData')} /></div>
        ) : filtered.length === 0 ? (
          <div className="pg-pad-40"><EmptyState message={t('common.listTab.noMatch')} /></div>
        ) : (
          <div className="pg-overflow-x">
            <table className="table responsive-table vf-responsive-table">
              <thead>
                <tr>
                  <th>Nº Factura</th>
                  <th>{t('ventas.facturas.col.customer')}</th>
                  <th>{t('ventas.facturas.col.date')}</th>
                  <th>Subtotal</th>
                  <th>Impuesto</th>
                  <th className="vf-col-right">{t('ventas.facturas.col.total')}</th>
                  <th>{t('ventas.facturas.col.status')}</th>
                  <th>{t('ventas.facturas.col.actions')}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((row) => {
                  const numero      = invoiceNumber(row);
                  const estado      = row.status.toLowerCase();
                  const isAuth      = estado === 'autorizado';
                  const isError     = estado === 'errorenvio' || estado === 'rechazado';
                  const badge       = getStatusBadge(row.status);
                  return (
                    <tr
                      key={row.id}
                      className="pg-row-clickable"
                      onClick={() => navigate(`/sales/invoices/${row.id}`)}
                    >
                      <td data-label="Nº Factura" className="vf-col-numero">{numero}</td>
                      <td data-label={t('ventas.facturas.col.customer')}>{row.clienteNombre}</td>
                      <td data-label={t('ventas.facturas.col.date')} className="vf-col-date">
                        {formatDate(row.issueDate)}
                      </td>
                      <td data-label="Subtotal">${row.subtotal.toFixed(2)}</td>
                      <td data-label="Impuesto">${row.impuesto.toFixed(2)}</td>
                      <td data-label={t('ventas.facturas.col.total')} className="vf-col-total vf-cell-right">
                        ${row.total.toFixed(2)}
                      </td>
                      <td data-label={t('ventas.facturas.col.status')}>
                        <span className={badge.cls}>{badge.label}</span>
                        {isError && row.mensajeError && (
                          <p className="vf-error-hint" title={row.mensajeError}>
                            {row.mensajeError.length > 60
                              ? row.mensajeError.slice(0, 60) + '…'
                              : row.mensajeError}
                          </p>
                        )}
                      </td>
                      <td data-label={t('ventas.facturas.col.actions')} className="vf-cell-actions">
                        <button
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            navigate(`/sales/invoices/${row.id}`);
                          }}
                        >
                          {t('common.view')}
                        </button>
                        {isAuth && (
                          <>
                            <button
                              className="zh-btn zh-btn--ghost zh-btn--sm"
                              type="button"
                              disabled={rideId === row.id}
                              onClick={() => void onDownloadRide(row.id, numero)}
                              title="Descargar RIDE (PDF oficial)"
                            >
                              <span className="material-symbols-outlined">picture_as_pdf</span>
                              {rideId === row.id ? '...' : 'RIDE'}
                            </button>
                            <button
                              className="zh-btn zh-btn--primary zh-btn--sm"
                              type="button"
                              disabled={printingId === row.id}
                              onClick={() => void onPrint(row.id)}
                            >
                              {printingId === row.id ? t('common.loading') : t('ventas.facturas.print')}
                            </button>
                          </>
                        )}
                        {isError && (
                          <button
                            className="zh-btn zh-btn--ghost zh-btn--sm"
                            type="button"
                            disabled={retryingId === row.id}
                            onClick={() => void onRetry(row.id)}
                            title="Reintentar envío al SRI"
                          >
                            <span className="material-symbols-outlined">refresh</span>
                            {retryingId === row.id ? 'Enviando...' : 'Reintentar'}
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

        {/* Table Footer */}
        <div className="pg-table-footer">
          <div className="pg-legend">
            <div className="pg-legend-item">
              <span className="pg-legend-dot pg-legend-dot--success" />
              <span>Autorizado</span>
            </div>
            <div className="pg-legend-item">
              <span className="pg-legend-dot pg-legend-dot--warning" />
              <span>Pendiente / Procesando</span>
            </div>
            <div className="pg-legend-item">
              <span className="pg-legend-dot pg-legend-dot--error" />
              <span>Anulado / Error</span>
            </div>
          </div>
          {rows.length > 0 && (
            <span className="pg-table-timestamp">
              Última carga: {new Date().toLocaleTimeString('es')}
            </span>
          )}
        </div>
      </div>

      {/* ── Support Grid: Recent activity + Accent card ── */}
      <div className="vf-support-grid">

        {/* Recent activity */}
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">history</span>
              <span className="pg-section-label">Actividad Reciente</span>
            </div>
          </div>
          <div className="pg-section-body">
            {rows.length === 0 ? (
              <EmptyState message="Sin actividad reciente" />
            ) : (
              <div className="pg-activity-list">
                {rows.slice(0, 4).map((row) => {
                  const numero  = `${row.establecimiento}-${row.puntoEmision}-${row.secuencial}`;
                  const isAuth  = row.status.toLowerCase() === 'autorizado';
                  return (
                    <div key={row.id} className="pg-activity-item">
                      <div className="pg-activity-left">
                        <div className={`pg-activity-icon ${isAuth ? 'pg-activity-icon--in' : 'pg-activity-icon--neutral'}`}>
                          <span className="material-symbols-outlined">
                            {isAuth ? 'check_circle' : 'schedule'}
                          </span>
                        </div>
                        <div>
                          <p className="pg-activity-title">{row.clienteNombre}</p>
                          <p className="pg-activity-subtitle">Factura {numero}</p>
                        </div>
                      </div>
                      <div className="pg-activity-right">
                        <span className={`pg-activity-amount ${isAuth ? 'pg-activity-amount--in' : ''}`}>
                          ${row.total.toFixed(2)}
                        </span>
                        <span className="pg-activity-time">
                          {formatDate(row.issueDate)}
                        </span>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </div>

        {/* Accent card */}
        <div className="pg-accent-card">
          <div className="pg-accent-card-inner">
            <h3 className="pg-accent-card-title">Emisión Electrónica</h3>
            <p className="pg-accent-card-body">
              Los comprobantes autorizados se envían automáticamente al sistema tributario. Verifica el estado de cada factura en la tabla.
            </p>
            <button className="pg-accent-card-btn" type="button">
              <span className="material-symbols-outlined">open_in_new</span>
              Ver Guía SRI
            </button>
          </div>
          <span className="material-symbols-outlined pg-accent-card-deco">receipt_long</span>
        </div>

      </div>

    </ErpPageTemplate>
  );
}
