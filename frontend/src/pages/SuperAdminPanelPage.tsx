import { useEffect, useMemo, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge } from '../components/PageShell';
import { superAdminService, type SuperAdminPlan, type SuperAdminTenant } from '../services/superAdminService';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../components/zh/ZHLayout';
import { ZHDashboardScaffold, ZHKpiPanel, ZHPanelGrid } from '../components/zh/ZHDashboard';
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
  const [plans, setPlans] = useState<SuperAdminPlan[]>([]);
  const [plansLoading, setPlansLoading] = useState(true);
  const [plansError, setPlansError] = useState('');

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

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setPlansLoading(true);
        setPlansError('');
        const list = await superAdminService.getPlansCatalog();
        if (!cancelled) setPlans(list);
      } catch (e) {
        if (!cancelled) {
          setPlansError(
            (e as { response?: { data?: { message?: string } } })?.response?.data?.message ?? t('common.errorGeneric'),
          );
        }
      } finally {
        if (!cancelled) setPlansLoading(false);
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
      <PageShell kicker={t('app.nav.group.saas')} title={t('superadmin.title')}>
        <TableCard>
          <div className="empty-state">{t('superadmin.noAccess')}</div>
        </TableCard>
      </PageShell>
    );
  }

  if (hasSelectedTenant) {
    return (
      <PageShell
        kicker={t('app.nav.group.saas')}
        title={t('superadmin.title')}
        action={
          <ZHBtn variant="primary" size="md" type="button" onClick={() => navigate('/dashboard')}>
            {t('superadmin.goToTenant')}
          </ZHBtn>
        }
      >
        <TableCard>
          <div className="empty-state">{t('superadmin.alreadyInTenant')}</div>
        </TableCard>
      </PageShell>
    );
  }

  return (
    <PageShell
      kicker={t('app.nav.group.saas')}
      title={t('superadmin.title')}
      subtitle={t('superadmin.subtitle')}
      action={
        <NavLink to="/superadmin/instance-quota">{t('app.nav.superadmin.instanceQuota')}</NavLink>
      }
    >
      {error ? <ErrorState message={error} /> : null}

      <ZHDashboardScaffold>
        <ZHPanelGrid className="zh-dash-panels--leftNarrow">
          <div>
            {loading || !metrics ? (
              <TableCard><LoadingState /></TableCard>
            ) : (
              <ZHKpiPanel
                title={t('superadmin.metrics')}
                items={[
                  { label: t('superadmin.totalTenants'), value: String(metrics.totals.totalTenants), tone: 'neutral' },
                  { label: t('superadmin.activeTenants'), value: String(metrics.totals.activeTenants), tone: 'info' },
                  { label: t('superadmin.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
                  { label: t('superadmin.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
                ]}
              />
            )}
          </div>

          <TableCard>
            <ZHCardSection title={t('superadmin.tenantPicker')}>
              <ZHGridRow cols={1}>
                <ZHField label={t('superadmin.searchPlaceholder')}>
                  <input
                    value={q}
                    onChange={(e) => setQ(e.target.value)}
                    placeholder={t('superadmin.searchPlaceholder')}
                    disabled={loading || switching !== null}
                  />
                </ZHField>
              </ZHGridRow>

              {loading ? (
                <LoadingState />
              ) : filtered.length === 0 ? (
                <EmptyState message={t('common.noData')} />
              ) : (
                <div className="sa-tenantList">
                  {filtered.map((tenant) => (
                    <div key={tenant.id} className="sa-tenantRow">
                      <div className="sa-tenantName">{tenant.name}</div>
                      <div className="sa-tenantMeta">
                        <span className="mono">{tenant.slug}</span>
                        <span className="mono">{tenant.id}</span>
                      </div>
                      <div className="sa-tenant-stats">
                        <Badge
                          label={tenant.isActive ? t('common.active') : t('common.inactive')}
                          variant={tenant.isActive ? 'green' : 'gray'}
                        />
                        <span className="subtle">
                          {t('common.users') ?? 'Usuarios'}: <strong>{tenant.totalUsers}</strong> · {t('common.active')}: <strong>{tenant.activeUsers}</strong>
                        </span>
                        <span className="subtle">
                          {new Date(tenant.createdAt).toLocaleDateString()}
                        </span>
                      </div>
                      <div className="sa-tenant-actions">
                        <ZHInlineRowRight>
                          <ZHBtn
                            variant="secondary"
                            size="md"
                            type="button"
                            disabled={switching !== null}
                            onClick={() =>
                              navigate(`/companies?subscription=${encodeURIComponent(tenant.id)}`)
                            }
                          >
                            {t('superadmin.subscription')}
                          </ZHBtn>
                          <ZHBtn
                            variant="primary"
                            size="md"
                            type="button"
                            onClick={() => void handleSwitch(tenant)}
                            disabled={switching !== null}
                          >
                            {switching === tenant.id ? t('superadmin.switching') : t('superadmin.enter')}
                          </ZHBtn>
                        </ZHInlineRowRight>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </ZHCardSection>
          </TableCard>
        </ZHPanelGrid>

        <div className="sa-plansWrap">
          <TableCard>
            <ZHCardSection title={t('superadmin.plans.title')}>
              <p className="subtle sa-plansIntro">{t('superadmin.plans.subtitle')}</p>
              {plansError ? <ErrorState message={plansError} /> : null}
              {plansLoading ? (
                <LoadingState />
              ) : plans.length === 0 ? (
                <EmptyState message={t('superadmin.plans.empty')} />
              ) : (
                <div className="sa-plansGrid">
                  {plans.map((plan) => (
                    <article key={plan.id} className="sa-planCard">
                      <header className="sa-planCard-head">
                        <div>
                          <div className="sa-planCard-name">{plan.name}</div>
                          <div className="sa-planCard-code mono">{plan.code}</div>
                        </div>
                        <Badge
                          label={plan.isActive ? t('common.active') : t('common.inactive')}
                          variant={plan.isActive ? 'green' : 'gray'}
                        />
                      </header>
                      <ul className="sa-planFeatureList">
                        {plan.features.map((f) => (
                          <li key={`${plan.id}-${f.featureCode}`} className="sa-planFeatureRow">
                            <div className="sa-planFeatureMain">
                              <span className="sa-planFeatureCode mono">{f.featureCode}</span>
                              <span className="sa-planFeatureName">{f.featureName}</span>
                            </div>
                            <div className="sa-planFeatureMeta">
                              {f.isMetered ? (
                                <span className="sa-planPill">{t('superadmin.plans.metered')}</span>
                              ) : (
                                <span className="sa-planPill sa-planPill--soft">{t('superadmin.plans.module')}</span>
                              )}
                              <span className="subtle">
                                {f.limitPerPeriod != null
                                  ? `${t('superadmin.plans.limitLabel')}: ${f.limitPerPeriod}`
                                  : t('superadmin.plans.limitUnlimited')}
                              </span>
                            </div>
                            {f.description ? <div className="sa-planFeatureDesc subtle">{f.description}</div> : null}
                          </li>
                        ))}
                      </ul>
                    </article>
                  ))}
                </div>
              )}
            </ZHCardSection>
          </TableCard>
        </div>
      </ZHDashboardScaffold>
    </PageShell>
  );
}

