import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { LoadingState, NoAccessPage } from '../../../../components/PageShell';
import { ZHPageNotice } from '../../../../components/zh/ZHPageNotice';
import { ZHBtn, ZHField, ZHGrid } from '../../../../components/zh/ZHForm';
import { useI18n } from '../../../../i18n/i18n';
import { useAsync } from '../../../../hooks/useAsync';
import { loadDecimalConfig, saveDecimalConfig } from '../../../../lib/config/decimal.config';
import { applyServerErrors } from '../../../lib/validationErrors';
import { formatApiRequestError } from '../../../lib/apiError';
import { usePermissionsUi } from '../../../../access/usePermissionsUi';
import {
  decimalSettingsSchema,
  defaultDecimalSettingsValues,
  type DecimalSettingsValues,
} from '../schemas/decimalSettingsSchema';

const DECIMAL_OPTIONS = [0, 1, 2, 3, 4, 5, 6];

export function DecimalSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow('configuracion.empresa.view');
  const canEdit = canShow('configuracion.empresa.edit');

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const cfgState = useAsync(() => loadDecimalConfig());

  const { register, handleSubmit, reset, setError: setFieldError, formState: { errors, isDirty } } = useForm<DecimalSettingsValues>({
    resolver: zodResolver(decimalSettingsSchema),
    defaultValues: defaultDecimalSettingsValues,
  });

  useEffect(() => {
    if (!cfgState.data) return;
    reset(cfgState.data);
  }, [cfgState.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await saveDecimalConfig(values);
      setSaved(true);
      cfgState.refetch();
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) => setSaveError(msg));
      if (!applied) setSaveError(formatApiRequestError(err, { offline: t('common.apiUnreachable'), generic: t('common.errorGeneric') }));
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    if (cfgState.data) reset(cfgState.data);
  };

  if (!canView) return <NoAccessPage title={t('settings.company.title')} />;
  if (cfgState.loading) return <LoadingState />;

  return (
    <>
      {cfgState.error && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={cfgState.error} />
      )}
      {saveError && (
        <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={saveError} />
      )}
      {saved && <ZHPageNotice variant="success" message={t('settings.company.saved')} />}

      <form onSubmit={onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">decimal_increase</span>
              <p className="pg-section-label">{t('settings.company.decimals.title')}</p>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="zh-text-muted zh-mb-16">
              {t('settings.company.decimals.description')}
            </p>
            <ZHGrid cols={2}>
              <ZHField label={t('settings.company.decimals.salesUnitPrice')} error={errors.salesUnitPrice?.message}>
                <select disabled={saving || !canEdit} {...register('salesUnitPrice')}>
                  {DECIMAL_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </ZHField>

              <ZHField label={t('settings.company.decimals.purchaseUnitPrice')} error={errors.purchaseUnitPrice?.message}>
                <select disabled={saving || !canEdit} {...register('purchaseUnitPrice')}>
                  {DECIMAL_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </ZHField>

              <ZHField label={t('settings.company.decimals.quantity')} error={errors.quantity?.message}>
                <select disabled={saving || !canEdit} {...register('quantity')}>
                  {DECIMAL_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </ZHField>

              <ZHField label={t('settings.company.decimals.percentage')} error={errors.percentage?.message}>
                <select disabled={saving || !canEdit} {...register('percentage')}>
                  {DECIMAL_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </ZHField>

              <ZHField label={t('settings.company.decimals.totalAmount')} error={errors.totalAmount?.message}>
                <select disabled={saving || !canEdit} {...register('totalAmount')}>
                  {DECIMAL_OPTIONS.map(n => <option key={n} value={n}>{n}</option>)}
                </select>
              </ZHField>
            </ZHGrid>
          </div>
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
