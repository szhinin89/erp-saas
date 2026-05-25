import type { Dispatch, SetStateAction } from 'react';
import { Modal } from '../../components/Modal';
import { ZHModalHeader } from '../../components/zh/ZHModalHeader';
import { ZHPageNotice } from '../../components/zh/ZHPageNotice';
import { SUBSCRIBER_MODULE_KEYS } from '../../constants/subscriptionModules';
import { ZHBtn, ZHField } from '../../components/zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { defaultModuleChecksAllOn } from './platformSubscriberUtils';
import type { CreateSubscriberWithAdminBody, PlatformPlanCatalogEntry } from './api/platformService';

export type PlatformCreateSubscriberModalProps = {
  t: (key: string, fallback?: string) => string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  busy: boolean;
  error: string;
  form: CreateSubscriberWithAdminBody;
  setForm: Dispatch<SetStateAction<CreateSubscriberWithAdminBody>>;
  planCode: string;
  setPlanCode: (value: string) => void;
  restrictModules: boolean;
  setRestrictModules: (value: boolean) => void;
  moduleChecks: Record<string, boolean>;
  setModuleChecks: Dispatch<SetStateAction<Record<string, boolean>>>;
  activePlans: PlatformPlanCatalogEntry[];
  moduleLabel: (key: string) => string;
  onSave: () => void | Promise<void>;
  slugify: (name: string) => string;
};

