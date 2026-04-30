import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { PageShell } from '../components/PageShell';
import { superAdminService, type SuperAdminTenant } from '../services/superAdminService';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import './SuperAdminPanelPage.css';

function storeImpersonationTenantName(name: string) {
  localStorage.setItem('superadmin-impersonation-tenant-name', name);
}

export function SuperAdminPanelPage() {
  const navigate = useNavigate();
  const { user, login } = useAuthStore();
  const { t } = useI18n();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof superAdminService.getMetrics>> | null>(null);
  const [tenants, setTenants] = useState<SuperAdminTenant[]>([]);
  const [q, setQ] = useState('');
  const [switching, setSwitching] = useState<string | null>(null);

  const isSuperAdmin = user?.role === 'SuperAdmin';
  const hasSelectedTenant = !!user?.tenantId && user.tenantId !== '00000000-0000-0000-0000-000000000000';

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        const [m, tns] = await Promise.all([
          superAdminService.getMetrics(),
          superAdminService.getTenants(),
        ]);
        if (cancelled) return;
        setMetrics(m);
        setTenants(tns);
      } catch (e) {
        if (cancelled) return;
        setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric'));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [t]);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return tenants;
    return tenants.filter((t) =>
      t.name.toLowerCase().includes(query) || t.slug.toLowerCase().includes(query) || t.id.toLowerCase().includes(query)
    );
  }, [tenants, q]);

  const handleSwitch = async (tenant: SuperAdminTenant) => {
    setSwitching(tenant.id);
    setError('');
    try {
      const auth = await superAdminService.switchTenant(tenant.id);
      storeImpersonationTenantName(tenant.name);
      login(auth);
      navigate('/dashboard');
    } catch (e) {
      setError((e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric'));
    } finally {
      setSwitching(null);
    }
  };

  if (!isSuperAdmin) {
    return (
      <PageShell title={t('superadmin.title')}>
        <div className="card">
          <p style={{ margin: 0 }}>{t('superadmin.noAccess')}</p>
        </div>
      </PageShell>
    );
  }

  if (hasSelectedTenant) {
    return (
      <PageShell
        title={t('superadmin.title')}
        action={<button onClick={() => navigate('/dashboard')}>{t('superadmin.goToTenant')}</button>}
      >
        <div className="card">
          <p style={{ margin: 0 }}>{t('superadmin.alreadyInTenant')}</p>
        </div>
      </PageShell>
    );
  }

  return (
    <PageShell title={t('superadmin.title')} action={<span className="subtle">{t('superadmin.subtitle')}</span>}>
      {error ? <div className="error-state">{error}</div> : null}

      <div className="sa-grid">
        <div className="sa-metrics card">
          <h2 className="sa-sectionTitle">{t('superadmin.metrics')}</h2>
          {loading || !metrics ? (
            <div className="empty-state">{t('common.loading')}</div>
          ) : (
            <div className="sa-metricCards">
              <div className="sa-metric">
                <div className="sa-metricLabel">{t('superadmin.totalTenants')}</div>
                <div className="sa-metricValue">{metrics.totals.totalTenants}</div>
              </div>
              <div className="sa-metric">
                <div className="sa-metricLabel">{t('superadmin.activeTenants')}</div>
                <div className="sa-metricValue">{metrics.totals.activeTenants}</div>
              </div>
              <div className="sa-metric">
                <div className="sa-metricLabel">{t('superadmin.totalUsers')}</div>
                <div className="sa-metricValue">{metrics.totals.totalUsers}</div>
              </div>
              <div className="sa-metric">
                <div className="sa-metricLabel">{t('superadmin.activeUsers')}</div>
                <div className="sa-metricValue">{metrics.totals.activeUsers}</div>
              </div>
            </div>
          )}
        </div>

        <div className="sa-tenants card">
          <h2 className="sa-sectionTitle">{t('superadmin.tenantPicker')}</h2>

          <input
            className="sa-search"
            placeholder={t('superadmin.searchPlaceholder')}
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />

          <div className="sa-tenantList">
            {filtered.map((tenant) => (
              <button
                key={tenant.id}
                className="sa-tenantRow"
                onClick={() => handleSwitch(tenant)}
                disabled={switching !== null}
              >
                <div className="sa-tenantName">{tenant.name}</div>
                <div className="sa-tenantMeta">
                  <span className="mono">{tenant.slug}</span>
                  <span className="mono">{tenant.id}</span>
                </div>
                <div className="sa-tenantAction">
                  {switching === tenant.id ? t('superadmin.switching') : t('superadmin.enter')}
                </div>
              </button>
            ))}
          </div>
        </div>
      </div>
    </PageShell>
  );
}

