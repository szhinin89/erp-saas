import { useState } from 'react';
import { useI18n } from '../../i18n/i18n';
import { TENANT_MODULE_KEYS, type TenantModuleKey } from '../../constants/subscriptionModules';
import { ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHBtn } from '../zh/ZHForm';
import './TenantSubscriptionEditor.css';

export type SubscriptionCompanyRow = {
  id: string;
  name: string;
  slug: string;
  planCode?: string | null;
  enabledModules?: string[];
  hasModuleRestrictions?: boolean;
};

type Props = {
  tenant: SubscriptionCompanyRow;
  onSave: (body: { planCode: string | null; enabledModules: string[] | null }) => Promise<void>;
  onBack: () => void;
};

export function TenantSubscriptionEditor({ tenant, onSave, onBack }: Props) {
  const { t } = useI18n();
  const [planCode, setPlanCode] = useState(tenant.planCode ?? '');
  const [unrestricted, setUnrestricted] = useState(!tenant.hasModuleRestrictions);
  const [selected, setSelected] = useState<Set<TenantModuleKey>>(() => {
    const mods = tenant.enabledModules ?? [];
    const s = new Set<TenantModuleKey>();
    for (const k of TENANT_MODULE_KEYS) {
      if (mods.some((m) => m.toLowerCase() === k)) s.add(k);
    }
    return s;
  });
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const toggleModule = (key: TenantModuleKey) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  };

  const submit = async () => {
    setError('');
    setSaving(true);
    try {
      const trimmed = planCode.trim();
      const plan = trimmed.length === 0 ? null : trimmed;
      const modules: string[] | null = unrestricted ? null : TENANT_MODULE_KEYS.filter((k) => selected.has(k));
      if (!unrestricted && modules!.length === 0) {
        setError(t('companies.subscription.errorNeedOneModule'));
        return;
      }
      await onSave({ planCode: plan, enabledModules: modules });
    } catch (err: unknown) {
      const msg =
        (err as { response?: { data?: { message?: string } } })?.response?.data?.message ??
        t('companies.subscription.errorSave');
      setError(msg);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div>
      <ZHFormAlert type="info" message={tenant.name} detail={`${tenant.slug} · ${tenant.id}`} />
      {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <ZHFormSection title={t('companies.subscription.planSection')}>
        <ZHGrid cols={1}>
          <ZHField label={t('companies.subscription.planCode')}>
            <input
              value={planCode}
              onChange={(e) => setPlanCode(e.target.value)}
              disabled={saving}
              placeholder={t('companies.subscription.planPlaceholder')}
            />
          </ZHField>
        </ZHGrid>
      </ZHFormSection>

      <ZHFormSection title={t('companies.subscription.modulesSection')}>
        <ZHGrid cols={1}>
          <label className="zh-checkbox-line">
            <input
              type="checkbox"
              checked={unrestricted}
              onChange={(e) => setUnrestricted(e.target.checked)}
              disabled={saving}
            />
            <span>{t('companies.subscription.allModules')}</span>
          </label>
          <p className="zh-help-text">{t('companies.subscription.modulesHint')}</p>
          <div className="zh-module-checkboxes">
            {TENANT_MODULE_KEYS.map((key) => (
              <label key={key} className="zh-checkbox-line">
                <input
                  type="checkbox"
                  checked={selected.has(key)}
                  onChange={() => toggleModule(key)}
                  disabled={saving || unrestricted}
                />
                <span>{t(`companies.subscription.module.${key}`)}</span>
              </label>
            ))}
          </div>
        </ZHGrid>
      </ZHFormSection>

      <div className="zh-form-actions-row">
        <ZHBtn variant="secondary" size="md" type="button" disabled={saving} onClick={onBack}>
          {t('companies.subscription.back')}
        </ZHBtn>
        <ZHBtn variant="primary" size="md" type="button" disabled={saving} onClick={() => void submit()}>
          {saving ? t('companies.subscription.saving') : t('companies.subscription.save')}
        </ZHBtn>
      </div>
    </div>
  );
}
