import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useAsync } from '../../../../hooks/useAsync';
import { sriService } from '../api/sriService';
import { formatApiError } from '../../../lib/formatApiError';
import {
  sriConfigSchema,
  SRI_WSDL_DEFAULTS,
  type SriConfigValues,
} from '../../../../schemas/configuracion/sriConfigSchema';

export const SRI_ENV_OPTIONS = [
  { value: 2, label: 'Pruebas (2)', description: 'Ambiente de certificación SRI — no genera comprobantes válidos' },
  { value: 1, label: 'Producción (1)', description: 'Ambiente real — los comprobantes tienen validez tributaria' },
] as const;

export function useSriConfigPage() {
  const hasPerm = usePermissionsStore((s) => s.has);
  const role = useAuthStore((s) => s.user?.role ?? '');

  const isAdmin = role === 'Admin' || role === 'SuperAdmin';
  const canView = isAdmin || hasPerm('settings.company.view');
  const canEdit = isAdmin || hasPerm('settings.company.edit');

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [showPass, setShowPass] = useState(false);

  const sriState = useAsync(() => sriService.get(), canView);

  const form = useForm<SriConfigValues>({
    resolver: zodResolver(sriConfigSchema),
    defaultValues: {
      ruc: '',
      legalName: '',
      tradeName: '',
      mainAddress: '',
      requiresAccounting: false,
      specialTaxpayer: '',
      estabCode: '001',
      emPointCode: '001',
      certP12Path: '',
      certPassword: '',
      environment: 2,
      emissionType: 1,
      wsdlUrl: SRI_WSDL_DEFAULTS.pruebas,
    },
  });

  const { handleSubmit, reset, watch, setValue, getValues, formState: { errors, isDirty } } = form;
  const envValue = watch('environment');

  const resetFromData = (d: NonNullable<typeof sriState.data>) => {
    reset({
      ruc: d.companyRuc ?? '',
      legalName: d.legalName ?? '',
      tradeName: d.tradeName ?? '',
      mainAddress: d.mainAddress ?? '',
      requiresAccounting: d.requiresAccounting ?? false,
      specialTaxpayer: d.specialTaxpayer ?? '',
      estabCode: d.estabCode ?? '001',
      emPointCode: d.emPointCode ?? '001',
      certP12Path: d.certificateP12Path ?? '',
      certPassword: '',
      environment: d.environment === 1 ? 1 : 2,
      emissionType: d.emissionType ?? 1,
      wsdlUrl: d.sriAuthorizationUrl ?? SRI_WSDL_DEFAULTS.pruebas,
    });
  };

  useEffect(() => {
    const d = sriState.data;
    if (!d) return;
    resetFromData(d);
  }, [sriState.data, reset]);

  useEffect(() => {
    const currentUrl = getValues('wsdlUrl');
    const isDefault = Object.values(SRI_WSDL_DEFAULTS).includes(currentUrl);
    if (isDefault || !currentUrl) {
      setValue('wsdlUrl', envValue === 1 ? SRI_WSDL_DEFAULTS.produccion : SRI_WSDL_DEFAULTS.pruebas, {
        shouldDirty: true,
      });
    }
  }, [envValue, getValues, setValue]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await sriService.upsert({
        ruc: values.ruc,
        legalName: values.legalName,
        tradeName: values.tradeName || null,
        mainAddress: values.mainAddress,
        requiresAccounting: values.requiresAccounting,
        specialTaxpayer: values.specialTaxpayer || null,
        estabCode: values.estabCode,
        emPointCode: values.emPointCode,
        certP12Path: values.certP12Path,
        certPassword: values.certPassword ?? '',
        environment: values.environment,
        emissionType: 1,
        wsdlUrl: values.wsdlUrl,
      });
      setSaved(true);
      sriState.refetch();
    } catch (err) {
      setSaveError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const d = sriState.data;
    if (d) resetFromData(d);
  };

  return {
    canView,
    canEdit,
    saving,
    saveError,
    saved,
    showPass,
    setShowPass,
    sriState,
    form,
    errors,
    isDirty,
    onSubmit,
    handleDiscard,
  };
}
