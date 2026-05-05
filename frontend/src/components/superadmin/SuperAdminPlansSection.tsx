import { useCallback, useEffect, useMemo, useState } from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import { TableCard, EmptyState, LoadingState, Badge } from '../PageShell';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { useSuperAdminGate } from '../../hooks/useSuperAdminGate';
import { Modal } from '../Modal';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHModalHeader } from '../zh/ZHModalHeader';
import {
  superAdminService,
  type CreateSaasPlanBody,
  type PlanFeatureAssignBody,
  type SaasFeatureDefinitionAdmin,
  type SaasPlanAdmin,
  type SaasPublicPlan,
  type SuperAdminMetrics,
  type SuperAdminTenant,
  type UpdateSaasPlanBody,
} from '../../services/superAdminService';
import { useI18n } from '../../i18n/i18n';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import { formatApiRequestError } from '../../modules/lib/apiError';
import { goToCompaniesTenantDetail } from '../../navigation/companiesTenantDetailNav';
import './SuperAdminPlansSection.css';

const POLL_MS = 20_000;

/** Tema visual por código de plan (alineado a tarjetas comerciales tipo pricing). */
function planVisualTier(code: string): 'starter' | 'business' | 'professional' | 'enterprise' | 'default' {
  const c = (code ?? '').trim().toLowerCase();
  if (c === 'starter') return 'starter';
  if (c === 'business') return 'business';
  if (c === 'professional') return 'professional';
  if (c === 'enterprise') return 'enterprise';
  return 'default';
}

/** Precio mensual equivalente para estimar MRR (planes con ciclo distinto a mensual). */
function monthlyEquivalentPrice(plan: SaasPlanAdmin): number {
  const c = (plan.billingCycle ?? 'monthly').toLowerCase();
  if (c === 'yearly') return plan.priceAmount / 12;
  if (c === 'quarterly') return plan.priceAmount / 3;
  if (c === 'one_time') return 0;
  return plan.priceAmount;
}

