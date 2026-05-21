import { useEffect, useLayoutEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useI18n } from '../../../i18n/i18n';
import { useAuthStore } from '../../../store/authStore';
import { companyService, type CompanyItem, type SubscriberDetailDto } from '../api/companyService';
import {
  createCompanyWithAdminSchema,
  updateSubscriberCompanySchema,
  type CreateCompanyFormValues,
  type UpdateSubscriberCompanyFormValues,
} from '../../../schemas/saas/companySchema';
import { formatApiRequestError } from '../../lib/apiError';
import { useDeployment } from '../../../deployment/DeploymentContext';
import {
  clearCompaniesDetailSubscriberId,
  clearCompaniesSubscriptionSubscriberId,
  persistCompaniesDetailSubscriberId,
  readCompaniesDetailSubscriberId,
} from '../../../navigation/companiesSubscriberDetailNav';
import { useConfig } from '../../config';

export type CompanyTab = 'data' | 'globalParameters' | 'mainNavigation' | 'audit';
export const ELECTRONIC_BILLING_TRIAL_KEY = 'billing.electronic.trial_enabled';

const emptyCompanyForm = (): CreateCompanyFormValues => ({
  subscriberName: '',
  subscriberSlug: '',
  ruc: '',
  shortName: '',
  tradeName: '',
  dinardap: '',
  logoUrl: '',
  displayOrder: 0,
  priority: 0,
  adminFirstName: '',
  adminLastName: '',
  adminEmail: '',
  adminPassword: '',
  linkExistingAdmin: false,
  passwordResetMode: 1,
});

const emptyDetailCompanyForm = (): UpdateSubscriberCompanyFormValues => ({
  subscriberName: '',
  subscriberSlug: '',
  ruc: '',
  shortName: '',
  tradeName: '',
  dinardap: '',
  logoUrl: '',
  displayOrder: 0,
  priority: 0,
});

