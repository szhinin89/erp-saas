import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  updateSubscriberCompanySchema,
  type UpdateSubscriberCompanyFormValues,
} from '../../../../schemas/saas/companySchema';
import { formatApiRequestError } from '../../../lib/apiError';
import { platformService, type PlatformSubscriber, type SubscriberEntitlementsSnapshot } from '../../api/platformService';
import { subscriberService, type SubscriberDetailDto } from '../../api/subscriberService';

export const ELECTRONIC_BILLING_TRIAL_KEY = 'billing.electronic.trial_enabled';

const emptyDetailForm = (): UpdateSubscriberCompanyFormValues => ({
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

export function useSubscriberDetailPage(subscriberId: string | undefined) {
  const [subscriber, setSubscriber] = useState<PlatformSubscriber | null>(null);
  const [detail, setDetail] = useState<SubscriberDetailDto | null>(null);
  const [entitlements, setEntitlements] = useState<SubscriberEntitlementsSnapshot | null>(null);
  const [tenantUsers, setTenantUsers] = useState<Array<{ id: string; email: string; firstName: string; lastName: string; isActive: boolean; userType: string }>>([]);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [saveOk, setSaveOk] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [electronicBillingTrialEnabled, setElectronicBillingTrialEnabled] = useState(false);
  const [globalConfigCount, setGlobalConfigCount] = useState(0);
  const [menuFlags, setMenuFlags] = useState<{ hasCustomMenu: boolean; usedPlanMenu: boolean } | null>(null);

  const form = useForm<UpdateSubscriberCompanyFormValues>({
    resolver: zodResolver(updateSubscriberCompanySchema),
    defaultValues: emptyDetailForm(),
  });

  const reloadSummary = useCallback(async () => {
    if (!subscriberId) return;
    const list = await platformService.getSubscribers();
    const found = list.find((s) => s.id === subscriberId) ?? null;
    setSubscriber(found);
  }, [subscriberId]);

  const reloadDetail = useCallback(async () => {
    if (!subscriberId) return;
    setDetailLoading(true);
    try {
      const d = await subscriberService.getSubscriber(subscriberId);
      setDetail(d);
      form.reset({
        subscriberName: d.name,
        subscriberSlug: d.slug,
        ruc: d.ruc ?? '',
        shortName: d.shortName ?? '',
        tradeName: d.tradeName ?? '',
        dinardap: d.dinardap ?? '',
        logoUrl: d.logoUrl ?? '',
        displayOrder: d.displayOrder,
        priority: d.priority,
      });
      const [resolved, globals, menu] = await Promise.all([
        subscriberService.resolveSubscriberConfig(subscriberId, ELECTRONIC_BILLING_TRIAL_KEY),
        subscriberService.listSubscriberGlobalConfig(subscriberId),
        platformService.getSubscriberResolvedMenu(subscriberId),
      ]);
      const rv = resolved?.value?.trim().toLowerCase();
      setElectronicBillingTrialEnabled(rv === 'true' || rv === 'false' ? rv === 'true' : d.electronicBillingTrialEnabled);
      setGlobalConfigCount(globals.length);
      setMenuFlags({ hasCustomMenu: menu.hasCustomMenu, usedPlanMenu: menu.usedPlanMenu });
    } catch {
      setError('No se pudo cargar el detalle del suscriptor.');
    } finally {
      setDetailLoading(false);
    }
  }, [subscriberId, form]);

  useEffect(() => {
    if (!subscriberId) return;
    setLoading(true);
    setError(null);
    Promise.all([reloadSummary(), reloadDetail()])
      .catch(() => setError('No se pudo cargar el suscriptor.'))
      .finally(() => setLoading(false));
  }, [subscriberId, reloadSummary, reloadDetail]);

  const loadEntitlements = useCallback(async () => {
    if (!subscriberId) return;
    const snap = await platformService.getSubscriberEntitlements(subscriberId);
    setEntitlements(snap ?? null);
  }, [subscriberId]);

  const loadTenantUsers = useCallback(async () => {
    if (!subscriberId) return;
    const rows = await platformService.getSubscriberTenantUsers(subscriberId);
    setTenantUsers(rows);
  }, [subscriberId]);

  const saveCompanyProfile = form.handleSubmit(async (values) => {
    if (!subscriberId) return;
    setSaving(true);
    setSaveOk(false);
    setSaveError(null);
    try {
      const updated = await subscriberService.updateSubscriberCompany(subscriberId, {
        name: values.subscriberName,
        slug: values.subscriberSlug,
        ruc: values.ruc?.trim() || null,
        shortName: values.shortName?.trim() || null,
        tradeName: values.tradeName?.trim() || null,
        dinardap: values.dinardap?.trim() || null,
        logoUrl: values.logoUrl?.trim() || null,
        displayOrder: values.displayOrder,
        priority: values.priority,
      });
      setDetail(updated);
      setSaveOk(true);
      await reloadSummary();
    } catch (e) {
      setSaveError(formatApiRequestError(e, { generic: 'Error al guardar.' }));
    } finally {
      setSaving(false);
    }
  });

  const saveGlobalParameters = async () => {
    if (!subscriberId) return;
    setSaving(true);
    setSaveOk(false);
    setSaveError(null);
    try {
      const updated = await subscriberService.updateSubscriberGlobalParameters(subscriberId, {
        electronicBillingTrialEnabled,
      });
      setDetail(updated);
      setSaveOk(true);
    } catch (e) {
      setSaveError(formatApiRequestError(e, { generic: 'Error al guardar parámetros.' }));
    } finally {
      setSaving(false);
    }
  };

  const resetCustomMenu = async () => {
    if (!subscriberId) return;
    await platformService.deleteSubscriberCustomMenu(subscriberId);
    await reloadDetail();
  };

  return {
    subscriber,
    detail,
    entitlements,
    tenantUsers,
    loading,
    detailLoading,
    error,
    saveOk,
    saveError,
    saving,
    electronicBillingTrialEnabled,
    setElectronicBillingTrialEnabled,
    globalConfigCount,
    menuFlags,
    form,
    reloadSummary,
    reloadDetail,
    loadEntitlements,
    loadTenantUsers,
    saveCompanyProfile,
    saveGlobalParameters,
    resetCustomMenu,
  };
}