function csvEscape(cell: string): string {
  const s = String(cell ?? '');
  if (/[",\n\r]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

type PlanFormState = {
  code: string;
  name: string;
  shortLabel: string;
  isActive: boolean;
  priceAmount: string;
  currency: string;
  billingCycle: string;
  isPubliclyVisible: boolean;
  isRecommended: boolean;
  sortOrder: string;
  externalBillingRef: string;
};

function emptyPlanForm(): PlanFormState {
  return {
    code: '',
    name: '',
    shortLabel: '',
    isActive: true,
    priceAmount: '0',
    currency: 'USD',
    billingCycle: 'monthly',
    isPubliclyVisible: true,
    isRecommended: false,
    sortOrder: '0',
    externalBillingRef: '',
  };
}

function planToForm(p: SaasPlanAdmin): PlanFormState {
  return {
    code: p.code,
    name: p.name,
    shortLabel: p.shortLabel ?? '',
    isActive: p.isActive,
    priceAmount: String(p.priceAmount),
    currency: p.currency,
    billingCycle: p.billingCycle,
    isPubliclyVisible: p.isPubliclyVisible,
    isRecommended: p.isRecommended,
    sortOrder: String(p.sortOrder),
    externalBillingRef: p.externalBillingRef ?? '',
  };
}

/** Panel de planes SaaS y tenants; el catálogo menú ↔ plan se administra en Empresas → Plan ↔ menú. */
export function SuperAdminPlansSection() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { isSuperAdmin } = useSuperAdminGate();

  const [plans, setPlans] = useState<SaasPlanAdmin[]>([]);
  const [publicPlans, setPublicPlans] = useState<SaasPublicPlan[]>([]);
  const [features, setFeatures] = useState<SaasFeatureDefinitionAdmin[]>([]);
  const [tenants, setTenants] = useState<SuperAdminTenant[]>([]);
  const [metrics, setMetrics] = useState<SuperAdminMetrics | null>(null);
  const [tenantSearch, setTenantSearch] = useState('');
  const [tenantPlanFilter, setTenantPlanFilter] = useState('');
  const [tenantStatusFilter, setTenantStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const [planModal, setPlanModal] = useState<'closed' | 'create' | 'edit'>('closed');
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null);
  const [planForm, setPlanForm] = useState<PlanFormState>(emptyPlanForm);

  const [featuresModalPlanId, setFeaturesModalPlanId] = useState<string | null>(null);
  const [featureRows, setFeatureRows] = useState<Record<string, { included: boolean; limit: string }>>({});

  const [deletePlanId, setDeletePlanId] = useState<string | null>(null);

  const loadAll = useCallback(async () => {
    const [p, defs, pub, tns, met] = await Promise.all([
      superAdminService.listSaasPlansAdmin(),
      superAdminService.listSaasFeatureDefinitions(),
      superAdminService.getPublicPlans().catch(() => [] as SaasPublicPlan[]),
      superAdminService.getTenants().catch(() => [] as SuperAdminTenant[]),
      superAdminService.getMetrics().catch(() => null),
    ]);
    const sorted = [...p].sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
    setPlans(sorted);
    setFeatures(defs);
    setPublicPlans(pub);
    setTenants(Array.isArray(tns) ? tns : []);
    setMetrics(met && typeof met === 'object' && 'totals' in met ? (met as SuperAdminMetrics) : null);
  }, []);

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

  const sortedFeatureDefs = useMemo(
    () => [...features].sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })),
    [features],
  );

  const planByCode = useMemo(() => {
    const m = new Map<string, SaasPlanAdmin>();
    for (const p of plans) m.set(p.code.trim().toLowerCase(), p);
    return m;
  }, [plans]);

  const approxMrr = useMemo(() => {
    let sum = 0;
    for (const tn of tenants) {
      if (!tn.isActive) continue;
      const pc = (tn.planCode ?? '').trim().toLowerCase();
      if (!pc) continue;
      const p = planByCode.get(pc);
      if (!p?.isActive) continue;
      sum += monthlyEquivalentPrice(p);
    }
    return sum;
  }, [tenants, planByCode]);

  const filteredTenantsForTable = useMemo(() => {
    let r = tenants;
    const pf = tenantPlanFilter.trim().toLowerCase();
    if (pf) r = r.filter((tn) => (tn.planCode ?? '').trim().toLowerCase() === pf);
    if (tenantStatusFilter === 'active') r = r.filter((tn) => tn.isActive);
    if (tenantStatusFilter === 'inactive') r = r.filter((tn) => !tn.isActive);
    const q = tenantSearch.trim().toLowerCase();
    if (q) r = r.filter((tn) => `${tn.name} ${tn.slug} ${tn.id}`.toLowerCase().includes(q));
    return r;
  }, [tenants, tenantPlanFilter, tenantSearch, tenantStatusFilter]);

  const exportTenantsCsv = () => {
    const headers = [
      'id',
      'name',
      'slug',
      'planCode',
      'activeUsers',
      'totalUsers',
      'createdAt',
      'mrrApprox',
    ];
    const lines = [headers.join(',')];
    for (const tn of filteredTenantsForTable) {
      const pc = (tn.planCode ?? '').trim().toLowerCase();
      const p = pc ? planByCode.get(pc) : undefined;
      const mrr = p?.isActive ? monthlyEquivalentPrice(p) : 0;
      lines.push(
        [
          csvEscape(tn.id),
          csvEscape(tn.name),
          csvEscape(tn.slug),
          csvEscape(tn.planCode ?? ''),
          String(tn.activeUsers),
          String(tn.totalUsers),
          csvEscape(tn.createdAt),
          String(mrr),
        ].join(','),
      );
    }
    const blob = new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `saas-tenants-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  useEffect(() => {
    if (!isSuperAdmin) return;
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        await loadAll();
      } catch (e) {
        if (!cancelled) {
          setError(
            formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }),
          );
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [isSuperAdmin, loadAll, t]);

  useEffect(() => {
    if (!isSuperAdmin) return;
    const tick = () => {
      if (document.visibilityState !== 'visible') return;
      void loadAll().catch(() => undefined);
    };
    const id = window.setInterval(tick, POLL_MS);
    const onVis = () => { if (document.visibilityState === 'visible') void loadAll().catch(() => undefined); };
    document.addEventListener('visibilitychange', onVis);
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', onVis);
    };
  }, [isSuperAdmin, loadAll]);

  const openCreatePlan = () => {
    setPlanForm(emptyPlanForm());
    setEditingPlanId(null);
    setPlanModal('create');
  };

  const openEditPlan = (p: SaasPlanAdmin) => {
    setPlanForm(planToForm(p));
    setEditingPlanId(p.id);
    setPlanModal('edit');
  };

  const openFeaturesModal = (p: SaasPlanAdmin) => {
    const map: Record<string, { included: boolean; limit: string }> = {};
    for (const d of features) {
      const row = p.features.find((f) => f.featureId === d.id);
      map[d.id] = {
        included: row?.isIncluded ?? false,
        limit: row?.limitPerPeriod != null ? String(row.limitPerPeriod) : '',
      };
    }
    setFeatureRows(map);
    setFeaturesModalPlanId(p.id);
  };

  const savePlan = async () => {
    if (!editingPlanId && planModal !== 'create') return;
    setBusy(true);
    setError('');
    try {
      const price = Number(planForm.priceAmount.replace(',', '.'));
      const sort = Number.parseInt(planForm.sortOrder, 10) || 0;
      const ext = planForm.externalBillingRef.trim() || null;
      const shortLabel = planForm.shortLabel.trim() || null;

      if (planModal === 'create') {
        const body: CreateSaasPlanBody = {
          code: planForm.code.trim(),
          name: planForm.name.trim(),
          shortLabel,
          isActive: planForm.isActive,
          priceAmount: Number.isFinite(price) ? price : 0,
          currency: planForm.currency.trim().toUpperCase() || 'USD',
          billingCycle: planForm.billingCycle.trim().toLowerCase() || 'monthly',
          isPubliclyVisible: planForm.isPubliclyVisible,
          isRecommended: planForm.isRecommended,
          sortOrder: sort,
          externalBillingRef: ext,
        };
        await superAdminService.createSaasPlan(body);
      } else if (editingPlanId) {
        const body: UpdateSaasPlanBody = {
          name: planForm.name.trim(),
          shortLabel,
          isActive: planForm.isActive,
          priceAmount: Number.isFinite(price) ? price : 0,
          currency: planForm.currency.trim().toUpperCase() || 'USD',
          billingCycle: planForm.billingCycle.trim().toLowerCase() || 'monthly',
          isPubliclyVisible: planForm.isPubliclyVisible,
          externalBillingRef: ext,
        };
        await superAdminService.updateSaasPlan(editingPlanId, body);
        if (planForm.isRecommended) {
          await superAdminService.setSaasPlanRecommended(editingPlanId);
        }
      }
      setPlanModal('closed');
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  const saveFeatures = async () => {
    if (!featuresModalPlanId) return;
    setBusy(true);
    setError('');
    try {
      const rows: PlanFeatureAssignBody[] = features.map((d) => {
        const r = featureRows[d.id] ?? { included: false, limit: '' };
        const lim = r.limit.trim() === '' ? null : Number.parseInt(r.limit, 10);
        return {
          featureId: d.id,
          isIncluded: r.included,
          limitPerPeriod: lim != null && Number.isFinite(lim) ? lim : null,
        };
      });
      await superAdminService.replaceSaasPlanFeatures(featuresModalPlanId, rows);
      setFeaturesModalPlanId(null);
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async () => {
    if (!deletePlanId) return;
    setBusy(true);
    setError('');
    try {
      await superAdminService.deleteSaasPlan(deletePlanId);
      setDeletePlanId(null);
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  const movePlan = async (index: number, dir: -1 | 1) => {
    const j = index + dir;
    if (j < 0 || j >= plans.length) return;
    const next = [...plans];
    const tmp = next[index];
    next[index] = next[j];
    next[j] = tmp;
    setPlans(next);
    setBusy(true);
    setError('');
    try {
      await superAdminService.reorderSaasPlans(next.map((p) => p.id));
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      await loadAll();
    } finally {
      setBusy(false);
    }
  };

  const setRecommendedOnly = async (planId: string) => {
    setBusy(true);
    setError('');
    try {
      await superAdminService.setSaasPlanRecommended(planId);
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  };

  const formatMoney = (amount: number, currency: string) => {
    try {
      return new Intl.NumberFormat(undefined, { style: 'currency', currency: currency || 'USD', maximumFractionDigits: 2 }).format(amount);
    } catch {
      return `${amount} ${currency}`;
    }
  };

  const activePlansCount = plans.filter((p) => p.isActive).length;
  const activePlanNames = plans
    .filter((p) => p.isActive)
    .map((p) => p.shortLabel ?? p.name)
    .join(', ');
  const totals = metrics?.totals;
  const inactiveTenants = totals ? Math.max(0, totals.totalTenants - totals.activeTenants) : 0;
  const inactivePct = totals && totals.totalTenants > 0 ? Math.round((inactiveTenants / totals.totalTenants) * 100) : 0;

  return (
    <div className="sap-plansSection">
      <div className="sap-dash-head">
        <div className="sap-dash-headRow">
          <div className="sap-dash-headText">
            <p className="sap-dash-eyebrow">{t('superadmin.plansDashboard.eyebrow')}</p>
            <p className="subtle sap-dash-lead">{t('superadmin.plansAdmin.subtitle')}</p>
          </div>
          <div className="sap-dash-actions">
            <ZHBtn variant="ghost" size="md" type="button" onClick={exportTenantsCsv} disabled={busy || filteredTenantsForTable.length === 0}>
              {t('superadmin.plansDashboard.exportCsv')}
            </ZHBtn>
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => void loadAll()} disabled={busy}>
              {t('superadmin.plansAdmin.refresh')}
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="button" onClick={openCreatePlan} disabled={busy}>
              {t('superadmin.plansDashboard.newPlan')}
            </ZHBtn>
          </div>
        </div>
        <p className="subtle sap-publicHint">
          {t('superadmin.plansAdmin.publicHintPrefix')} <strong>{publicPlans.length}</strong> {t('superadmin.plansAdmin.publicHintSuffix')}
        </p>
      </div>

      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      {loading ? (
        <LoadingState />
      ) : (
        <>
          <div className="sap-dash-kpiGrid">
            <div className="sap-dash-kpiCard sap-dash-kpiCard--info">
              <div className="sap-dash-kpiValue">{totals?.activeTenants ?? tenants.length}</div>
              <div className="sap-dash-kpiSub">
                {totals
                  ? t('superadmin.plansDashboard.kpi.activeTenantsSub').replace('{{total}}', String(totals.totalTenants))
                  : '—'}
              </div>
              <div className="sap-dash-kpiLabel">{t('superadmin.plansDashboard.kpi.activeTenants')}</div>
            </div>
            <div className="sap-dash-kpiCard sap-dash-kpiCard--success">
              <div className="sap-dash-kpiValue">{formatMoney(approxMrr, plans.find((p) => p.isActive)?.currency ?? 'USD')}</div>
              <div className="sap-dash-kpiSub">{t('superadmin.plansDashboard.kpi.mrrSub')}</div>
              <div className="sap-dash-kpiLabel">{t('superadmin.plansDashboard.kpi.mrr')}</div>
            </div>
            <div className="sap-dash-kpiCard sap-dash-kpiCard--neutral">
              <div className="sap-dash-kpiValue">{activePlansCount}</div>
              <div className="sap-dash-kpiSub">{activePlanNames || '—'}</div>
              <div className="sap-dash-kpiLabel">{t('superadmin.plansDashboard.kpi.catalogPlans')}</div>
            </div>
            <div className="sap-dash-kpiCard sap-dash-kpiCard--warn">
              <div className="sap-dash-kpiValue">{inactivePct}%</div>
              <div className="sap-dash-kpiSub">
                {totals
                  ? t('superadmin.plansDashboard.kpi.inactiveSub').replace('{{n}}', String(inactiveTenants))
                  : '—'}
              </div>
              <div className="sap-dash-kpiLabel">{t('superadmin.plansDashboard.kpi.inactiveRatio')}</div>
            </div>
          </div>

          {plans.length === 0 ? (
            <TableCard>
              <EmptyState message={t('superadmin.plans.empty')} />
            </TableCard>
          ) : (
            <div className="sap-pricing-grid">
          {plans.map((plan, index) => {
            const tier = planVisualTier(plan.code);
            const codeKey = plan.code.trim().toLowerCase();
            const taglineKey = `superadmin.plansCard.tagline.${codeKey}`;
            const taglineResolved = t(taglineKey);
            const taglineText = taglineResolved !== taglineKey ? taglineResolved : t('superadmin.plansCard.taglineFallback');
            const tenantCount = planTenantStats.counts.get(codeKey) ?? 0;
            const usagePct = planTenantStats.max > 0 ? Math.round((tenantCount / planTenantStats.max) * 100) : 0;
            const isIncluded = (featureId: string) =>
              plan.features.some((f) => String(f.featureId) === String(featureId) && f.isIncluded);
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
                    <span className="sap-pricing-price">{formatMoney(plan.priceAmount, plan.currency)}</span>
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
                    {sortedFeatureDefs.map((def) => {
                      const on = isIncluded(def.id);
                      return (
                        <li key={def.id} className={on ? 'sap-pricing-ft--on' : 'sap-pricing-ft--off'}>
                          <span className="sap-pricing-ftIcon" aria-hidden>
                            {on ? '✓' : '—'}
                          </span>
                          <span className="sap-pricing-ftLabel">{def.name}</span>
                        </li>
                      );
                    })}
                  </ul>
                  <div className="sap-pricing-metaRow subtle">
                    <Badge label={plan.isActive ? t('common.active') : t('common.inactive')} variant={plan.isActive ? 'green' : 'gray'} />
                    {plan.isPubliclyVisible ? (
                      <Badge label={t('superadmin.plansAdmin.public')} variant="green" />
                    ) : (
                      <Badge label={t('superadmin.plansAdmin.hidden')} variant="gray" />
                    )}
                    <span className="mono">{plan.code}</span>
                  </div>
                  <footer className="sap-pricing-footer">
                    <div className="sap-pricing-footerMain">
                      <ZHBtn variant="secondary" size="md" type="button" onClick={() => openEditPlan(plan)} disabled={busy}>
                        {t('common.edit')}
                      </ZHBtn>
                      <NavLink
                        className="zh-btn zh-btn--ghost zh-btn--md sap-pricing-linkBtn"
                        to={`/companies?plan=${encodeURIComponent(plan.code)}`}
                      >
                        {t('superadmin.plansCard.viewTenants')}
                      </NavLink>
                    </div>
                    <div className="sap-pricing-footerExtra">
                      <ZHBtn variant="ghost" size="sm" type="button" onClick={() => openFeaturesModal(plan)} disabled={busy}>
                        {t('superadmin.plansAdmin.features')}
                      </ZHBtn>
                      <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void movePlan(index, -1)} disabled={busy || index === 0}>
                        {t('superadmin.plansAdmin.up')}
                      </ZHBtn>
                      <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void movePlan(index, 1)} disabled={busy || index === plans.length - 1}>
                        {t('superadmin.plansAdmin.down')}
                      </ZHBtn>
                      <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void setRecommendedOnly(plan.id)} disabled={busy || plan.isRecommended}>
                        {t('superadmin.plansAdmin.recommend')}
                      </ZHBtn>
                      <ZHBtn variant="destructive" size="sm" type="button" onClick={() => setDeletePlanId(plan.id)} disabled={busy}>
                        {t('common.delete')}
                      </ZHBtn>
                    </div>
                  </footer>
                </div>
              </article>
            );
          })}
        </div>
      )}

      <TableCard>
        <ZHCardSection title={t('superadmin.plansDashboard.tenantsSectionTitle')}>
          <div className="sap-tenant-toolbar">
            <input
              type="search"
              className="zh-input sap-tenant-search"
              value={tenantSearch}
              onChange={(e) => setTenantSearch(e.target.value)}
              placeholder={t('superadmin.plansDashboard.tenantSearchPlaceholder')}
              aria-label={t('superadmin.plansDashboard.tenantSearchPlaceholder')}
            />
            <select
              className="zh-input sap-tenant-select"
              value={tenantPlanFilter}
              onChange={(e) => setTenantPlanFilter(e.target.value)}
              aria-label={t('superadmin.plansDashboard.filterPlan')}
            >
              <option value="">{t('superadmin.plansDashboard.filterAllPlans')}</option>
              {plans.map((p) => (
                <option key={p.id} value={p.code.trim().toLowerCase()}>
                  {p.shortLabel ?? p.name} ({p.code})
                </option>
              ))}
            </select>
            <select
              className="zh-input sap-tenant-select"
              value={tenantStatusFilter}
              onChange={(e) => setTenantStatusFilter(e.target.value as 'all' | 'active' | 'inactive')}
              aria-label={t('superadmin.plansDashboard.filterStatus')}
            >
              <option value="all">{t('superadmin.plansDashboard.filterAllStatuses')}</option>
              <option value="active">{t('superadmin.plansDashboard.statusActive')}</option>
              <option value="inactive">{t('superadmin.plansDashboard.statusInactive')}</option>
            </select>
          </div>
          {filteredTenantsForTable.length === 0 ? (
            <EmptyState message={t('superadmin.plansDashboard.tenantsEmpty')} />
          ) : (
            <div className="sap-tenant-tableWrap">
              <table className="sap-tenant-table">
                <thead>
                  <tr>
                    <th>{t('superadmin.plansDashboard.col.company')}</th>
                    <th>{t('superadmin.plansDashboard.col.plan')}</th>
                    <th>{t('superadmin.plansDashboard.col.users')}</th>
                    <th>{t('superadmin.plansDashboard.col.sinceExpires')}</th>
                    <th>{t('superadmin.plansDashboard.col.mrr')}</th>
                    <th>{t('superadmin.plansDashboard.col.status')}</th>
                    <th>{t('superadmin.plansDashboard.col.actions')}</th>
                  </tr>
                </thead>
                <tbody>
                  {filteredTenantsForTable.map((tn) => {
                    const pc = (tn.planCode ?? '').trim().toLowerCase();
                    const p = pc ? planByCode.get(pc) : undefined;
                    const tier = planVisualTier(pc || 'default');
                    const planLabel = p ? p.shortLabel ?? p.name : tn.planCode || '—';
                    const mrrVal = p?.isActive ? monthlyEquivalentPrice(p) : 0;
                    const showMrr = p?.isActive && mrrVal > 0;
                    return (
                      <tr key={tn.id}>
                        <td>
                          <div className="sap-tenant-company">{tn.name}</div>
                          <div className="mono subtle sap-tenant-id">{tn.slug}</div>
                        </td>
                        <td>
                          <span className={`sap-tenant-planBadge sap-tenant-planBadge--tier-${tier}`}>{planLabel}</span>
                        </td>
                        <td className="mono">
                          {tn.activeUsers}/{tn.totalUsers}
                        </td>
                        <td className="sap-tenant-dates">
                          <div>{new Date(tn.createdAt).toLocaleDateString()}</div>
                          <div className="subtle">{t('superadmin.plansDashboard.expiresNotTracked')}</div>
                        </td>
                        <td className="mono">{showMrr && p ? formatMoney(mrrVal, p.currency) : '—'}</td>
                        <td>
                          <span className={tn.isActive ? 'sap-tenant-status sap-tenant-status--active' : 'sap-tenant-status sap-tenant-status--suspended'}>
                            {tn.isActive ? t('superadmin.plansDashboard.statusActive') : t('superadmin.plansDashboard.statusSuspended')}
                          </span>
                        </td>
                        <td>
                          <ZHBtn
                            variant="ghost"
                            size="sm"
                            type="button"
                            onClick={() => goToCompaniesTenantDetail(navigate, tn.id)}
                          >
                            {t('common.edit')}
                          </ZHBtn>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </ZHCardSection>
      </TableCard>
        </>
      )}

      {planModal !== 'closed' ? (
        <Modal
          onClose={() => setPlanModal('closed')}
          size="lg"
          header={
            <ZHModalHeader
              title={planModal === 'create' ? t('superadmin.plansAdmin.modalCreate') : t('superadmin.plansAdmin.modalEdit')}
              subtitle={t('superadmin.plansAdmin.modalSubtitle')}
              onClose={() => setPlanModal('closed')}
            />
          }
        >
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.plansAdmin.field.code')}>
              <input
                className="zh-input"
                value={planForm.code}
                onChange={(e) => setPlanForm((s) => ({ ...s, code: e.target.value }))}
                disabled={planModal === 'edit'}
              />
            </ZHField>
            <ZHField label={t('superadmin.plansAdmin.field.shortLabel')}>
              <input
                className="zh-input"
                value={planForm.shortLabel}
                onChange={(e) => setPlanForm((s) => ({ ...s, shortLabel: e.target.value }))}
                placeholder="STARTER"
              />
            </ZHField>
          </ZHGridRow>
          <ZHField label={t('superadmin.plansAdmin.field.name')}>
            <input className="zh-input" value={planForm.name} onChange={(e) => setPlanForm((s) => ({ ...s, name: e.target.value }))} />
          </ZHField>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.plansAdmin.field.price')}>
              <input
                className="zh-input"
                value={planForm.priceAmount}
                onChange={(e) => setPlanForm((s) => ({ ...s, priceAmount: e.target.value }))}
              />
            </ZHField>
            <ZHField label={t('superadmin.plansAdmin.field.currency')}>
              <input
                className="zh-input"
                value={planForm.currency}
                onChange={(e) => setPlanForm((s) => ({ ...s, currency: e.target.value }))}
                maxLength={8}
              />
            </ZHField>
          </ZHGridRow>
          <ZHGridRow cols={2}>
            <ZHField label={t('superadmin.plansAdmin.field.billing')}>
              <select
                className="zh-input"
                value={planForm.billingCycle}
                onChange={(e) => setPlanForm((s) => ({ ...s, billingCycle: e.target.value }))}
              >
                <option value="monthly">{t('superadmin.plansAdmin.cycle.monthly')}</option>
                <option value="quarterly">{t('superadmin.plansAdmin.cycle.quarterly')}</option>
                <option value="yearly">{t('superadmin.plansAdmin.cycle.yearly')}</option>
                <option value="one_time">{t('superadmin.plansAdmin.cycle.oneTime')}</option>
              </select>
            </ZHField>
            <ZHField label={t('superadmin.plansAdmin.field.sort')}>
              <input
                className="zh-input"
                value={planForm.sortOrder}
                onChange={(e) => setPlanForm((s) => ({ ...s, sortOrder: e.target.value }))}
              />
            </ZHField>
          </ZHGridRow>
          <ZHField label={t('superadmin.plansAdmin.field.billingRef')}>
            <input
              className="zh-input"
              value={planForm.externalBillingRef}
              onChange={(e) => setPlanForm((s) => ({ ...s, externalBillingRef: e.target.value }))}
              placeholder="price_… (Stripe u otro)"
            />
          </ZHField>
          <label className="sap-check">
            <input
              type="checkbox"
              checked={planForm.isActive}
              onChange={(e) => setPlanForm((s) => ({ ...s, isActive: e.target.checked }))}
            />
            {t('superadmin.plansAdmin.field.active')}
          </label>
          <label className="sap-check">
            <input
              type="checkbox"
              checked={planForm.isPubliclyVisible}
              onChange={(e) => setPlanForm((s) => ({ ...s, isPubliclyVisible: e.target.checked }))}
            />
            {t('superadmin.plansAdmin.field.public')}
          </label>
          {planModal === 'create' ? (
            <label className="sap-check">
              <input
                type="checkbox"
                checked={planForm.isRecommended}
                onChange={(e) => setPlanForm((s) => ({ ...s, isRecommended: e.target.checked }))}
              />
              {t('superadmin.plansAdmin.field.recommendedCreate')}
            </label>
          ) : (
            <label className="sap-check">
              <input
                type="checkbox"
                checked={planForm.isRecommended}
                onChange={(e) => setPlanForm((s) => ({ ...s, isRecommended: e.target.checked }))}
              />
              {t('superadmin.plansAdmin.field.recommendedEdit')}
            </label>
          )}
          <ZHInlineRowRight>
            <ZHBtn variant="ghost" type="button" onClick={() => setPlanModal('closed')}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" type="button" onClick={() => void savePlan()} disabled={busy}>
              {t('common.save')}
            </ZHBtn>
          </ZHInlineRowRight>
        </Modal>
      ) : null}

      {featuresModalPlanId ? (
        <Modal
          onClose={() => setFeaturesModalPlanId(null)}
          size="lg"
          header={
            <ZHModalHeader
              title={t('superadmin.plansAdmin.featuresModalTitle')}
              subtitle={t('superadmin.plansAdmin.featuresModalSubtitle')}
              onClose={() => setFeaturesModalPlanId(null)}
            />
          }
        >
          <div className="sap-featureTable">
            <div className="sap-featureTable-head">
              <span>{t('superadmin.plansAdmin.col.feature')}</span>
              <span>{t('superadmin.plansAdmin.col.included')}</span>
              <span>{t('superadmin.plansAdmin.col.limit')}</span>
            </div>
            {features.map((d) => {
              const r = featureRows[d.id] ?? { included: false, limit: '' };
              return (
                <div key={d.id} className="sap-featureTable-row">
                  <div>
                    <div className="mono">{d.code}</div>
                    <div className="subtle">{d.name}</div>
                  </div>
                  <div>
                    <input
                      type="checkbox"
                      checked={r.included}
                      onChange={(e) =>
                        setFeatureRows((m) => ({
                          ...m,
                          [d.id]: { ...r, included: e.target.checked },
                        }))
                      }
                    />
                  </div>
                  <div>
                    <input
                      className="zh-input sap-limitInput"
                      value={r.limit}
                      onChange={(e) =>
                        setFeatureRows((m) => ({
                          ...m,
                          [d.id]: { ...r, limit: e.target.value },
                        }))
                      }
                      disabled={!r.included}
                    />
                  </div>
                </div>
              );
            })}
          </div>
          <ZHInlineRowRight>
            <ZHBtn variant="ghost" type="button" onClick={() => setFeaturesModalPlanId(null)}>
              {t('common.cancel')}
            </ZHBtn>
            <ZHBtn variant="primary" type="button" onClick={() => void saveFeatures()} disabled={busy}>
              {t('common.save')}
            </ZHBtn>
          </ZHInlineRowRight>
        </Modal>
      ) : null}

      {deletePlanId ? (
        <ZHConfirmModal
          title={t('superadmin.plansAdmin.deleteTitle')}
          message={t('superadmin.plansAdmin.deleteMessage')}
          confirmLabel={t('common.delete')}
          loading={busy}
          onCancel={() => setDeletePlanId(null)}
          onConfirm={() => void handleDelete()}
        />
      ) : null}
    </div>
  );
}
