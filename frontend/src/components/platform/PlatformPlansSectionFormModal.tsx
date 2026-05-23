import type { Dispatch, SetStateAction } from 'react';
import { Modal } from '../Modal';
import { ZHModalHeader } from '../zh/ZHModalHeader';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHGridRow, ZHInlineRowRight } from '../zh/ZHLayout';
import type { PlanFormState } from './platformPlansSectionUtils';

type Props = {
  mode: 'create' | 'edit';
  planForm: PlanFormState;
  setPlanForm: Dispatch<SetStateAction<PlanFormState>>;
  busy: boolean;
  t: (key: string) => string;
  onClose: () => void;
  onSave: () => void;
};

export function PlatformPlansSectionFormModal({ mode, planForm, setPlanForm, busy, t, onClose, onSave }: Props) {
  return (
    <Modal
      onClose={onClose}
      size="lg"
      header={
        <ZHModalHeader
          title={mode === 'create' ? t('superadmin.plansAdmin.modalCreate') : t('superadmin.plansAdmin.modalEdit')}
          subtitle={t('superadmin.plansAdmin.modalSubtitle')}
          onClose={onClose}
        />
      }
    >
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.plansAdmin.field.code')}>
          <input
            className="zh-input"
            value={planForm.code}
            onChange={(e) => setPlanForm((s) => ({ ...s, code: e.target.value }))}
            disabled={mode === 'edit'}
          />
        </ZHField>
        <ZHField label={t('superadmin.plansAdmin.field.shortLabel')}>
          <input
            className="zh-input"
            value={planForm.shortLabel}
            onChange={(e) => setPlanForm((s) => ({ ...s, shortLabel: e.target.value }))}
            placeholder="STARTER"
          />
        </ZHField>
      </ZHGridRow>
      <ZHField label={t('superadmin.plansAdmin.field.name')}>
        <input className="zh-input" value={planForm.name} onChange={(e) => setPlanForm((s) => ({ ...s, name: e.target.value }))} />
      </ZHField>
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.plansAdmin.field.price')}>
          <input
            className="zh-input"
            value={planForm.priceAmount}
            onChange={(e) => setPlanForm((s) => ({ ...s, priceAmount: e.target.value }))}
          />
        </ZHField>
        <ZHField label={t('superadmin.plansAdmin.field.currency')}>
          <input
            className="zh-input"
            value={planForm.currency}
            onChange={(e) => setPlanForm((s) => ({ ...s, currency: e.target.value }))}
            maxLength={8}
          />
        </ZHField>
      </ZHGridRow>
      <ZHGridRow cols={2}>
        <ZHField label={t('superadmin.plansAdmin.field.billing')}>
          <select
            className="zh-input"
            value={planForm.billingCycle}
            onChange={(e) => setPlanForm((s) => ({ ...s, billingCycle: e.target.value }))}
          >
            <option value="monthly">{t('superadmin.plansAdmin.cycle.monthly')}</option>
            <option value="quarterly">{t('superadmin.plansAdmin.cycle.quarterly')}</option>
            <option value="yearly">{t('superadmin.plansAdmin.cycle.yearly')}</option>
            <option value="one_time">{t('superadmin.plansAdmin.cycle.oneTime')}</option>
          </select>
        </ZHField>
        <ZHField label={t('superadmin.plansAdmin.field.sort')}>
          <input
            className="zh-input"
            value={planForm.sortOrder}
            onChange={(e) => setPlanForm((s) => ({ ...s, sortOrder: e.target.value }))}
          />
        </ZHField>
      </ZHGridRow>
      <ZHField label={t('superadmin.plansAdmin.field.billingRef')}>
        <input
          className="zh-input"
          value={planForm.externalBillingRef}
          onChange={(e) => setPlanForm((s) => ({ ...s, externalBillingRef: e.target.value }))}
          placeholder="price_… (Stripe u otro)"
        />
      </ZHField>
      <label className="zh-inline-check">
        <input
          type="checkbox"
          checked={planForm.isActive}
          onChange={(e) => setPlanForm((s) => ({ ...s, isActive: e.target.checked }))}
        />
        {t('superadmin.plansAdmin.field.active')}
      </label>
      <label className="zh-inline-check">
        <input
          type="checkbox"
          checked={planForm.isPubliclyVisible}
          onChange={(e) => setPlanForm((s) => ({ ...s, isPubliclyVisible: e.target.checked }))}
        />
        {t('superadmin.plansAdmin.field.public')}
      </label>
      {mode === 'create' ? (
        <label className="zh-inline-check">
          <input
            type="checkbox"
            checked={planForm.isRecommended}
            onChange={(e) => setPlanForm((s) => ({ ...s, isRecommended: e.target.checked }))}
          />
          {t('superadmin.plansAdmin.field.recommendedCreate')}
        </label>
      ) : (
        <label className="zh-inline-check">
          <input
            type="checkbox"
            checked={planForm.isRecommended}
            onChange={(e) => setPlanForm((s) => ({ ...s, isRecommended: e.target.checked }))}
          />
          {t('superadmin.plansAdmin.field.recommendedEdit')}
        </label>
      )}
      <ZHInlineRowRight>
        <ZHBtn variant="ghost" type="button" onClick={onClose}>
          {t('common.cancel')}
        </ZHBtn>
        <ZHBtn variant="primary" type="button" onClick={onSave} disabled={busy}>
          {t('common.save')}
        </ZHBtn>
      </ZHInlineRowRight>
    </Modal>
  );
}
