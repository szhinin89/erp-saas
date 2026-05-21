import React, { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField } from '../../../../components/zh/ZHForm';
import { usePermissionsStore } from '../../../../store/permissionsStore';
import { useAuthStore } from '../../../../store/authStore';
import { useI18n } from '../../../../i18n/i18n';
import { useAsync } from '../../../../hooks/useAsync';
import { companyService } from '../../../../services/companyService';
import { catalogService, type CatalogItem } from '../../../../services/catalogService';
import { formatApiError } from '../../../lib/formatApiError';
import {
  companyConfigSchema,
  defaultCompanyConfigValues,
  type CompanyConfigValues,
} from '../schemas/companyConfigSchema';

const CURRENCIES = [
  { value: 'USD', label: 'USD — Dólar Estadounidense' },
  { value: 'EUR', label: 'EUR — Euro' },
];

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

function taxTypeBadge(type: string): React.ReactElement {
  const upper = type.toUpperCase();
  if (upper === 'VAT')    return <span className="badge badge--green">IVA</span>;
  if (upper === 'EXCISE') return <span className="badge badge--red">ICE</span>;
  return <span className="badge badge--gray">{upper}</span>;
}

export function CompanyConfigPage() {
  const { t } = useI18n();
  const hasPerm  = usePermissionsStore((s) => s.has);
  const role     = useAuthStore((s) => s.user?.role ?? '');
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');

  const isAdmin  = role === 'Admin' || role === 'SuperAdmin';
  const canView  = isAdmin || hasPerm('configuracion.empresa.view');
  const canEdit  = isAdmin || hasPerm('configuracion.empresa.edit');

  const [saving,    setSaving]    = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved,     setSaved]     = useState(false);

  const subscriberState = useAsync(
    () => subscriberId ? companyService.getSubscriber(subscriberId) : Promise.resolve(null),
    !!subscriberId,
  );

  const taxState = useAsync(() => catalogService.taxRates(false));

  const { register, handleSubmit, reset, formState: { errors, isDirty } } = useForm<CompanyConfigValues>({
    resolver: zodResolver(companyConfigSchema),
    defaultValues: defaultCompanyConfigValues,
  });

  useEffect(() => {
    const d = subscriberState.data;
    if (!d) return;
    reset({
      companyName:       d.name ?? '',
      ruc:               d.ruc ?? '',
      shortName:         d.shortName ?? '',
      currency:          d.currency ?? 'USD',
      language:          d.language ?? 'es',
      timezone:          d.timezone ?? 'America/Guayaquil',
      invoicePrefix:     d.invoicePrefix ?? 'FAC-',
      initialFolio:      1001,
      defaultCreditDays: d.defaultCreditDays ?? 30,
    });
  }, [subscriberState.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || !subscriberId) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await companyService.updateSubscriberCompany(subscriberId, {
        name:         values.companyName,
        slug:         subscriberState.data?.slug ?? '',
        ruc:          values.ruc  || null,
        shortName:    values.shortName || null,
        tradeName:    subscriberState.data?.tradeName ?? null,
        dinardap:     subscriberState.data?.dinardap  ?? null,
        logoUrl:      subscriberState.data?.logoUrl   ?? null,
        displayOrder: subscriberState.data?.displayOrder ?? 0,
        priority:     subscriberState.data?.priority     ?? 0,
      });

      await companyService.updateSubscriberOperationalSettings(subscriberId, {
        currency:          values.currency,
        language:          values.language,
        timezone:          values.timezone,
        invoicePrefix:     values.invoicePrefix || null,
        defaultCreditDays: values.defaultCreditDays ?? 30,
      });

      setSaved(true);
      subscriberState.refetch();
    } catch (err) {
      setSaveError(formatApiError(err));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const d = subscriberState.data;
    if (d) {
      reset({
        companyName:       d.name ?? '',
        ruc:               d.ruc ?? '',
        shortName:         d.shortName ?? '',
        currency:          d.currency ?? 'USD',
        language:          d.language ?? 'es',
        timezone:          d.timezone ?? 'America/Guayaquil',
        invoicePrefix:     d.invoicePrefix ?? 'FAC-',
        initialFolio:      1001,
        defaultCreditDays: d.defaultCreditDays ?? 30,
      });
    }
  };

  if (!canView) return <NoAccessPage title="Configuración de Empresa" />;
  if (subscriberState.loading) return <LoadingState />;

  return (
    <div className="pg-page">

      <div className="pg-header-row">
        <div className="pg-header-left">
          <nav className="pg-breadcrumb" aria-label="Ruta de navegación">
            <span className="pg-breadcrumb-item">Configuración</span>
            <span className="material-symbols-outlined pg-breadcrumb-sep">chevron_right</span>
            <span className="pg-breadcrumb-item">Empresa</span>
          </nav>
          <h1 className="pg-title">Configuración de Parámetros</h1>
          <p className="pg-subtitle">Ajusta los valores globales y las preferencias operativas del sistema.</p>
        </div>
      </div>

      {subscriberState.error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={subscriberState.error} />
      )}
      {saveError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
      )}
      {saved && (
        <ZHPageNotice variant="success" message="Configuración guardada correctamente." />
      )}

      <form onSubmit={onSubmit}>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">settings</span>
              <p className="pg-section-label">Parámetros Generales</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Nombre de la Empresa" required error={errors.companyName?.message}>
                <input
                  className="zh-input"
                  placeholder="Razón social registrada"
                  disabled={saving || !canEdit}
                  {...register('companyName')}
                />
              </ZHField>

              <ZHField label="Nombre Comercial / Corto" error={errors.shortName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre visible en documentos"
                  disabled={saving || !canEdit}
                  {...register('shortName')}
                />
              </ZHField>

              <ZHField label="RUC" error={errors.ruc?.message}>
                <input
                  className="zh-input"
                  placeholder="13 dígitos"
                  disabled={saving || !canEdit}
                  {...register('ruc')}
                />
              </ZHField>

              <ZHField label="Moneda Base">
                <select className="zh-input" disabled={saving || !canEdit} {...register('currency')}>
                  {CURRENCIES.map((c) => (
                    <option key={c.value} value={c.value}>{c.label}</option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Idioma del Sistema">
                <select className="zh-input" disabled={saving || !canEdit} {...register('language')}>
                  {LANGUAGES.map((l) => (
                    <option key={l.value} value={l.value}>{l.label}</option>
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

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">receipt_long</span>
              <p className="pg-section-label">Facturación y Folios</p>
            </div>
          </div>
          <div className="pg-section-body">
            <div className="pg-form-grid pg-form-grid--3">
              <ZHField label="Prefijo de Factura" error={errors.invoicePrefix?.message}>
                <input
                  className="zh-input"
                  placeholder="FAC-"
                  disabled={saving || !canEdit}
                  {...register('invoicePrefix')}
                />
              </ZHField>

              <ZHField label="Folio Inicial" error={errors.initialFolio?.message}>
                <input
                  className="zh-input"
                  type="number"
                  min={1}
                  disabled={saving || !canEdit}
                  {...register('initialFolio')}
                />
              </ZHField>

              <ZHField label="Días de Crédito por Defecto" error={errors.defaultCreditDays?.message}>
                <input
                  className="zh-input"
                  type="number"
                  min={0}
                  disabled={saving || !canEdit}
                  {...register('defaultCreditDays')}
                />
              </ZHField>
            </div>

            <div style={{ marginTop: 'var(--space-4)' }}>
              <ZHPageNotice
                variant="info"
                message="La numeración de folios es correlativa y se reiniciará al cambiar el prefijo."
              />
            </div>
          </div>
        </div>

        <div className="pg-section" style={{ marginBottom: 'var(--space-4)' }}>
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">account_balance</span>
              <p className="pg-section-label">Tarifas de Impuestos SRI</p>
            </div>
            <span className="badge badge--gray" style={{ fontSize: 11 }}>Solo lectura — catálogo SRI</span>
          </div>

          {taxState.loading ? (
            <div className="pg-section-body"><LoadingState /></div>
          ) : (
            <div style={{ overflowX: 'auto' }}>
              <table className="table">
                <thead>
                  <tr>
                    <th>Código</th>
                    <th>Nombre del Impuesto</th>
                    <th>Tipo</th>
                    <th style={{ textAlign: 'right' }}>Estado</th>
                  </tr>
                </thead>
                <tbody>
                  {(taxState.data ?? []).length === 0 ? (
                    <tr>
                      <td colSpan={4} style={{ textAlign: 'center', padding: 'var(--space-6)', color: 'var(--color-text-secondary)' }}>
                        No hay tarifas disponibles
                      </td>
                    </tr>
                  ) : (
                    (taxState.data ?? []).map((rate: CatalogItem & { type?: string }) => (
                      <tr key={rate.id}>
                        <td className="mono subtle">{rate.code}</td>
                        <td>{rate.name}</td>
                        <td>{taxTypeBadge((rate as any).type ?? 'VAT')}</td>
                        <td style={{ textAlign: 'right' }}>
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
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            Los cambios en nombre y RUC se aplican inmediatamente. Moneda, idioma y zona horaria requieren soporte de configuración extendida (próxima versión).
          </div>
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={saving || !isDirty}
              onClick={handleDiscard}
            >
              Descartar Cambios
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={saving || !canEdit || !isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? t('common.saving') : 'Guardar Configuración'}
            </ZHBtn>
          </div>
        </div>

      </form>
    </div>
  );
}
