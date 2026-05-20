import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { EmptyState, LoadingState, NoAccessPage } from '../../../components/PageShell';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { useI18n } from '../../../i18n/i18n';
import { usePermissionsStore } from '../../../store/permissionsStore';
import { ventasFacturasService, type VentasFacturaDto } from '../api/ventasFacturasService';
import { openVentasFacturaPrint } from '../utils/openVentasFacturaPrint';
import './ventas-facturas-page.css';

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
  const { t } = useI18n();
  const navigate = useNavigate();
  const hasPerm  = usePermissionsStore((s) => s.has);
  const canView  = hasPerm('sales.invoices.view');

  const [rows,       setRows]       = useState<VentasFacturaDto[]>([]);
  const [loading,    setLoading]    = useState(true);
  const [error,      setError]      = useState<string | null>(null);
  const [printError,  setPrintError]  = useState<string | null>(null);
  const [printingId,  setPrintingId]  = useState<string | null>(null);
  const [rideId,      setRideId]      = useState<string | null>(null);
  const [retryingId,  setRetryingId]  = useState<string | null>(null);
  const [retryMsg,    setRetryMsg]    = useState<string | null>(null);
  const [q,           setQ]           = useState('');

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

  useEffect(() => { if (canView) void load(); }, [canView, load]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return rows;
    return rows.filter((r) =>
      r.clienteNombre.toLowerCase().includes(query) ||
      `${r.establecimiento}-${r.puntoEmision}-${r.secuencial}`.includes(query) ||
      r.estado.toLowerCase().includes(query),
    );
  }, [rows, q]);

  const stats = useMemo(() => {
    const total       = rows.length;
    const autorizadas = rows.filter((r) => r.estado.toLowerCase() === 'autorizado').length;
    const pendientes  = rows.filter((r) =>
      !['autorizado', 'anulado', 'rechazado', 'errorenvio'].includes(r.estado.toLowerCase()),
    ).length;
    const valorTotal  = rows.reduce((sum, r) => sum + r.total, 0);
    return { total, autorizadas, pendientes, valorTotal };
  }, [rows]);

  const onPrint = async (id: string) => {
    setPrintError(null);
    setPrintingId(id);
    const result = await openVentasFacturaPrint(id);
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
      const blob = await ventasFacturasService.downloadRide(id);
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
      await ventasFacturasService.retry(id);
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
    <div className="pg-page">

      {/* ── Page Header ── */}
      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Navegación">
            <span className="pg-breadcrumb-item">{t('app.nav.group.sales')}</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">{t('ventas.facturas.title')}</span>
          </nav>
          <h1 className="pg-title">{t('ventas.facturas.title')}</h1>
          <p className="pg-subtitle">Registro y control de comprobantes de venta electrónicos.</p>
        </div>
        <div className="pg-header-right">
          <button
            className="zh-btn zh-btn--secondary"
            type="button"
            disabled={loading}
            onClick={() => void load()}
          >
            <span className="material-symbols-outlined">refresh</span>
            {t('ventas.facturas.refresh')}
          </button>
          <button
            className="zh-btn zh-btn--primary"
            type="button"
            onClick={() => navigate('/ventas/facturas/nueva')}
          >
            <span className="material-symbols-outlined">add</span>
            Nueva Factura
          </button>
        </div>
      </div>

      {/* ── Alerts ── */}
      {error      && <ZHPageNotice variant="error"   message={t('common.errorPrefix')} detail={error} />}
      {printError && <ZHPageNotice variant="error"   message={t('common.errorPrefix')} detail={printError} />}
      {retryMsg   && <ZHPageNotice variant="success" message={retryMsg} />}

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
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
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
                placeholder="Filtrar por número, cliente o estado..."
                value={q}
                onChange={(e) => setQ(e.target.value)}
                disabled={loading}
              />
            </div>
            <button className="zh-btn zh-btn--ghost zh-btn--md" type="button">
              <span className="material-symbols-outlined">filter_list</span>
              Filtros
            </button>
          </div>
          <div className="pg-table-controls-right">
            <span>
              Mostrando {filtered.length} de {rows.length}
            </span>
            <div className="pg-pagination-controls">
              <button className="pg-pagination-btn" type="button" disabled>
                <span className="material-symbols-outlined">chevron_left</span>
              </button>
              <button className="pg-pagination-btn" type="button">
                <span className="material-symbols-outlined">chevron_right</span>
              </button>
            </div>
          </div>
        </div>

        {/* Table Body */}
        {loading ? (
          <div style={{ padding: '40px' }}><LoadingState /></div>
        ) : rows.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message={t('common.noData')} /></div>
        ) : filtered.length === 0 ? (
          <div style={{ padding: '40px' }}><EmptyState message="No se encontraron resultados para la búsqueda." /></div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
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
                  const numero      = `${row.establecimiento}-${row.puntoEmision}-${row.secuencial}`;
                  const estado      = row.estado.toLowerCase();
                  const isAuth      = estado === 'autorizado';
                  const isError     = estado === 'errorenvio' || estado === 'rechazado';
                  const badge       = getStatusBadge(row.estado);
                  return (
                    <tr key={row.id}>
                      <td data-label="Nº Factura" className="vf-col-numero">{numero}</td>
                      <td data-label={t('ventas.facturas.col.customer')}>{row.clienteNombre}</td>
                      <td data-label={t('ventas.facturas.col.date')} className="vf-col-date">
                        {new Date(row.fechaEmision).toLocaleDateString('es')}
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
                  const isAuth  = row.estado.toLowerCase() === 'autorizado';
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
                          {new Date(row.fechaEmision).toLocaleDateString('es')}
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
          <div style={{ position: 'relative', zIndex: 1 }}>
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

    </div>
  );
}
