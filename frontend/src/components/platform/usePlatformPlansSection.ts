import { useCallback, useEffect, useMemo, useState } from 'react';
import { useI18n } from '../../i18n/i18n';
import { usePlatformGate } from '../../hooks/usePlatformGate';
import {
  platformService,
  type CommercialPlanAdmin,
  type CreateCommercialPlanBody,
  type SaasPublicPlan,
  type PlatformOverviewMetrics,
  type PlatformSubscriber,
  type UpdateCommercialPlanBody,
} from '../../modules/platform/api/platformService';
import { formatApiRequestError } from '../../modules/lib/apiError';
import {
  csvEscape,
  emptyPlanForm,
  monthlyEquivalentPrice,
  planToForm,
  POLL_MS,
  type PlanFormState,
} from './platformPlansSectionUtils';

export function usePlatformPlansSection() {
  const { t } = useI18n();
  const { isPlatformOperator } = usePlatformGate();

  const [plans, setPlans] = useState<CommercialPlanAdmin[]>([]);
  const [publicPlans, setPublicPlans] = useState<SaasPublicPlan[]>([]);
  const [subscribers, setSubscribers] = useState<PlatformSubscriber[]>([]);
  const [metrics, setMetrics] = useState<PlatformOverviewMetrics | null>(null);
  const [subscriberSearch, setSubscriberSearch] = useState('');
  const [subscriberPlanFilter, setSubscriberPlanFilter] = useState('');
  const [subscriberStatusFilter, setSubscriberStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const [planModal, setPlanModal] = useState<'closed' | 'create' | 'edit'>('closed');
  const [editingPlanId, setEditingPlanId] = useState<string | null>(null);
  const [planForm, setPlanForm] = useState<PlanFormState>(emptyPlanForm);

  const [deletePlanId, setDeletePlanId] = useState<string | null>(null);

  const loadAll = useCallback(async () => {
    const [p, pub, tns, met] = await Promise.all([
      platformService.listCommercialPlansAdmin(),
      platformService.getPublicPlans().catch(() => [] as SaasPublicPlan[]),
      platformService.getSubscribers().catch(() => [] as PlatformSubscriber[]),
      platformService.getMetrics().catch(() => null),
    ]);
    const sorted = [...p].sort((a, b) => a.sortOrder - b.sortOrder || a.code.localeCompare(b.code));
    setPlans(sorted);
    setPublicPlans(pub);
    setSubscribers(Array.isArray(tns) ? tns : []);
    setMetrics(met && typeof met === 'object' && 'totals' in met ? (met as PlatformOverviewMetrics) : null);
  }, []);

  const planSubscriberStats = useMemo(() => {
    const counts = new Map<string, number>();
    for (const tn of subscribers) {
      const c = (tn.planCode ?? '').trim().toLowerCase();
      if (!c) continue;
      counts.set(c, (counts.get(c) ?? 0) + 1);
    }
    let max = 0;
    for (const v of counts.values()) max = Math.max(max, v);
    return { counts, max };
  }, [subscribers]);

  const planByCode = useMemo(() => {
    const m = new Map<string, CommercialPlanAdmin>();
    for (const p of plans) m.set(p.code.trim().toLowerCase(), p);
    return m;
  }, [plans]);

  const approxMrr = useMemo(() => {
    let sum = 0;
    for (const tn of subscribers) {
      if (!tn.isActive) continue;
      const pc = (tn.planCode ?? '').trim().toLowerCase();
      if (!pc) continue;
      const p = planByCode.get(pc);
      if (!p?.isActive) continue;
      sum += monthlyEquivalentPrice(p);
    }
    return sum;
  }, [subscribers, planByCode]);

  const filteredTenantsForTable = useMemo(() => {
    let r = subscribers;
    const pf = subscriberPlanFilter.trim().toLowerCase();
    if (pf) r = r.filter((tn) => (tn.planCode ?? '').trim().toLowerCase() === pf);
    if (subscriberStatusFilter === 'active') r = r.filter((tn) => tn.isActive);
    if (subscriberStatusFilter === 'inactive') r = r.filter((tn) => !tn.isActive);
    const q = subscriberSearch.trim().toLowerCase();
    if (q) r = r.filter((tn) => `${tn.name} ${tn.slug} ${tn.id}`.toLowerCase().includes(q));
    return r;
  }, [subscribers, subscriberPlanFilter, subscriberSearch, subscriberStatusFilter]);

  const exportTenantsCsv = useCallback(() => {
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
    a.download = `saas-subscribers-${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  }, [filteredTenantsForTable, planByCode]);

  useEffect(() => {
    if (!isPlatformOperator) return;
    let cancelled = false;
    void (async () => {
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
    return () => {
      cancelled = true;
    };
  }, [isPlatformOperator, loadAll, t]);

  useEffect(() => {
    if (!isPlatformOperator) return;
    const tick = () => {
      if (document.visibilityState !== 'visible') return;
      void loadAll().catch(() => undefined);
    };
    const id = window.setInterval(tick, POLL_MS);
    const onVis = () => {
      if (document.visibilityState === 'visible') void loadAll().catch(() => undefined);
    };
    document.addEventListener('visibilitychange', onVis);
    return () => {
      window.clearInterval(id);
      document.removeEventListener('visibilitychange', onVis);
    };
  }, [isPlatformOperator, loadAll]);

  const openCreatePlan = useCallback(() => {
    setPlanForm(emptyPlanForm());
    setEditingPlanId(null);
    setPlanModal('create');
  }, []);

  const openEditPlan = useCallback((p: CommercialPlanAdmin) => {
    setPlanForm(planToForm(p));
    setEditingPlanId(p.id);
    setPlanModal('edit');
  }, []);

  const closePlanModal = useCallback(() => setPlanModal('closed'), []);

  const savePlan = useCallback(async () => {
    if (!editingPlanId && planModal !== 'create') return;
    setBusy(true);
    setError('');
    try {
      const price = Number(planForm.priceAmount.replace(',', '.'));
      const sort = Number.parseInt(planForm.sortOrder, 10) || 0;
      const ext = planForm.externalBillingRef.trim() || null;
      const shortLabel = planForm.shortLabel.trim() || null;

      if (planModal === 'create') {
        const body: CreateCommercialPlanBody = {
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
        await platformService.createCommercialPlan(body);
      } else if (editingPlanId) {
        const body: UpdateCommercialPlanBody = {
          name: planForm.name.trim(),
          shortLabel,
          isActive: planForm.isActive,
          priceAmount: Number.isFinite(price) ? price : 0,
          currency: planForm.currency.trim().toUpperCase() || 'USD',
          billingCycle: planForm.billingCycle.trim().toLowerCase() || 'monthly',
          isPubliclyVisible: planForm.isPubliclyVisible,
          externalBillingRef: ext,
        };
        await platformService.updateCommercialPlan(editingPlanId, body);
        if (planForm.isRecommended) {
          await platformService.setCommercialPlanRecommended(editingPlanId);
        }
      }
      setPlanModal('closed');
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  }, [editingPlanId, loadAll, planForm, planModal, t]);

  const handleDelete = useCallback(async () => {
    if (!deletePlanId) return;
    setBusy(true);
    setError('');
    try {
      await platformService.deleteCommercialPlan(deletePlanId);
      setDeletePlanId(null);
      await loadAll();
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setBusy(false);
    }
  }, [deletePlanId, loadAll, t]);

  const movePlan = useCallback(
    async (index: number, dir: -1 | 1) => {
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
        await platformService.reorderCommercialPlans(next.map((p) => p.id));
        await loadAll();
      } catch (e) {
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
        await loadAll();
      } finally {
        setBusy(false);
      }
    },
    [loadAll, plans, t],
  );

  const setRecommendedOnly = useCallback(
    async (planId: string) => {
      setBusy(true);
      setError('');
      try {
        await platformService.setCommercialPlanRecommended(planId);
        await loadAll();
      } catch (e) {
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      } finally {
        setBusy(false);
      }
    },
    [loadAll, t],
  );

  const activePlansCount = plans.filter((p) => p.isActive).length;
  const activePlanNames = plans
    .filter((p) => p.isActive)
    .map((p) => p.shortLabel ?? p.name)
    .join(', ');
  const totals = metrics?.totals;
  const inactiveSubscribers = totals ? Math.max(0, totals.totalSubscribers - totals.activeSubscribers) : 0;
  const inactivePct =
    totals && totals.totalSubscribers > 0 ? Math.round((inactiveSubscribers / totals.totalSubscribers) * 100) : 0;

  const defaultCurrency = plans.find((p) => p.isActive)?.currency ?? 'USD';

  return {
    t,
    plans,
    publicPlans,
    subscribers,
    loading,
    error,
    busy,
    planModal,
    planForm,
    setPlanForm,
    deletePlanId,
    setDeletePlanId,
    subscriberSearch,
    setSubscriberSearch,
    subscriberPlanFilter,
    setSubscriberPlanFilter,
    subscriberStatusFilter,
    setSubscriberStatusFilter,
    planSubscriberStats,
    planByCode,
    approxMrr,
    filteredTenantsForTable,
    exportTenantsCsv,
    loadAll,
    openCreatePlan,
    openEditPlan,
    closePlanModal,
    savePlan,
    handleDelete,
    movePlan,
    setRecommendedOnly,
    activePlansCount,
    activePlanNames,
    totals,
    inactiveSubscribers,
    inactivePct,
    defaultCurrency,
  };
}
