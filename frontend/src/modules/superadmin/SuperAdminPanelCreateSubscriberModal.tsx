import { Modal } from '../../components/Modal';
import { ZHModalHeader } from '../../components/zh/ZHModalHeader';
import { ZHPageNotice } from '../../components/zh/ZHPageNotice';
import { SUBSCRIBER_MODULE_KEYS } from '../../constants/subscriptionModules';
import { ZHBtn, ZHField } from '../../components/zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../../components/zh/ZHLayout';
import { defaultModuleChecksAllOn } from './superAdminPanelUtils';
import type { SuperAdminPanelPageState } from './useSuperAdminPanelPage';

type Props = Pick<
  SuperAdminPanelPageState,
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

export function SuperAdminPanelCreateSubscriberModal({
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
          title={t('superadmin.createSubscriber')}
          subtitle={t('superadmin.createSubscriberSubtitle')}
          onClose={() => (createBusy ? undefined : setCreateSubscriberOpen(false))}
        />
      }
    >
      {createError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={createError} /> : null}
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.createSubscriber.field.name')}>
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
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('superadmin.createSubscriber.field.slug')}>
          <input
            className="zh-input"
            value={createForm.subscriberSlug}
            onChange={(e) => setCreateForm((s) => ({ ...s, subscriberSlug: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={3}>
        <ZHField label={t('superadmin.createSubscriber.field.ruc')}>
          <input
            className="zh-input"
            value={createForm.ruc ?? ''}
            onChange={(e) => setCreateForm((s) => ({ ...s, ruc: e.target.value }))}
            placeholder={t('superadmin.createSubscriber.field.rucPlaceholder')}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('superadmin.createSubscriber.field.countryCode')}>
          <input
            className="zh-input"
            value={createForm.countryCode ?? 'ECU'}
            onChange={(e) => setCreateForm((s) => ({ ...s, countryCode: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('superadmin.createSubscriber.field.timezone')}>
          <input
            className="zh-input"
            value={createForm.timezone ?? 'America/Guayaquil'}
            onChange={(e) => setCreateForm((s) => ({ ...s, timezone: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.createSubscriber.field.planCode')}>
          <select
            className="zh-input"
            value={createPlanCode}
            onChange={(e) => setCreatePlanCode(e.target.value)}
            disabled={createBusy || activePlans.length === 0}
            required
          >
            <option value="">
              {activePlans.length === 0
                ? t('superadmin.createSubscriber.planSelectNoPlans')
                : t('superadmin.createSubscriber.planSelectPlaceholder')}
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
        {t('superadmin.createSubscriber.field.restrictModules')}
      </label>
      {createRestrictModules ? (
        <p className="subtle sa-modules-hint">{t('superadmin.createSubscriber.modulesHint')}</p>
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
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.createSubscriber.field.adminFirstName')}>
          <input
            className="zh-input"
            value={createForm.adminFirstName}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminFirstName: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('superadmin.createSubscriber.field.adminLastName')}>
          <input
            className="zh-input"
            value={createForm.adminLastName}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminLastName: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.createSubscriber.field.adminEmail')}>
          <input
            className="zh-input"
            value={createForm.adminEmail}
            onChange={(e) => setCreateForm((s) => ({ ...s, adminEmail: e.target.value }))}
            disabled={createBusy}
          />
        </ZHField>
        <ZHField label={t('superadmin.createSubscriber.field.passwordResetMode')}>
          <select
            className="zh-input"
            value={String(createForm.passwordResetMode ?? 0)}
            onChange={(e) => setCreateForm((s) => ({ ...s, passwordResetMode: Number(e.target.value) }))}
            disabled={createBusy}
          >
            <option value={0}>{t('superadmin.createSubscriber.passwordResetMode.disabled')}</option>
            <option value={2}>{t('superadmin.createSubscriber.passwordResetMode.email')}</option>
            <option value={1}>{t('superadmin.createSubscriber.passwordResetMode.admin')}</option>
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
        {t('superadmin.createSubscriber.field.linkExistingAdmin')}
      </label>
      {!createForm.linkExistingAdmin ? (
        <ZHField label={t('superadmin.createSubscriber.field.adminPassword')}>
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
