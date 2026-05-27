import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { useI18n } from '../../../../i18n/i18n';
import { useAsync } from '../../../../hooks/useAsync';
import { runtimeSubscriberService } from '../../../subscribers/api/runtimeSubscriberService';
import { companyManagementService } from '../../../company-management/api/companyManagementService';
import { catalogService, type CatalogItem } from '../../../catalog/api/catalogService';
import { formatApiError } from '../../../lib/formatApiError';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import { useAuthStore } from '../../../../store/authStore';
import {
  companyConfigSchema,
  defaultCompanyConfigValues,
  type CompanyConfigValues,
} from '../schemas/companyConfigSchema';

const LANGUAGES = [
  { value: 'es', label: 'Español (Ecuador)' },
  { value: 'en', label: 'English (US)' },
  { value: 'qu', label: 'Kichwa (Cañar)' },
];

const TIMEZONES = [
  { value: 'America/Guayaquil', label: '(GMT-05:00) Guayaquil / Ecuador' },
  { value: 'America/Bogota',    label: '(GMT-05:00) Bogotá / Colombia' },
  { value: 'America/Lima',      label: '(GMT-05:00) Lima / Perú' },
  { value: 'America/New_York',  label: '(GMT-05:00) Eastern Time' },
];

const CURRENCIES = [
  { value: 'USD', label: 'USD — Dólar Estadounidense' },
  { value: 'EUR', label: 'EUR — Euro' },
];

function taxTypeBadge(type: string): React.ReactElement {
  const upper = type.toUpperCase();
  if (upper === 'VAT')    return <span className="badge badge--green">IVA</span>;
  if (upper === 'EXCISE') return <span className="badge badge--red">ICE</span>;
  return <span className="badge badge--gray">{upper}</span>;
}

