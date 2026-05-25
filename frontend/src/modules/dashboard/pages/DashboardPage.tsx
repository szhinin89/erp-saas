import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer,
} from 'recharts';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { RuntimeModeBadge } from '../../../components/RuntimeModeBadge';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { ErpPageTemplate } from '../../../templates/ErpPageTemplate';
import { useDashboardKpis, useArAging, useApAging } from '../hooks/useDashboardData';
import './DashboardPage.css';

function fmt(n: number | undefined, decimals = 2) {
  if (n === undefined || n === null) return '—';
  return `$${n.toLocaleString('es-EC', { minimumFractionDigits: decimals, maximumFractionDigits: decimals })}`;
}

function fmtN(n: number | undefined) {
  if (n === undefined || n === null) return '—';
  return n.toLocaleString('es-EC');
}

function agingChartData(data: { totalCurrent: number; total1To30: number; total31To60: number; total61To90: number; totalOver90: number } | null) {
  if (!data) return [];
  return [
    { bucket: 'Vigente',  value: data.totalCurrent },
    { bucket: '1–30',    value: data.total1To30 },
    { bucket: '31–60',   value: data.total31To60 },
    { bucket: '61–90',   value: data.total61To90 },
    { bucket: '>90',     value: data.totalOver90 },
  ];
}

