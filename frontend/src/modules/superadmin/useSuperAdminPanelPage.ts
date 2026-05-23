import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { SUBSCRIBER_MODULE_KEYS } from '../../constants/subscriptionModules';
import { superAdminService, type CreateSubscriberWithAdminBody, type SuperAdminPlan, type SuperAdminSubscriber } from './api/superAdminService';
import { useAuthStore } from '../../store/authStore';
import { usePermissionsStore } from '../../store/permissionsStore';
import { useSuperAdminGate } from '../../hooks/useSuperAdminGate';
import { useI18n } from '../../i18n/i18n';
import { formatApiRequestError } from '../lib/apiError';
import { goToCompaniesSubscriberDetail } from '../../navigation/companiesSubscriberDetailNav';
import type { SessionResponse } from '../../types/access';
import {
  defaultModuleChecksAllOn,
  normalizeEnabledModulesForApi,
  slugifySubscriberName,
  storeImpersonationSubscriberName,
  type SuperAdminHomeTab,
} from './superAdminPanelUtils';

export type UseSuperAdminPanelPageParams = {
  embeddedTab?: SuperAdminHomeTab;
  shellLayout?: boolean;
};

export function useSuperAdminPanelPage({ embeddedTab, shellLayout }: UseSuperAdminPanelPageParams = {}) {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const { login } = useAuthStore();
  const clearPermissions = usePermissionsStore((s) => s.clearPermissions);
  const { isSuperAdmin, hasSelectedSubscriber } = useSuperAdminGate();
  const { t } = useI18n();

  const tabParam = searchParams.get('tab');
  const homeTab: SuperAdminHomeTab = embeddedTab
    ? embeddedTab
    : tabParam === 'companies' || tabParam === 'plans' || tabParam === 'menus'
      ? tabParam
      : 'overview';

  const selectHomeTab = useCallback(
    (tab: SuperAdminHomeTab) => {
      if (shellLayout) {
        if (tab === 'overview') void navigate('/superadmin/overview');
        else if (tab === 'companies') void navigate('/superadmin/subscribers');
        else if (tab === 'plans') void navigate('/superadmin/menu-plans?tab=plans');
        else if (tab === 'menus') void navigate('/superadmin/menu-plans?tab=menu');
        return;
      }
      if (tab === 'overview') setSearchParams({}, { replace: true });
      else setSearchParams({ tab }, { replace: true });
    },
    [navigate, setSearchParams, shellLayout],
  );

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [metrics, setMetrics] = useState<Awaited<ReturnType<typeof superAdminService.getMetrics>> | null>(null);
  const [subscribers, setSubscribers] = useState<SuperAdminSubscriber[]>([]);
  const [q, setQ] = useState('');
  const [switching, setSwitching] = useState<string | null>(null);
  const [plans, setPlans] = useState<SuperAdminPlan[]>([]);

  const [createSubscriberOpen, setCreateSubscriberOpen] = useState(false);
  const [createBusy, setCreateBusy] = useState(false);
  const [createError, setCreateError] = useState('');
  const [createForm, setCreateForm] = useState<CreateSubscriberWithAdminBody>({
    subscriberName: '',
    subscriberSlug: '',
    ruc: '',
    countryCode: 'ECU',
    timezone: 'America/Guayaquil',
    adminFirstName: '',
    adminLastName: '',
    adminEmail: '',
    adminPassword: '',
    passwordResetMode: 0,
    linkExistingAdmin: false,
  });
  const [createPlanCode, setCreatePlanCode] = useState('');
  const [createRestrictModules, setCreateRestrictModules] = useState(false);
  const [createModuleChecks, setCreateModuleChecks] = useState<Record<string, boolean>>(defaultModuleChecksAllOn);

  const [subModalOpen, setSubModalOpen] = useState(false);
  const [subModalSubscriber, setSubModalSubscriber] = useState<SuperAdminSubscriber | null>(null);
  const [subPlanCode, setSubPlanCode] = useState('');
  const [subRestrict, setSubRestrict] = useState(false);
  const [subModuleChecks, setSubModuleChecks] = useState<Record<string, boolean>>(defaultModuleChecksAllOn);
  const [subBusy, setSubBusy] = useState(false);
  const [subError, setSubError] = useState('');

  const refreshSubscribersAndMetrics = useCallback(async () => {
    const [m, tns] = await Promise.all([
      superAdminService.getMetrics(),
      superAdminService.getSubscribers(),
    ]);
    setMetrics(m);
    setSubscribers(tns);
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        setLoading(true);
        setError('');
        await refreshSubscribersAndMetrics();
        if (cancelled) return;
      } catch (e) {
        if (cancelled) return;
        setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [refreshSubscribersAndMetrics, t]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const list = await superAdminService.getPlansCatalog();
        if (!cancelled) setPlans(list);
      } catch {
        if (!cancelled) setPlans([]);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const filtered = useMemo(() => {
    const query = q.trim().toLowerCase();
    if (!query) return subscribers;
    return subscribers.filter(
      (subscriber) =>
        subscriber.name.toLowerCase().includes(query) ||
        subscriber.slug.toLowerCase().includes(query) ||
        subscriber.id.toLowerCase().includes(query),
    );
  }, [subscribers, q]);

  const planLabelForSubscriber = useMemo(() => {
    const map = new Map<string, string>();
    for (const p of plans) {
      map.set(p.code.trim().toLowerCase(), p.name.trim() ? `${p.name} (${p.code})` : p.code);
    }
    return (planCode: string | null | undefined) => {
      const c = (planCode ?? '').trim();
      if (!c) return '';
      return map.get(c.toLowerCase()) ?? c;
    };
  }, [plans]);

  const activePlans = useMemo(
    () => [...plans].filter((p) => p.isActive).sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0)),
    [plans],
  );

  const moduleLabel = useCallback((key: string) => t(`companies.subscription.module.${key}`, key), [t]);

  const handleSwitch = async (subscriber: SuperAdminSubscriber) => {
    if (!subscriber.isActive) return;
    setSwitching(subscriber.id);
    setError('');
    try {
      const auth = await superAdminService.switchSubscriber(subscriber.id);
      storeImpersonationSubscriberName(subscriber.name);
      clearPermissions();
      login(auth);
      navigate('/saas/overview');
    } catch (e) {
      setError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSwitching(null);
    }
  };

  const openCreateSubscriber = () => {
    setCreateError('');
    setCreatePlanCode('');
    setCreateRestrictModules(false);
    setCreateModuleChecks(defaultModuleChecksAllOn());
    setCreateForm({
      subscriberName: '',
      subscriberSlug: '',
      adminFirstName: '',
      adminLastName: '',
      adminEmail: '',
      adminPassword: '',
      passwordResetMode: 0,
      linkExistingAdmin: false,
    });
    setCreateSubscriberOpen(true);
  };

  const openSubscriptionModal = (subscriber: SuperAdminSubscriber) => {
    setSubModalSubscriber(subscriber);
    setSubPlanCode((subscriber.planCode ?? '').trim());
    const restricted = !!subscriber.hasModuleRestrictions;
    setSubRestrict(restricted);
    const checks: Record<string, boolean> = {};
    for (const k of SUBSCRIBER_MODULE_KEYS) {
      if (!restricted) {
        checks[k] = true;
      } else {
        checks[k] = (subscriber.enabledModules ?? []).some((em: string) => em.toLowerCase() === k.toLowerCase());
      }
    }
    setSubModuleChecks(checks);
    setSubError('');
    setSubModalOpen(true);
  };

  const saveCreateSubscriber = async () => {
    setCreateBusy(true);
    setCreateError('');
    try {
      const subscriberName = createForm.subscriberName.trim();
      const subscriberSlug = (createForm.subscriberSlug.trim() || slugifySubscriberName(subscriberName)).toLowerCase();
      const adminEmail = createForm.adminEmail.trim().toLowerCase();

      if (activePlans.length === 0) {
        setCreateError(t('superadmin.createSubscriber.error.noPlans'));
        return;
      }
      if (!createPlanCode.trim()) {
        setCreateError(t('superadmin.createSubscriber.error.planRequired'));
        return;
      }

      if (createRestrictModules) {
        const n = SUBSCRIBER_MODULE_KEYS.filter((k) => createModuleChecks[k]).length;
        if (n === 0) {
          setCreateError(t('superadmin.createSubscriber.error.noModulesSelected'));
          return;
        }
      }

      const planCodeNorm = createPlanCode.trim();
      const enabledModules = normalizeEnabledModulesForApi(createRestrictModules, createModuleChecks);

      const session: SessionResponse = await superAdminService.createSubscriberWithAdmin({
        ...createForm,
        subscriberName,
        subscriberSlug,
        adminEmail,
        ruc: createForm.ruc?.trim() || null,
        countryCode: createForm.countryCode?.trim() || 'ECU',
        timezone: createForm.timezone?.trim() || 'America/Guayaquil',
        adminPassword: createForm.linkExistingAdmin ? '' : createForm.adminPassword,
        planCode: planCodeNorm,
        enabledModules: enabledModules === undefined ? null : enabledModules,
      });

      setError('');
      setCreateSubscriberOpen(false);
      goToCompaniesSubscriberDetail(navigate, session.subscriberId);
    } catch (e) {
      setCreateError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setCreateBusy(false);
    }
  };

  const saveSubscriptionModal = async () => {
    if (!subModalSubscriber) return;
    setSubBusy(true);
    setSubError('');
    try {
      if (subRestrict) {
        const n = SUBSCRIBER_MODULE_KEYS.filter((k) => subModuleChecks[k]).length;
        if (n === 0) {
          setSubError(t('superadmin.changeSubscription.error.noModulesSelected'));
          return;
        }
      }
      const planCode = subPlanCode.trim() || null;
      const enabledModulesNorm = normalizeEnabledModulesForApi(subRestrict, subModuleChecks);

      await superAdminService.updateSubscriberSubscription(subModalSubscriber.id, {
        planCode,
        enabledModules: enabledModulesNorm === undefined ? null : enabledModulesNorm,
      });
      await refreshSubscribersAndMetrics();
      setSubModalOpen(false);
      setSubModalSubscriber(null);
    } catch (e) {
      setSubError(formatApiRequestError(e, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSubBusy(false);
    }
  };

  return {
    t,
    navigate,
    isSuperAdmin,
    hasSelectedSubscriber,
    homeTab,
    selectHomeTab,
    shellLayout,
    loading,
    error,
    metrics,
    subscribers,
    q,
    setQ,
    switching,
    activePlans,
    filtered,
    planLabelForSubscriber,
    moduleLabel,
    handleSwitch,
    openCreateSubscriber,
    openSubscriptionModal,
    createSubscriberOpen,
    setCreateSubscriberOpen,
    createBusy,
    createError,
    createForm,
    setCreateForm,
    createPlanCode,
    setCreatePlanCode,
    createRestrictModules,
    setCreateRestrictModules,
    createModuleChecks,
    setCreateModuleChecks,
    saveCreateSubscriber,
    subModalOpen,
    setSubModalOpen,
    subModalSubscriber,
    subPlanCode,
    setSubPlanCode,
    subRestrict,
    setSubRestrict,
    subModuleChecks,
    setSubModuleChecks,
    subBusy,
    subError,
    saveSubscriptionModal,
    slugify: slugifySubscriberName,
  };
}

export type SuperAdminPanelPageState = ReturnType<typeof useSuperAdminPanelPage>;
