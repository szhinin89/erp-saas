import { useNavigate } from 'react-router-dom';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import './DashboardPage.css';

type ActivityStatus = 'completed' | 'pending' | 'cancelled';

const DEMO_ACTIVITY = [
  { id: '#FAC-2934', client: 'Distribuidora Central S.A.', amount: '$1,250.00', status: 'completed' as ActivityStatus },
  { id: '#FAC-2935', client: 'Logística Express S.L.',    amount: '$840.50',   status: 'pending'   as ActivityStatus },
  { id: '#FAC-2936', client: 'Andrés Mendoza',             amount: '$2,100.00', status: 'cancelled' as ActivityStatus },
  { id: '#FAC-2937', client: 'TecnoMundo C.A.',            amount: '$3,450.00', status: 'completed' as ActivityStatus },
  { id: '#FAC-2938', client: 'Inversiones Delta',          amount: '$560.00',   status: 'completed' as ActivityStatus },
];

const DEMO_NOTIFICATIONS = [
  { dot: 'error',   title: 'Actualización de Sistema',   body: 'Reinicio programado hoy a las 23:00h.',                   time: 'Hace 15 min' },
  { dot: 'success', title: 'Pago Recibido',              body: 'Cliente "Inversiones Delta" liquidó factura #2931.',      time: 'Hace 2 horas' },
  { dot: 'warning', title: 'Stock Mínimo',               body: '"Monitor UltraWide 34\'" alcanzó stock crítico.',         time: 'Hace 5 horas' },
];

function statusBadge(s: ActivityStatus) {
  if (s === 'completed') return 'badge badge--green';
  if (s === 'pending')   return 'badge badge--gray';
  return 'badge badge--red';
}

export function DashboardPage() {
  const { t, locale } = useI18n();
  const user = useAuthStore((s) => s.user);
  const navigate = useNavigate();

  const today = new Date().toLocaleDateString(locale === 'en' ? 'en-US' : 'es-ES', {
    weekday: 'long', year: 'numeric', month: 'long', day: 'numeric',
  });

  const statusLabel = (s: ActivityStatus) => {
    if (s === 'completed') return t('dashboard.activity.status.completed');
    if (s === 'pending')   return t('dashboard.activity.status.pending');
    return t('dashboard.activity.status.cancelled');
  };

  return (
    <div className="dsh-page">

      {/* ── Welcome ── */}
      <div className="dsh-welcome">
        <h1 className="dsh-welcome-title">{t('dashboard.welcome')} {user?.fullName ?? ''}</h1>
        <p className="dsh-welcome-date">{today}</p>
      </div>

      {/* ── KPI Cards — usa pg-kpi* global ── */}
      <div className="pg-kpis">

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">payments</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('dashboard.kpis.salesDay')}</p>
            <p className="pg-kpi-value">—</p>
            <div className="dsh-kpi-trend dsh-kpi-trend--success">
              <span className="material-symbols-outlined">trending_up</span>
              <span>{t('dashboard.kpis.salesDayTrend')}</span>
            </div>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--warning">
              <span className="material-symbols-outlined">pending_actions</span>
            </div>
            <span className="badge badge--red">{t('dashboard.kpis.pendingOrders.urgent')}</span>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('dashboard.kpis.pendingOrders')}</p>
            <p className="pg-kpi-value">—</p>
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
            <p className="pg-kpi-value">—</p>
            <p className="subtle">{t('dashboard.kpis.lowStock.reorder')}</p>
          </div>
        </div>

        <div className="pg-kpi">
          <div className="pg-kpi-top">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">account_balance_wallet</span>
            </div>
          </div>
          <div className="pg-kpi-bottom">
            <p className="pg-kpi-label">{t('dashboard.kpis.monthlyRevenue')}</p>
            <p className="pg-kpi-value">—</p>
            <div className="dsh-kpi-progress">
              <div className="dsh-kpi-progress-bar" style={{ width: '75%' }} />
            </div>
            <p className="dsh-kpi-progress-label">75% {t('dashboard.kpis.monthlyRevenue.goal')}</p>
          </div>
        </div>

      </div>

      {/* ── Body grid ── */}
      <div className="dsh-body">

        {/* Activity table — usa .table global */}
        <div className="dsh-activity-card">
          <div className="dsh-activity-head">
            <h2 className="dsh-section-title">{t('dashboard.activity.title')}</h2>
            <button type="button" className="dsh-link-btn" onClick={() => navigate('/ventas/facturas')}>
              {t('dashboard.activity.viewAll')}
            </button>
          </div>
          <div className="dsh-activity-table-wrap">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('dashboard.activity.col.id')}</th>
                  <th>{t('dashboard.activity.col.client')}</th>
                  <th>{t('dashboard.activity.col.amount')}</th>
                  <th>{t('dashboard.activity.col.status')}</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {DEMO_ACTIVITY.map((row) => (
                  <tr key={row.id} style={{ cursor: 'pointer' }} onClick={() => navigate('/ventas/facturas')}>
                    <td className="dsh-cell-id">{row.id}</td>
                    <td>{row.client}</td>
                    <td>{row.amount}</td>
                    <td><span className={statusBadge(row.status)}>{statusLabel(row.status)}</span></td>
                    <td className="dsh-cell-chevron">
                      <span className="material-symbols-outlined">chevron_right</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Right column */}
        <div className="dsh-right">

          {/* Quick access */}
          <div className="dsh-card">
            <div className="dsh-card-head">
              <h2 className="dsh-section-title">{t('dashboard.quickAccess.title')}</h2>
            </div>
            <div className="dsh-card-body">
              <button type="button" className="dsh-quick-btn" onClick={() => navigate('/ventas/facturas')}>
                <span className="material-symbols-outlined dsh-quick-icon">add_shopping_cart</span>
                <div>
                  <p className="dsh-quick-label">{t('dashboard.quickAccess.newInvoice')}</p>
                  <p className="dsh-quick-sub">{t('dashboard.quickAccess.newInvoice.sub')}</p>
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

          {/* Notifications */}
          <div className="dsh-card">
            <div className="dsh-card-head">
              <h2 className="dsh-section-title">{t('dashboard.notifications.title')}</h2>
              <span className="badge badge--blue">{DEMO_NOTIFICATIONS.length}</span>
            </div>
            <div className="dsh-card-body dsh-notif-list">
              {DEMO_NOTIFICATIONS.map((n, i) => (
                <div key={i} className="dsh-notif">
                  <span className={`dsh-notif-dot dsh-notif-dot--${n.dot}`} />
                  <div>
                    <p className="dsh-notif-title">{n.title}</p>
                    <p className="dsh-notif-body">{n.body}</p>
                    <p className="dsh-notif-time">{n.time}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>

        </div>
      </div>

      {/* ── Footer ── */}
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

    </div>
  );
}