export function DashboardPage() {
  const { t, locale } = useI18n();
  const user = useAuthStore((s) => s.user);
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);
  const navigate = useNavigate();

  const kpis   = useDashboardKpis();
  const arAging = useArAging();
  const apAging = useApAging();

  useEffect(() => {
    if (!user?.companyId) {
      navigate('/saas/overview', { replace: true });
    }
  }, [navigate, user?.companyId]);

  if (!user?.companyId) return null;

  const today = new Date().toLocaleDateString(locale === 'en' ? 'en-US' : 'es-ES', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });

  const d = kpis.data;
  const loading = kpis.loading || arAging.loading || apAging.loading;
  const periodLabel = d ? `${d.month}/${d.year}` : '';

  return (
    <ErpPageTemplate
      key={`dashboard-${companySessionVersion}`}
      title={`${t('dashboard.welcome')} ${user?.fullName ?? ''}`.trim()}
      subtitle={today}
      kicker={t('dashboard.title')}
      action={<RuntimeModeBadge />}
      pageClassName="dsh-page"
    >
      {kpis.error && <ZHPageNotice variant="error" message="Error al cargar KPIs" detail={kpis.error} />}

      {/* ── KPI cards ── */}
      <div className="pg-kpis">
        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">payments</span>
            </div>
            {periodLabel && <span className="badge badge--blue">{periodLabel}</span>}
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">Ventas MTD</p>
            <p className="pg-kpi-value">{loading ? '…' : fmt(d?.salesMtd)}</p>
            <p className="subtle">{loading ? '' : `${fmtN(d?.invoicesMtd)} facturas`}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--warning">
              <span className="material-symbols-outlined">account_balance</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">CxC Pendiente</p>
            <p className="pg-kpi-value">{loading ? '…' : fmt(d?.pendingArTotal)}</p>
            {!loading && !!d?.overdueArTotal && (
              <div className="dsh-kpi-trend dsh-kpi-trend--warning">
                <span className="material-symbols-outlined">warning</span>
                <span>{fmt(d.overdueArTotal)} vencido</span>
              </div>
            )}
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">inventory_2</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('dashboard.kpis.lowStock')}</p>
            <p className="pg-kpi-value">{loading ? '…' : fmtN(d?.lowStockSkuCount)}</p>
            <p className="subtle">{loading ? '' : `${fmtN(d?.outOfStockSkuCount)} sin stock`}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">shopping_cart</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">CxP Pendiente</p>
            <p className="pg-kpi-value">{loading ? '…' : fmt(d?.pendingApTotal)}</p>
            {!loading && !!d?.overdueApTotal && (
              <div className="dsh-kpi-trend dsh-kpi-trend--warning">
                <span className="material-symbols-outlined">warning</span>
                <span>{fmt(d.overdueApTotal)} vencido</span>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* ── Aging charts + quick access ── */}
      <div className="dsh-body">
        <div className="dsh-charts-col">
          {/* AR Aging */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">Antigüedad CxC (AR)</h2>
              {arAging.data && (
                <span className="badge badge--blue">Total: {fmt(arAging.data.grandTotal)}</span>
              )}
            </div>
            <div className="dsh-chart-wrap">
              {arAging.loading ? (
                <div className="dsh-chart-placeholder">Cargando…</div>
              ) : arAging.error ? (
                <div className="dsh-chart-placeholder dsh-chart-placeholder--error">{arAging.error}</div>
              ) : (
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={agingChartData(arAging.data)} margin={{ top: 8, right: 16, left: 8, bottom: 4 }}>
                    <XAxis dataKey="bucket" tick={{ fontSize: 12 }} />
                    <YAxis tickFormatter={(v) => `$${(v / 1000).toFixed(0)}k`} tick={{ fontSize: 11 }} width={48} />
                    <Tooltip formatter={(v: number) => fmt(v)} />
                    <Bar dataKey="value" name="Monto" fill="var(--color-primary)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </div>
          </div>

          {/* AP Aging */}
          <div className="card card--xl">
            <div className="dsh-activity-head">
              <h2 className="dsh-section-title">Antigüedad CxP (AP)</h2>
              {apAging.data && (
                <span className="badge badge--blue">Total: {fmt(apAging.data.grandTotal)}</span>
              )}
            </div>
            <div className="dsh-chart-wrap">
              {apAging.loading ? (
                <div className="dsh-chart-placeholder">Cargando…</div>
              ) : apAging.error ? (
                <div className="dsh-chart-placeholder dsh-chart-placeholder--error">{apAging.error}</div>
              ) : (
                <ResponsiveContainer width="100%" height={200}>
                  <BarChart data={agingChartData(apAging.data)} margin={{ top: 8, right: 16, left: 8, bottom: 4 }}>
                    <XAxis dataKey="bucket" tick={{ fontSize: 12 }} />
                    <YAxis tickFormatter={(v) => `$${(v / 1000).toFixed(0)}k`} tick={{ fontSize: 11 }} width={48} />
                    <Tooltip formatter={(v: number) => fmt(v)} />
                    <Bar dataKey="value" name="Monto" fill="var(--color-warning)" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              )}
            </div>
          </div>
        </div>

        <div className="dsh-right">
          <div className="card card--xl">
            <div className="card-header">
              <h2 className="dsh-section-title">{t('dashboard.quickAccess.title')}</h2>
            </div>
            <div className="dsh-card-body">
              <button type="button" className="dsh-quick-btn" onClick={() => navigate('/sales/invoices/new')}>
                <span className="material-symbols-outlined dsh-quick-icon">add_shopping_cart</span>
                <div>
                  <p className="dsh-quick-label">{t('dashboard.quickAccess.newInvoice')}</p>
                  <p className="dsh-quick-sub">{t('dashboard.quickAccess.newInvoice.sub')}</p>
                </div>
              </button>
              <button type="button" className="dsh-quick-btn" onClick={() => navigate('/sales/quotes/new')}>
                <span className="material-symbols-outlined dsh-quick-icon">request_quote</span>
                <div>
                  <p className="dsh-quick-label">Nueva Cotización</p>
                  <p className="dsh-quick-sub">Iniciar pipeline comercial</p>
                </div>
              </button>
              <button type="button" className="dsh-quick-btn" onClick={() => navigate('/products')}>
                <span className="material-symbols-outlined dsh-quick-icon">inventory</span>
                <div>
                  <p className="dsh-quick-label">{t('dashboard.quickAccess.addItem')}</p>
                  <p className="dsh-quick-sub">{t('dashboard.quickAccess.addItem.sub')}</p>
                </div>
              </button>
              <button type="button" className="dsh-quick-btn" onClick={() => navigate('/accounting')}>
                <span className="material-symbols-outlined dsh-quick-icon">analytics</span>
                <div>
                  <p className="dsh-quick-label">{t('dashboard.quickAccess.viewReports')}</p>
                  <p className="dsh-quick-sub">{t('dashboard.quickAccess.viewReports.sub')}</p>
                </div>
              </button>
            </div>
          </div>

          {/* YTD summary */}
          <div className="card card--xl">
            <div className="card-header">
              <h2 className="dsh-section-title">Resumen Anual</h2>
              {d && <span className="badge badge--blue">{d.year}</span>}
            </div>
            <div className="dsh-card-body">
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">Ventas YTD</span>
                <span className="dsh-summary-value">{loading ? '…' : fmt(d?.salesYtd)}</span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">Facturas MTD</span>
                <span className="dsh-summary-value">{loading ? '…' : fmtN(d?.invoicesMtd)}</span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">CxC vencida</span>
                <span className="dsh-summary-value dsh-summary-value--warn">{loading ? '…' : fmt(d?.overdueArTotal)}</span>
              </div>
              <div className="dsh-summary-row">
                <span className="dsh-summary-label">CxP vencida</span>
                <span className="dsh-summary-value dsh-summary-value--warn">{loading ? '…' : fmt(d?.overdueApTotal)}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <footer className="dsh-footer">
        <div className="dsh-footer-left">
          <span className="dsh-footer-brand">ZH Technologies</span>
          <span className="dsh-footer-copy">{t('dashboard.footer.rights')}</span>
        </div>
        <nav className="dsh-footer-links">
          <a href="#">{t('dashboard.footer.support')}</a>
          <a href="#">{t('dashboard.footer.docs')}</a>
          <a href="#">{t('dashboard.footer.privacy')}</a>
        </nav>
      </footer>
    </ErpPageTemplate>
  );
}
