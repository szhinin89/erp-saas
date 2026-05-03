import { useEffect, useMemo, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { PageShell, TableCard, EmptyState, ErrorState, LoadingState, Badge } from '../components/PageShell';
import {
  superAdminService,
  type SaasFeatureDefinitionAdmin,
  type SuperAdminPlan,
  type SuperAdminTenant,
} from '../services/superAdminService';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import { ZHBtn, ZHField } from '../components/zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../components/zh/ZHLayout';
import { ZHDashboardScaffold, ZHKpiPanel } from '../components/zh/ZHDashboard';
import { formatApiRequestError } from '../modules/lib/apiError';
import '../components/zh/ZHFormTabs.css';
import './SuperAdminPlansPage.css';
import './SuperAdminPanelPage.css';

type SuperAdminHomeTab = 'overview' | 'companies' | 'plans';

function formatPlanMoney(amount: number | undefined, currency: string | undefined) {
  const a = amount ?? 0;
  const c = (currency ?? 'USD').trim() || 'USD';
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency: c, maximumFractionDigits: 2 }).format(a);
  } catch {
    return `${a} ${c}`;
  }
}

/** Misma jerarquía visual que `/superadmin/plans`. */
function planVisualTier(code: string): 'starter' | 'business' | 'professional' | 'enterprise' | 'default' {
  const c = (code ?? '').trim().toLowerCase();
  if (c === 'starter') return 'starter';
  if (c === 'business') return 'business';
  if (c === 'professional') return 'professional';
  if (c === 'enterprise') return 'enterprise';
  return 'default';
}

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
  const [featureDefs, setFeatureDefs] = useState<SaasFeatureDefinitionAdmin[]>([]);
  const [plansLoading, setPlansLoading] = useState(true);
  const [plansError, setPlansError] = useState('');
  const [homeTab, setHomeTab] = useState<SuperAdminHomeTab>('overview');

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
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
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
        const [list, defs] = await Promise.all([
          superAdminService.getPlansCatalog(),
          superAdminService.listSaasFeatureDefinitions().catch(() => [] as SaasFeatureDefinitionAdmin[]),
        ]);
        if (!cancelled) {
          setPlans(list);
          setFeatureDefs(Array.isArray(defs) ? defs : []);
        }
      } catch (e) {
        if (!cancelled) {
          setPlansError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
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

  const sortedPlans = useMemo(
    () => [...plans].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.code.localeCompare(b.code)),
    [plans],
  );

  const sortedFeatureDefs = useMemo(
    () => [...featureDefs].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
    [featureDefs],
  );

  const planTenantStats = useMemo(() => {
    const counts = new Map<string, number>();
    for (const tn of tenants) {
      const c = (tn.planCode ?? '').trim().toLowerCase();
      if (!c) continue;
      counts.set(c, (counts.get(c) ?? 0) + 1);
    }
    let max = 0;
    for (const v of counts.values()) max = Math.max(max, v);
    return { counts, max };
  }, [tenants]);

  const handleSwitch = async (tenant: SuperAdminTenant) => {
    setSwitching(tenant.id);
    setError('');
    try {
      const auth = await superAdminService.switchTenant(tenant.id);
      storeImpersonationTenantName(tenant.name);
      login(auth);
      navigate('/dashboard');
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSwitching(null);
    }
  };

  if (!isSuperAdmin) {
    return (
      <PageShell kicker={t('app.nav.group.home')} title={t('superadmin.title')}>
        <TableCard>
          <div className="empty-state">{t('superadmin.noAccess')}</div>
        </TableCard>
      </PageShell>
    );
  }

  if (hasSelectedTenant) {
    return (
      <PageShell
        kicker={t('app.nav.group.home')}
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
      kicker={t('app.nav.group.home')}
      title={t('superadmin.title')}
      subtitle={t('superadmin.subtitle')}
      action={
        <NavLink to="/superadmin/instance-quota">{t('app.nav.superadmin.instanceQuota')}</NavLink>
      }
    >
      {error ? <ErrorState message={error} /> : null}

      <ZHDashboardScaffold>
        <div className="sa-panelTabsWrap">
          <div className="zh-form-tabs sa-panelTabs" role="tablist" aria-label={t('superadmin.title')}>
            <button
              type="button"
              role="tab"
              aria-selected={homeTab === 'overview'}
              className={homeTab === 'overview' ? 'is-active' : ''}
              onClick={() => setHomeTab('overview')}
            >
              {t('superadmin.tabOverview')}
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={homeTab === 'companies'}
              className={homeTab === 'companies' ? 'is-active' : ''}
              onClick={() => setHomeTab('companies')}
            >
              {t('superadmin.tabCompanies')}
            </button>
            <button
              type="button"
              role="tab"
              aria-selected={homeTab === 'plans'}
              className={homeTab === 'plans' ? 'is-active' : ''}
              onClick={() => setHomeTab('plans')}
            >
              {t('superadmin.tabPlans')}
            </button>
          </div>
        </div>

        {homeTab === 'overview' ? (
          <div className="sa-overviewKpi">
            {loading ? (
              <TableCard>
                <LoadingState />
              </TableCard>
            ) : metrics ? (
              <ZHKpiPanel
                title={t('superadmin.metrics')}
                items={[
                  { label: t('superadmin.totalTenants'), value: String(metrics.totals.totalTenants), tone: 'neutral' },
                  { label: t('superadmin.activeTenants'), value: String(metrics.totals.activeTenants), tone: 'info' },
                  { label: t('superadmin.totalUsers'), value: String(metrics.totals.totalUsers), tone: 'neutral' },
                  { label: t('superadmin.activeUsers'), value: String(metrics.totals.activeUsers), tone: 'success' },
                ]}
              />
            ) : null}
          </div>
        ) : homeTab === 'companies' ? (
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
              ) : error ? (
                <EmptyState message={t('superadmin.sectionLoadHint')} />
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
                          {t('common.users') ?? 'Usuarios'}: <strong>{tenant.totalUsers}</strong> · {t('common.active')}:{' '}
                          <strong>{tenant.activeUsers}</strong>
                        </span>
                        <span className="subtle">{new Date(tenant.createdAt).toLocaleDateString()}</span>
                      </div>
                      <div className="sa-tenant-actions">
                        <ZHInlineRowRight>
                          <ZHBtn
                            variant="secondary"
                            size="md"
                            type="button"
                            disabled={switching !== null}
                            onClick={() => navigate(`/companies?subscription=${encodeURIComponent(tenant.id)}`)}
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
        ) : (
          <div className="sa-plansWrap">
            <TableCard>
              <ZHCardSection title={t('superadmin.plans.title')}>
                <p className="subtle sa-plansIntro">{t('superadmin.plans.subtitle')}</p>
                <div className="sa-plansAdminLink">
                  <NavLink className="zh-btn zh-btn--primary zh-btn--md" to="/superadmin/plans">
                    {t('superadmin.plans.manageLink')}
                  </NavLink>
                </div>
                {plansLoading ? (
                  <LoadingState />
                ) : plansError ? (
                  <ErrorState message={plansError} />
                ) : plans.length === 0 ? (
                  <EmptyState message={t('superadmin.plans.empty')} />
                ) : (
                  <div className="sap-pricing-grid">
                    {sortedPlans.map((plan) => {
                      const tier = planVisualTier(plan.code);
                      const codeKey = plan.code.trim().toLowerCase();
                      const taglineKey = `superadmin.plansCard.tagline.${codeKey}`;
                      const taglineResolved = t(taglineKey);
                      const taglineText =
                        taglineResolved !== taglineKey ? taglineResolved : t('superadmin.plansCard.taglineFallback');
                      const tenantCount = planTenantStats.counts.get(codeKey) ?? 0;
                      const usagePct = planTenantStats.max > 0 ? Math.round((tenantCount / planTenantStats.max) * 100) : 0;
                      const isDefIncluded = (def: SaasFeatureDefinitionAdmin) =>
                        plan.features.some(
                          (f) =>
                            f.isIncluded &&
                            (f.featureCode ?? '').trim().toLowerCase() === (def.code ?? '').trim().toLowerCase(),
                        );
                      return (
                        <article
                          key={plan.id}
                          className={[
                            'sap-pricing-card',
                            `sap-pricing-card--tier-${tier}`,
                            plan.isRecommended ? 'sap-pricing-card--popular' : '',
                            plan.isActive ? '' : 'sap-pricing-card--inactive',
                          ]
                            .filter(Boolean)
                            .join(' ')}
                        >
                          <div className="sap-pricing-card-inner">
                            <div className="sap-pricing-topBadges">
                              {plan.isRecommended ? (
                                <span className="sap-pricing-ribbon">{t('superadmin.plansCard.mostPopular')}</span>
                              ) : null}
                              <span className="sap-pricing-planBadge mono">{plan.shortLabel ?? plan.code.toUpperCase()}</span>
                            </div>
                            <h3 className="sap-pricing-title">
                              {plan.name}
                              {plan.isRecommended ? <span className="sap-pricing-titleStar" aria-hidden> ★</span> : null}
                            </h3>
                            <p className="sap-pricing-subtitle">{taglineText}</p>
                            <div className="sap-pricing-priceRow">
                              <span className="sap-pricing-price">{formatPlanMoney(plan.priceAmount, plan.currency)}</span>
                              <span className="sap-pricing-period">
                                /
                                {(() => {
                                  const bc = (plan.billingCycle ?? 'monthly').toLowerCase();
                                  const k = `superadmin.plansCard.billingSuffix.${bc}`;
                                  const s = t(k);
                                  return s !== k ? s : t('superadmin.plansCard.billingSuffix.monthly');
                                })()}
                              </span>
                            </div>
                            <div className="sap-pricing-usage">
                              <div className="sap-pricing-usageLine">
                                <span>
                                  {tenantCount} {t('superadmin.plansCard.tenantsUnit')}
                                </span>
                                <span className="sap-pricing-usagePct">{usagePct}%</span>
                              </div>
                              <div className="sap-pricing-bar" aria-hidden>
                                <svg viewBox="0 0 100 6" preserveAspectRatio="none" className="sap-pricing-barSvg">
                                  <rect
                                    className="sap-pricing-barRect"
                                    x="0"
                                    y="0"
                                    width={Math.max(0, Math.min(100, usagePct))}
                                    height="6"
                                    rx="3"
                                  />
                                </svg>
                              </div>
                            </div>
                            <ul className="sap-pricing-features">
                              {sortedFeatureDefs.length > 0
                                ? sortedFeatureDefs.map((def) => {
                                    const on = isDefIncluded(def);
                                    return (
                                      <li key={def.id} className={on ? 'sap-pricing-ft--on' : 'sap-pricing-ft--off'}>
                                        <span className="sap-pricing-ftIcon" aria-hidden>
                                          {on ? '✓' : '—'}
                                        </span>
                                        <span className="sap-pricing-ftLabel">{def.name}</span>
                                      </li>
                                    );
                                  })
                                : plan.features
                                    .filter((f) => f.isIncluded)
                                    .map((f) => (
                                      <li key={`${plan.id}-${f.featureCode}`} className="sap-pricing-ft--on">
                                        <span className="sap-pricing-ftIcon" aria-hidden>
                                          ✓
                                        </span>
                                        <span className="sap-pricing-ftLabel">{f.featureName}</span>
                                      </li>
                                    ))}
                            </ul>
                            <div className="sap-pricing-metaRow subtle">
                              <Badge
                                label={plan.isActive ? t('common.active') : t('common.inactive')}
                                variant={plan.isActive ? 'green' : 'gray'}
                              />
                              {plan.isPubliclyVisible === false ? (
                                <Badge label={t('superadmin.plansAdmin.hidden')} variant="gray" />
                              ) : (
                                <Badge label={t('superadmin.plansAdmin.public')} variant="green" />
                              )}
                              <span className="mono">{plan.code}</span>
                            </div>
                            <footer className="sap-pricing-footer">
                              <div className="sap-pricing-footerMain">
                                <NavLink
                                  className="zh-btn zh-btn--ghost zh-btn--md sap-pricing-linkBtn"
                                  to={`/companies?plan=${encodeURIComponent(plan.code)}`}
                                >
                                  {t('superadmin.plansCard.viewTenants')}
                                </NavLink>
                              </div>
                            </footer>
                          </div>
                        </article>
                      );
                    })}
                  </div>
                )}
              </ZHCardSection>
            </TableCard>
          </div>
        )}
      </ZHDashboardScaffold>
    </PageShell>
  );
}

