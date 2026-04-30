import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import './DashboardPage.css';

export function DashboardPage() {
  const user = useAuthStore((s) => s.user);
  const { t } = useI18n();

  const stats = [
    { label: t('dashboard.stats.products'), value: '—', icon: '📦' },
    { label: t('dashboard.stats.journalEntries'), value: '—', icon: '📒' },
    { label: t('dashboard.stats.accounts'), value: '—', icon: '🏦' },
    { label: t('dashboard.stats.users'), value: '—', icon: '👤' },
  ];

  return (
    <div className="dashboard">
      <div className="page-header">
        <h1 className="page-title">{t('dashboard.title')}</h1>
        <p className="page-subtitle">{t('dashboard.welcome')} {user?.fullName ?? ''}</p>
      </div>

      <div className="stats-grid">
        {stats.map((s) => (
          <div key={s.label} className="stat-card">
            <span className="stat-icon">{s.icon}</span>
            <div className="stat-body">
              <span className="stat-value">{s.value}</span>
              <span className="stat-label">{s.label}</span>
            </div>
          </div>
        ))}
      </div>

      <div className="info-card">
        <h2>{t('dashboard.session.title')}</h2>
        <dl className="info-grid">
          <dt>{t('dashboard.session.user')}</dt>
          <dd>{user?.fullName}</dd>
          <dt>{t('dashboard.session.email')}</dt>
          <dd>{user?.email}</dd>
          <dt>{t('dashboard.session.role')}</dt>
          <dd>{user?.role}</dd>
          <dt>{t('dashboard.session.tenantId')}</dt>
          <dd className="mono">{user?.tenantId}</dd>
        </dl>
      </div>
    </div>
  );
}
