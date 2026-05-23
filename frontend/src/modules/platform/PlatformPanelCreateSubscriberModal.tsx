import { Modal } from '../../components/Modal';
import { ZHModalHeader } from '../../components/zh/ZHModalHeader';
import { ZHPageNotice } from '../../components/zh/ZHPageNotice';
import { SUBSCRIBER_MODULE_KEYS } from '../../constants/subscriptionModules';
import { ZHBtn, ZHField } from '../../components/zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { defaultModuleChecksAllOn } from './platformPanelUtils';
import type { PlatformPanelPageState } from './usePlatformPanelPage';

type Props = Pick<
  PlatformPanelPageState,
  | 't'
  | 'createSubscriberOpen'
  | 'setCreateSubscriberOpen'
  | 'createBusy'
  | 'createError'
  | 'createForm'
  | 'setCreateForm'
  | 'createPlanCode'
  | 'setCreatePlanCode'
  | 'createRestrictModules'
  | 'setCreateRestrictModules'
  | 'createModuleChecks'
  | 'setCreateModuleChecks'
  | 'activePlans'
  | 'moduleLabel'
  | 'saveCreateSubscriber'
  | 'slugify'
>;

export function PlatformPanelCreateSubscriberModal({
  t,
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
  activePlans,
  moduleLabel,
  saveCreateSubscriber,
  slugify,
}: Props) {
  if (!createSubscriberOpen) return null;

  return (
    <Modal
      onClose={() => (createBusy ? undefined : setCreateSubscriberOpen(false))}
      size="lg"
      header={
        <ZHModalHeader
          title={t('platform.createSubscriber')}
          subtitle={t('platform.createSubscriberSubtitle')}
          onClose={() => (createBusy ? undefined : setCreateSubscriberOpen(false))}
        />
      }
    >
      {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}

      {/* ── Sección 1: Datos del suscriptor ── */}
      <div className="sa-form-section">
        <p className="sa-form-section__title">{t('platform.createSubscriber.section.subscriber')}</p>
        <p className="sa-form-section__hint">{t('platform.createSubscriber.section.subscriberHint')}</p>
      </div>

      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.name')} required>
          <input
            className="zh-input"
            value={createForm.subscriberName}
            onChange={(e) =>
              setCreateForm((s) => {
                const name = e.target.value;
                const nextSlug = s.subscriberSlug.trim() ? s.subscriberSlug : slugify(name);
                return { ...s, subscriberName: name, subscriberSlug: nextSlug };
              })
            }
            placeholder="Ej: Distribuidora XYZ S.A."
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.slug')} hint={t('platform.createSubscriber.field.slugHint')}>
          <input
            className="zh-input"
            value={createForm.subscriberSlug}
            onChange={(e) => setCreateForm((s) => ({ ...s, subscriberSlug: e.target.value }))}
            placeholder="Ej: distribuidora-xyz"
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={3}>
        <ZHField label={t('platform.createSubscriber.field.ruc')}>
          <input
            className="zh-input"
            value={createForm.ruc ?? ''}
            onChange={(e) => setCreateForm((s) => ({ ...s, ruc: e.target.value }))}
            placeholder={t('platform.createSubscriber.field.rucPlaceholder')}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.countryCode')}>
          <input
            className="zh-input"
            value={createForm.countryCode ?? 'ECU'}
            onChange={(e) => setCreateForm((s) => ({ ...s, countryCode: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.timezone')}>
          <input
            className="zh-input"
            value={createForm.timezone ?? 'America/Guayaquil'}
            onChange={(e) => setCreateForm((s) => ({ ...s, timezone: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.planCode')} required>
          <select
            className="zh-input"
            value={createPlanCode}
            onChange={(e) => setCreatePlanCode(e.target.value)}
            disabled={createBusy || activePlans.length === 0}
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
          checked={createRestrictModules}
          onChange={(e) => {
            const on = e.target.checked;
            setCreateRestrictModules(on);
            if (on) setCreateModuleChecks(defaultModuleChecksAllOn());
          }}
          disabled={createBusy}
        />
        {t('platform.createSubscriber.field.restrictModules')}
      </label>
      {createRestrictModules ? (
        <p className="subtle sa-modules-hint">{t('platform.createSubscriber.modulesHint')}</p>
      ) : null}
      {createRestrictModules ? (
        <div className="sa-moduleChecks">
          {SUBSCRIBER_MODULE_KEYS.map((k) => (
            <label key={k} className="zh-inline-check sa-moduleCheck">
              <input
                type="checkbox"
                checked={!!createModuleChecks[k]}
                onChange={() =>
                  setCreateModuleChecks((s) => ({
                    ...s,
                    [k]: !s[k],
                  }))
                }
                disabled={createBusy}
              />
              {moduleLabel(k)}
            </label>
          ))}
        </div>
      ) : null}
      {/* ── Sección 2: Administrador inicial ── */}
      <div className="sa-form-section">
        <p className="sa-form-section__title">{t('platform.createSubscriber.section.admin')}</p>
        <p className="sa-form-section__hint">{t('platform.createSubscriber.section.adminHint')}</p>
      </div>

      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.adminFirstName')} required>
          <input
            className="zh-input"
            value={createForm.adminFirstName}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminFirstName: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.adminLastName')}>
          <input
            className="zh-input"
            value={createForm.adminLastName}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminLastName: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('platform.createSubscriber.field.adminEmail')}>
          <input
            className="zh-input"
            value={createForm.adminEmail}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminEmail: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('platform.createSubscriber.field.passwordResetMode')}>
          <select
            className="zh-input"
            value={String(createForm.passwordResetMode ?? 0)}
            onChange={(e) => setCreateForm((s) => ({ ...s, passwordResetMode: Number(e.target.value) }))}
            disabled={createBusy}
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
          checked={!!createForm.linkExistingAdmin}
          onChange={(e) => setCreateForm((s) => ({ ...s, linkExistingAdmin: e.target.checked }))}
          disabled={createBusy}
        />
        {t('platform.createSubscriber.field.linkExistingAdmin')}
      </label>
      {!createForm.linkExistingAdmin ? (
        <ZHField label={t('platform.createSubscriber.field.adminPassword')}>
          <input
            className="zh-input"
            type="password"
            value={createForm.adminPassword}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminPassword: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      ) : null}
      <ZHInlineRowRight>
        <ZHBtn variant="secondary" size="sm" onClick={() => setCreateSubscriberOpen(false)} disabled={createBusy}>
          {t('common.cancel')}
        </ZHBtn>
        <ZHBtn variant="primary" size="sm" onClick={() => void saveCreateSubscriber()} disabled={createBusy}>
          {t('common.save')}
        </ZHBtn>
      </ZHInlineRowRight>
    </Modal>
  );
}