export function useCompaniesPage() {
  const { t } = useI18n();
  const { maxActiveSubscribers, maxIdentityUsers } = useDeployment();
  const [searchParams, setSearchParams] = useSearchParams();
  const user = useAuthStore((s) => s.user);
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const { getValue } = useConfig();

  const [items, setItems] = useState<CompanyItem[]>([]);
  const [listQuery, setListQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [tab, setTab] = useState<CompanyTab>('data');
  const [auditSubscriberId, setAuditSubscriberId] = useState<string | null>(null);
  const [auditRefreshKey, setAuditRefreshKey] = useState(0);
  const planFilter = (searchParams.get('plan') ?? '').trim().toLowerCase();

  const [detailSubscriberId, setDetailSubscriberId] = useState<string | null>(null);
  const [subscriberDetail, setSubscriberDetail] = useState<SubscriberDetailDto | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [detailSaving, setDetailSaving] = useState(false);
  const [detailSaveError, setDetailSaveError] = useState('');
  const [detailSaveOk, setDetailSaveOk] = useState(false);
  const [globalParamSaving, setGlobalParamSaving] = useState(false);
  const [globalParamError, setGlobalParamError] = useState('');
  const [globalParamOk, setGlobalParamOk] = useState(false);
  const [electronicBillingTrialEnabled, setElectronicBillingTrialEnabled] = useState(false);
  const [globalParamScopeResolved, setGlobalParamScopeResolved] = useState<'global' | 'module' | 'feature' | ''>('');
  const [globalConfigCount, setGlobalConfigCount] = useState(0);
  const stockNegativeEnabled = Boolean(getValue<boolean>('ventas.stock.permitir_negativo', 'ventas', undefined, false));

  const {
    register,
    handleSubmit,
    reset,
    watch,
    formState: { errors },
  } = useForm<CreateCompanyFormValues>({
    resolver: zodResolver(createCompanyWithAdminSchema),
    defaultValues: emptyCompanyForm(),
  });

  const linkExistingAdmin = watch('linkExistingAdmin');

  const {
    register: registerDetail,
    handleSubmit: handleSubmitDetail,
    reset: resetDetailForm,
    formState: { errors: detailFieldErrors },
  } = useForm<UpdateSubscriberCompanyFormValues>({
    resolver: zodResolver(updateSubscriberCompanySchema),
    defaultValues: emptyDetailCompanyForm(),
  });

  const filtered = useMemo(() => {
    let base = items;
    if (planFilter) {
      base = base.filter((x) => (x.planCode ?? '').trim().toLowerCase() === planFilter);
    }
    const query = listQuery.trim().toLowerCase();
    if (!query) return base;
    return base.filter((x) => `${x.name} ${x.slug} ${x.id}`.toLowerCase().includes(query));
  }, [listQuery, items, planFilter]);

  const refresh = async () => {
    setError('');
    setLoading(true);
    try {
      const nextItems = await companyService.list();
      setItems(Array.isArray(nextItems) ? nextItems : []);
    } catch {
      setError(t('companies.error.load'));
      setItems([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void Promise.resolve().then(refresh);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const clearSubscriberDetailView = () => {
    clearCompaniesDetailSubscriberId();
    clearCompaniesSubscriptionSubscriberId();
    setDetailSubscriberId(null);
    setSubscriberDetail(null);
    setDetailError('');
    setSearchParams(
      (prev) => {
        const next = new URLSearchParams(prev);
        next.delete('data');
        return next;
      },
      { replace: true },
    );
  };

  const selectSubscriberRow = (id: string) => {
    clearCompaniesSubscriptionSubscriberId();
    persistCompaniesDetailSubscriberId(id);
    setDetailSubscriberId(id);
    setAuditSubscriberId(id);
    setAuditRefreshKey((k) => k + 1);
    setTab('data');
  };

  useLayoutEffect(() => {
    const openNav = searchParams.get('mainNavigation');
    if (openNav === '1' || openNav === 'true') {
      setTab('mainNavigation');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('mainNavigation');
          return next;
        },
        { replace: true },
      );
      return;
    }
    const fromUrl = (searchParams.get('data') ?? '').trim();
    if (fromUrl) {
      clearCompaniesSubscriptionSubscriberId();
      persistCompaniesDetailSubscriberId(fromUrl);
      setDetailSubscriberId(fromUrl);
      setTab('data');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('data');
          return next;
        },
        { replace: true },
      );
      return;
    }
    const subFromUrl = (searchParams.get('subscription') ?? '').trim();
    if (subFromUrl) {
      clearCompaniesSubscriptionSubscriberId();
      persistCompaniesDetailSubscriberId(subFromUrl);
      setDetailSubscriberId(subFromUrl);
      setTab('data');
      setSearchParams(
        (prev) => {
          const next = new URLSearchParams(prev);
          next.delete('subscription');
          return next;
        },
        { replace: true },
      );
    }
    const stored = readCompaniesDetailSubscriberId();
    if (stored) {
      setDetailSubscriberId((prev) => prev ?? stored);
      setTab('data');
    }
  }, [searchParams, setSearchParams]);

  useEffect(() => {
    if (!detailSubscriberId) {
      setSubscriberDetail(null);
      setDetailError('');
      setDetailLoading(false);
      return;
    }
    let cancelled = false;
    setDetailLoading(true);
    setDetailError('');
    (async () => {
      try {
        const d = await companyService.getSubscriber(detailSubscriberId);
        if (!cancelled) setSubscriberDetail(d);
      } catch {
        if (!cancelled) {
          setSubscriberDetail(null);
          setDetailError(t('companies.error.detailLoad'));
        }
      } finally {
        if (!cancelled) setDetailLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [detailSubscriberId, t]);

  useEffect(() => {
    if (!subscriberDetail) return;
    resetDetailForm({
      subscriberName: subscriberDetail.name,
      subscriberSlug: subscriberDetail.slug,
      ruc: subscriberDetail.ruc ?? '',
      shortName: subscriberDetail.shortName ?? '',
      tradeName: subscriberDetail.tradeName ?? '',
      dinardap: subscriberDetail.dinardap ?? '',
      logoUrl: subscriberDetail.logoUrl ?? '',
      displayOrder: subscriberDetail.displayOrder,
      priority: subscriberDetail.priority,
    });
    setDetailSaveError('');
    setDetailSaveOk(false);
    setGlobalParamError('');
    setGlobalParamOk(false);
    setGlobalParamScopeResolved('');
    setGlobalConfigCount(0);
  }, [subscriberDetail, resetDetailForm]);

  useEffect(() => {
    if (!detailSubscriberId || !subscriberDetail) return;
    let cancelled = false;
    (async () => {
      try {
        const [resolved, globals] = await Promise.all([
          companyService.resolveSubscriberConfig(detailSubscriberId, ELECTRONIC_BILLING_TRIAL_KEY),
          companyService.listSubscriberGlobalConfig(detailSubscriberId),
        ]);
        if (cancelled) return;
        const resolvedValue = resolved?.value?.trim().toLowerCase();
        if (resolvedValue === 'true' || resolvedValue === 'false') {
          setElectronicBillingTrialEnabled(resolvedValue === 'true');
          setGlobalParamScopeResolved(
            resolved?.scopeResolved === 'feature' || resolved?.scopeResolved === 'module' || resolved?.scopeResolved === 'global'
              ? resolved.scopeResolved
              : '',
          );
        } else {
          setElectronicBillingTrialEnabled(subscriberDetail.electronicBillingTrialEnabled);
          setGlobalParamScopeResolved('');
        }
        setGlobalConfigCount(Array.isArray(globals) ? globals.length : 0);
      } catch {
        if (!cancelled) {
          setElectronicBillingTrialEnabled(subscriberDetail.electronicBillingTrialEnabled);
          setGlobalParamScopeResolved('');
          setGlobalConfigCount(0);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [detailSubscriberId, subscriberDetail]);

  const submit = handleSubmit(async (form) => {
    setError('');
    setCreating(true);
    try {
      const session = await companyService.create({
        subscriberName: form.subscriberName,
        subscriberSlug: form.subscriberSlug,
        ruc: form.ruc?.trim() || null,
        shortName: form.shortName?.trim() || null,
        tradeName: form.tradeName?.trim() || null,
        dinardap: form.dinardap?.trim() || null,
        logoUrl: form.logoUrl?.trim() || null,
        displayOrder: form.displayOrder,
        priority: form.priority,
        adminFirstName: form.linkExistingAdmin ? '' : form.adminFirstName,
        adminLastName: form.linkExistingAdmin ? '' : form.adminLastName,
        adminEmail: form.adminEmail,
        adminPassword: form.linkExistingAdmin ? '' : form.adminPassword,
        linkExistingAdmin: form.linkExistingAdmin,
        passwordResetMode: 1,
      });
      if (session?.subscriberId) {
        setAuditSubscriberId(session.subscriberId);
        setAuditRefreshKey((k) => k + 1);
      }
      reset(emptyCompanyForm());
      await refresh();
      setTab('data');
      if (session?.subscriberId) {
        persistCompaniesDetailSubscriberId(session.subscriberId);
        setDetailSubscriberId(session.subscriberId);
        setAuditSubscriberId(session.subscriberId);
        setAuditRefreshKey((k) => k + 1);
      }
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('companies.error.create');
      setError(msg);
    } finally {
      setCreating(false);
    }
  });

  const saveTenantCompanyDetail = handleSubmitDetail(async (values) => {
    if (!detailSubscriberId) return;
    setDetailSaving(true);
    setDetailSaveError('');
    setDetailSaveOk(false);
    try {
      const slug = (values.subscriberSlug.trim() || values.subscriberName.trim()).toLowerCase();
      const updated = await companyService.updateSubscriberCompany(detailSubscriberId, {
        name: values.subscriberName.trim(),
        slug,
        ruc: values.ruc?.trim() || null,
        shortName: values.shortName?.trim() || null,
        tradeName: values.tradeName?.trim() || null,
        dinardap: values.dinardap?.trim() || null,
        logoUrl: values.logoUrl?.trim() || null,
        displayOrder: values.displayOrder,
        priority: values.priority,
      });
      setSubscriberDetail(updated);
      await refresh();
      setDetailSaveOk(true);
      window.setTimeout(() => setDetailSaveOk(false), 5000);
    } catch (e) {
      setDetailSaveError(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('companies.error.updateCompany'),
        }),
      );
    } finally {
      setDetailSaving(false);
    }
  });

  const saveGlobalParameters = async () => {
    if (!detailSubscriberId || !subscriberDetail) return;
    setGlobalParamSaving(true);
    setGlobalParamError('');
    setGlobalParamOk(false);
    try {
      await companyService.upsertSubscriberGlobalConfig(detailSubscriberId, {
        key: ELECTRONIC_BILLING_TRIAL_KEY,
        value: electronicBillingTrialEnabled ? 'true' : 'false',
        dataType: 'bool',
      });
      setGlobalParamScopeResolved('global');
      const globals = await companyService.listSubscriberGlobalConfig(detailSubscriberId);
      setGlobalConfigCount(Array.isArray(globals) ? globals.length : 0);
      await refresh();
      setGlobalParamOk(true);
      window.setTimeout(() => setGlobalParamOk(false), 5000);
    } catch (e) {
      setGlobalParamError(
        formatApiRequestError(e, {
          offline: t('common.apiUnreachable'),
          generic: t('companies.globalParams.saveError'),
        }),
      );
    } finally {
      setGlobalParamSaving(false);
    }
  };

  return {
    t,
    user,
    subscriberId,
    maxActiveSubscribers,
    maxIdentityUsers,
    items,
    listQuery,
    setListQuery,
    loading,
    error,
    creating,
    tab,
    setTab,
    auditSubscriberId,
    auditRefreshKey,
    detailSubscriberId,
    subscriberDetail,
    detailLoading,
    detailError,
    detailSaving,
    detailSaveError,
    detailSaveOk,
    globalParamSaving,
    globalParamError,
    globalParamOk,
    electronicBillingTrialEnabled,
    setElectronicBillingTrialEnabled,
    globalParamScopeResolved,
    globalConfigCount,
    stockNegativeEnabled,
    filtered,
    linkExistingAdmin,
    register,
    errors,
    registerDetail,
    detailFieldErrors,
    refresh,
    clearSubscriberDetailView,
    selectSubscriberRow,
    submit,
    saveTenantCompanyDetail,
    saveGlobalParameters,
  };
}
