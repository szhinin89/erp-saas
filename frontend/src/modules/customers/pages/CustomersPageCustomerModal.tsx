import type { FieldErrors, UseFormRegister } from 'react-hook-form';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { CustomerFormValues } from '../schemas/customerSchema';

type CustomersPageCustomerModalProps = {
  t: (key: string) => string;
  open: boolean;
  editingCustomerId: string | null;
  creating: boolean;
  canCreate: boolean;
  customerStatus: 'active' | 'inactive';
  setCustomerStatus: (value: 'active' | 'inactive') => void;
  register: UseFormRegister<CustomerFormValues>;
  errors: FieldErrors<CustomerFormValues>;
  onClose: () => void;
  onSubmit: (e?: React.BaseSyntheticEvent) => Promise<void>;
};

export function CustomersPageCustomerModal({
  t,
  open,
  editingCustomerId,
  creating,
  canCreate,
  customerStatus,
  setCustomerStatus,
  register,
  errors,
  onClose,
  onSubmit,
}: CustomersPageCustomerModalProps) {
  if (!open) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={editingCustomerId ? t('customers.modal.editTitle') : t('customers.modal.createTitle')}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="zh-modal">
        <div className="zh-modal-header">
          <h2 className="zh-modal-title">
            {editingCustomerId ? t('customers.modal.editTitle') : t('customers.modal.createTitle')}
          </h2>
          <button type="button" className="zh-modal-close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="zh-modal-body">
          <form onSubmit={onSubmit}>
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField
                label={t('customers.form.identification')}
                required
                error={errors.identification?.message ? t(String(errors.identification.message)) : undefined}
              >
                <input
                  className="zh-input"
                  placeholder={t('customers.form.identificationPlaceholder')}
                  disabled={creating}
                  {...register('identification')}
                />
              </ZHField>
              <ZHField
                label={t('customers.form.fullName')}
                required
                error={errors.fullName?.message ? t(String(errors.fullName.message)) : undefined}
              >
                <input
                  className="zh-input"
                  placeholder={t('customers.form.fullNamePlaceholder')}
                  disabled={creating}
                  {...register('fullName')}
                />
              </ZHField>
            </div>
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField
                label={t('customers.form.email')}
                required
                error={errors.email?.message ? t(String(errors.email.message)) : undefined}
              >
                <input
                  className="zh-input"
                  type="email"
                  placeholder={t('customers.form.emailPlaceholder')}
                  disabled={creating}
                  {...register('email')}
                />
              </ZHField>
              <ZHField label={t('customers.form.phone')}>
                <input
                  className="zh-input"
                  placeholder={t('customers.form.phonePlaceholder')}
                  disabled={creating}
                  {...register('phone')}
                />
              </ZHField>
            </div>
            <ZHField label={t('customers.form.address')}>
              <textarea className="zh-input" rows={2} disabled={creating} {...register('address')} />
            </ZHField>
            <ZHField label={t('customers.modal.status')}>
              <select
                className="zh-input"
                value={customerStatus}
                onChange={(e) => setCustomerStatus(e.target.value as 'active' | 'inactive')}
              >
                <option value="active">{t('customers.modal.statusActive')}</option>
                <option value="inactive">{t('customers.modal.statusInactive')}</option>
              </select>
            </ZHField>
            <div className="pg-actions-bar">
              <div className="pg-actions-buttons">
                <ZHBtn variant="ghost" size="md" type="button" onClick={onClose}>
                  {t('common.cancel')}
                </ZHBtn>
                <ZHBtn
                  variant="primary"
                  size="md"
                  type="submit"
                  disabled={creating || (!canCreate && !editingCustomerId)}
                >
                  {creating ? t('common.saving') : t('common.save')}
                </ZHBtn>
              </div>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
