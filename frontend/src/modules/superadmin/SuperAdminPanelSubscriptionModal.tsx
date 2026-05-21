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
  | 'subModalOpen'
  | 'setSubModalOpen'
  | 'subModalSubscriber'
  | 'subPlanCode'
  | 'setSubPlanCode'
  | 'subRestrict'
  | 'setSubRestrict'
  | 'subModuleChecks'
  | 'setSubModuleChecks'
  | 'subBusy'
  | 'subError'
  | 'activePlans'
  | 'moduleLabel'
  | 'saveSubscriptionModal'
>;

export function SuperAdminPanelSubscriptionModal({
  t,
  subModalOpen,
  setSubModalOpen,
  subModalSubscriber,
  subPlanCode,
  setSubPlanCode,
  subRestrict,
  setSubRestrict,
  subModuleChecks,
  setSubModuleChecks,
  subBusy,
  subError,
  activePlans,
  moduleLabel,
  saveSubscriptionModal,
}: Props) {
  if (!subModalOpen || !subModalSubscriber) return null;

  return (
    <Modal
      onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
      size="lg"
      header={
        <ZHModalHeader
          title={t('superadmin.changeSubscription.title')}
          subtitle={t('superadmin.changeSubscription.subtitle').replace('{name}', subModalSubscriber.name)}
          onClose={() => (subBusy ? undefined : setSubModalOpen(false))}
        />
      }
    >
      {subError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={subError} /> : null}
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.createSubscriber.field.planCode')}>
          <select className="zh-input" value={subPlanCode} onChange={(e) => setSubPlanCode(e.target.value)} disabled={subBusy}>
            <option value="">{t('superadmin.createSubscriber.planOptional')}</option>
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
          checked={subRestrict}
          onChange={(e) => {
            const on = e.target.checked;
            setSubRestrict(on);
            if (on) setSubModuleChecks(defaultModuleChecksAllOn());
          }}
          disabled={subBusy}
        />
        {t('superadmin.createSubscriber.field.restrictModules')}
      </label>
      {subRestrict ? <p className="subtle sa-modules-hint">{t('superadmin.createSubscriber.modulesHint')}</p> : null}
      {subRestrict ? (
        <div className="sa-moduleChecks">
          {SUBSCRIBER_MODULE_KEYS.map((k) => (
            <label key={k} className="zh-inline-check sa-moduleCheck">
              <input
                type="checkbox"
                checked={!!subModuleChecks[k]}
                onChange={() =>
                  setSubModuleChecks((s) => ({
                    ...s,
                    [k]: !s[k],
                  }))
                }
                disabled={subBusy}
              />
              {moduleLabel(k)}
            </label>
          ))}
        </div>
      ) : null}
      <ZHInlineRowRight>
        <ZHBtn variant="secondary" size="sm" onClick={() => setSubModalOpen(false)} disabled={subBusy}>
          {t('common.cancel')}
        </ZHBtn>
        <ZHBtn variant="primary" size="sm" onClick={() => void saveSubscriptionModal()} disabled={subBusy}>
          {t('common.save')}
        </ZHBtn>
      </ZHInlineRowRight>
    </Modal>
  );
}