export function CompanyProfileSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const canView = canShow('configuracion.empresa.view');
  const canEdit = canShow('configuracion.empresa.edit');

  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved,     setSaved]     = useState(false);

  const subscriberState = useAsync(
    () => subscriberId ? runtimeSubscriberService.getSubscriber(subscriberId) : Promise.resolve(null),
    !!subscriberId,
  );

  const companyState = useAsync(() => companyManagementService.getCurrent());
  const taxState = useAsync(() => catalogService.taxRates(false));

  const { register, handleSubmit, reset, formState: { errors, isDirty } } = useForm<CompanyConfigValues>({
    resolver: zodResolver(companyConfigSchema),
    defaultValues: defaultCompanyConfigValues,
  });

  useEffect(() => {
    const sub = subscriberState.data;
    const co  = companyState.data;
    if (!sub || !co) return;
    reset({
      subscriberName:    sub.name ?? '',
      preferredLanguage: sub.preferredLanguage ?? 'es',
      legalName:         co.legalName ?? '',
      tradeName:         co.tradeName ?? '',
      taxId:             co.taxId ?? '',
      mainAddress:       co.mainAddress ?? '',
      currency:          co.currencyCode ?? 'USD',
      timezone:          co.timezone ?? 'America/Guayaquil',
    });
  }, [subscriberState.data, companyState.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !subscriberId) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const sub = subscriberState.data;
      const co  = companyState.data;

      await runtimeSubscriberService.updateSubscriberCompany(subscriberId, {
        name:             values.subscriberName,
        slug:             sub?.slug ?? '',
        displayOrder:     sub?.displayOrder ?? 0,
        priority:         sub?.priority ?? 0,
        preferredLanguage: values.preferredLanguage,
      });

      if (co) {
        await companyManagementService.update(co.id, {
          taxId:       values.taxId ?? co.taxId,
          legalName:   values.legalName,
          mainAddress: values.mainAddress ?? co.mainAddress,
          tradeName:   values.tradeName || null,
          phone:       co.phone,
          email:       co.email,
          countryCode: co.countryCode,
          timezone:    values.timezone,
          currencyCode: values.currency,
          logoUrl:     co.logoUrl,
          brandingJson: co.brandingJson,
          id:          co.id,
          isActive:    co.isActive,
        });
      }

      setSaved(true);
      subscriberState.refetch();
      companyState.refetch();
    } catch (err) {
      setSaveError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const sub = subscriberState.data;
    const co  = companyState.data;
    if (sub && co) {
      reset({
        subscriberName:    sub.name ?? '',
        preferredLanguage: sub.preferredLanguage ?? 'es',
        legalName:         co.legalName ?? '',
        tradeName:         co.tradeName ?? '',
        taxId:             co.taxId ?? '',
        mainAddress:       co.mainAddress ?? '',
        currency:          co.currencyCode ?? 'USD',
        timezone:          co.timezone ?? 'America/Guayaquil',
      });
    }
  };

  if (!canView) return <NoAccessPage title={t('settings.company.title')} />;
  if (subscriberState.loading || companyState.loading) return <LoadingState />;

  return (
    <>
      {(subscriberState.error || companyState.error) && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={subscriberState.error ?? companyState.error} />
      )}
      {saveError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
      )}
      {saved && <ZHPageNotice variant="success" message={t('settings.company.saved')} />}

      <form onSubmit={onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">settings</span>
              <p className="pg-section-label">Cuenta SaaS</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Nombre de la Cuenta" required error={errors.subscriberName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre de la cuenta SaaS"
                  disabled={saving || !canEdit}
                  {...register('subscriberName')}
                />
              </ZHField>

              <ZHField label="Idioma del Sistema">
                <select className="zh-input" disabled={saving || !canEdit} {...register('preferredLanguage')}>
                  {LANGUAGES.map((l) => (
                    <option key={l.value} value={l.value}>{l.label}</option>
                  ))}
                </select>
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">business</span>
              <p className="pg-section-label">Datos de la Empresa</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Razón Social" required error={errors.legalName?.message}>
                <input
                  className="zh-input"
                  placeholder="Razón social registrada en el SRI"
                  disabled={saving || !canEdit}
                  {...register('legalName')}
                />
              </ZHField>

              <ZHField label="Nombre Comercial" error={errors.tradeName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre visible en documentos"
                  disabled={saving || !canEdit}
                  {...register('tradeName')}
                />
              </ZHField>

              <ZHField label="RUC" error={errors.taxId?.message}>
                <input
                  className="zh-input"
                  placeholder="13 dígitos"
                  disabled={saving || !canEdit}
                  {...register('taxId')}
                />
              </ZHField>

              <ZHField label="Dirección Matriz" error={errors.mainAddress?.message}>
                <input
                  className="zh-input"
                  placeholder="Dirección registrada"
                  disabled={saving || !canEdit}
                  {...register('mainAddress')}
                />
              </ZHField>

              <ZHField label="Moneda Base">
                <select className="zh-input" disabled={saving || !canEdit} {...register('currency')}>
                  {CURRENCIES.map((c) => (
                    <option key={c.value} value={c.value}>{c.label}</option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Zona Horaria">
                <select className="zh-input" disabled={saving || !canEdit} {...register('timezone')}>
                  {TIMEZONES.map((z) => (
                    <option key={z.value} value={z.value}>{z.label}</option>
                  ))}
                </select>
              </ZHField>
            </div>
          </div>
        </div>

        <div className="pg-section pg-section--mb-4">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">account_balance</span>
              <p className="pg-section-label">Tarifas de Impuestos SRI</p>
            </div>
            <span className="badge badge--gray pg-text-11">Solo lectura — catálogo SRI</span>
          </div>

          {taxState.loading ? (
            <div className="pg-section-body"><LoadingState /></div>
          ) : (
            <div className="pg-overflow-x">
              <table className="table">
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre del Impuesto</th>
                    <th>Tipo</th>
                    <th className="pg-th-right">Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {(taxState.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={4} className="pg-state-pad pg-cell-muted pg-td-center">
                        No hay tarifas disponibles
                      </td>
                    </tr>
                  ) : (
                    (taxState.data ?? []).map((rate: CatalogItem & { type?: string }) => (
                      <tr key={rate.id}>
                        <td className="mono subtle">{rate.code}</td>
                        <td>{rate.name}</td>
                        <td>{taxTypeBadge(rate.type ?? 'VAT')}</td>
                        <td className="pg-td-right">
                          <span className={rate.isActive ? 'zh-status zh-status--active' : 'zh-status zh-status--inactive'}>
                            {rate.isActive ? 'Activo' : 'Inactivo'}
                          </span>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          )}

          {taxState.error && (
            <div className="pg-section-body">
              <ZHPageNotice variant="warning" message="No se pudieron cargar las tarifas SRI." detail={taxState.error} />
            </div>
          )}
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" disabled={saving || !isDirty} onClick={handleDiscard}>
              Descartar Cambios
            </ZHBtn>
            <ZHBtn variant="primary" size="md" type="submit" disabled={saving || !canEdit || !isDirty}>
              <span className="material-symbols-outlined">save</span>
              {saving ? t('common.saving') : 'Guardar Configuración'}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