export function PlatformCreateSubscriberModal({
  t,
  open,
  onOpenChange,
  busy,
  error,
  form,
  setForm,
  planCode,
  setPlanCode,
  restrictModules,
  setRestrictModules,
  moduleChecks,
  setModuleChecks,
  activePlans,
  moduleLabel,
  onSave,
  slugify,
}: PlatformCreateSubscriberModalProps) {
  if (!open) return null;

  const close = () => {
    if (!busy) onOpenChange(false);
  };

  return (
    <Modal
      onClose={close}
      size="lg"
      header={
        <ZHModalHeader
          title={t('platform.createSubscriber')}
          subtitle={t('platform.createSubscriberSubtitle')}
          onClose={close}
        />
      }
    >
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}

      <div className="sa-form-section">
        <p className="sa-form-section__title">{t('platform.createSubscriber.section.subscriber')}</p>
        <p className="sa-form-section__hint">{t('platform.createSubscriber.section.subscriberHint')}</p>
      </div>

      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.name')} required>
          <input
            className="zh-input"
            value={form.subscriberName}
            onChange={(e) =>
              setForm((s) => {
                const name = e.target.value;
                const nextSlug = s.subscriberSlug.trim() ? s.subscriberSlug : slugify(name);
                return { ...s, subscriberName: name, subscriberSlug: nextSlug };
              })
            }
            placeholder="Ej: Distribuidora XYZ S.A."
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.slug')} hint={t('platform.createSubscriber.field.slugHint')}>
          <input
            className="zh-input"
            value={form.subscriberSlug}
            onChange={(e) => setForm((s) => ({ ...s, subscriberSlug: e.target.value }))}
            placeholder="Ej: distribuidora-xyz"
            disabled={busy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={3}>
        <ZHField label={t('platform.createSubscriber.field.ruc')}>
          <input
            className="zh-input"
            value={form.ruc ?? ''}
            onChange={(e) => setForm((s) => ({ ...s, ruc: e.target.value }))}
            placeholder={t('platform.createSubscriber.field.rucPlaceholder')}
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.countryCode')}>
          <input
            className="zh-input"
            value={form.countryCode ?? 'ECU'}
            onChange={(e) => setForm((s) => ({ ...s, countryCode: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.timezone')}>
          <input
            className="zh-input"
            value={form.timezone ?? 'America/Guayaquil'}
            onChange={(e) => setForm((s) => ({ ...s, timezone: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.planCode')} required>
          <select
            className="zh-input"
            value={planCode}
            onChange={(e) => setPlanCode(e.target.value)}
            disabled={busy || activePlans.length === 0}
            required
          >
            <option value="">
              {activePlans.length === 0
                ? t('platform.createSubscriber.planSelectNoPlans')
                : t('platform.createSubscriber.planSelectPlaceholder')}
            </option>
            {activePlans.map((p) => (
              <option key={p.id} value={p.code}>
                {p.name.trim() ? `${p.name} (${p.code})` : p.code}
              </option>
            ))}
          </select>
        </ZHField>
        <div />
      </ZHGridRow>
      <label className="zh-inline-check">
        <input
          type="checkbox"
          checked={restrictModules}
          onChange={(e) => {
            const on = e.target.checked;
            setRestrictModules(on);
            if (on) setModuleChecks(defaultModuleChecksAllOn());
          }}
          disabled={busy}
        />
        {t('platform.createSubscriber.field.restrictModules')}
      </label>
      {restrictModules ? (
        <p className="subtle sa-modules-hint">{t('platform.createSubscriber.modulesHint')}</p>
      ) : null}
      {restrictModules ? (
        <div className="sa-moduleChecks">
          {SUBSCRIBER_MODULE_KEYS.map((k) => (
            <label key={k} className="zh-inline-check sa-moduleCheck">
              <input
                type="checkbox"
                checked={!!moduleChecks[k]}
                onChange={() =>
                  setModuleChecks((s) => ({
                    ...s,
                    [k]: !s[k],
                  }))
                }
                disabled={busy}
              />
              {moduleLabel(k)}
            </label>
          ))}
        </div>
      ) : null}

      <div className="sa-form-section">
        <p className="sa-form-section__title">{t('platform.createSubscriber.section.admin')}</p>
        <p className="sa-form-section__hint">{t('platform.createSubscriber.section.adminHint')}</p>
      </div>

      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.adminFirstName')} required>
          <input
            className="zh-input"
            value={form.adminFirstName}
            onChange={(e) => setForm((s) => ({ ...s, adminFirstName: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.adminLastName')}>
          <input
            className="zh-input"
            value={form.adminLastName}
            onChange={(e) => setForm((s) => ({ ...s, adminLastName: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.adminEmail')}>
          <input
            className="zh-input"
            value={form.adminEmail}
            onChange={(e) => setForm((s) => ({ ...s, adminEmail: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.passwordResetMode')}>
          <select
            className="zh-input"
            value={String(form.passwordResetMode ?? 0)}
            onChange={(e) => setForm((s) => ({ ...s, passwordResetMode: Number(e.target.value) }))}
            disabled={busy}
          >
            <option value={0}>{t('platform.createSubscriber.passwordResetMode.disabled')}</option>
            <option value={2}>{t('platform.createSubscriber.passwordResetMode.email')}</option>
            <option value={1}>{t('platform.createSubscriber.passwordResetMode.admin')}</option>
          </select>
        </ZHField>
      </ZHGridRow>
      <label className="zh-inline-check">
        <input
          type="checkbox"
          checked={!!form.linkExistingAdmin}
          onChange={(e) => setForm((s) => ({ ...s, linkExistingAdmin: e.target.checked }))}
          disabled={busy}
        />
        {t('platform.createSubscriber.field.linkExistingAdmin')}
      </label>
      {!form.linkExistingAdmin ? (
        <ZHField label={t('platform.createSubscriber.field.adminPassword')}>
          <input
            className="zh-input"
            type="password"
            value={form.adminPassword}
            onChange={(e) => setForm((s) => ({ ...s, adminPassword: e.target.value }))}
            disabled={busy}
          />
        </ZHField>
      ) : null}
      <ZHInlineRowRight>
        <ZHBtn variant="secondary" size="sm" onClick={close} disabled={busy}>
          {t('common.cancel')}
        </ZHBtn>
        <ZHBtn variant="primary" size="sm" onClick={() => void onSave()} disabled={busy}>
          {t('common.save')}
        </ZHBtn>
      </ZHInlineRowRight>
    </Modal>
  );
}
