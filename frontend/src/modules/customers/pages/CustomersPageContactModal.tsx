import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { Customer } from '../api/customerService';
import type { ContactItem } from './customersPageUtils';

type CustomersPageContactModalProps = {
  t: (key: string) => string;
  open: boolean;
  editingContactId: string | null;
  contactForm: Omit<ContactItem, 'id'>;
  setContactForm: React.Dispatch<React.SetStateAction<Omit<ContactItem, 'id'>>>;
  customers: Customer[];
  onClose: () => void;
  onSave: () => void;
  onDelete: (contactId: string) => void;
};

export function CustomersPageContactModal({
  t,
  open,
  editingContactId,
  contactForm,
  setContactForm,
  customers,
  onClose,
  onSave,
  onDelete,
}: CustomersPageContactModalProps) {
  if (!open) return null;

  return (
    <div
      className="zh-modal-overlay"
      role="dialog"
      aria-modal="true"
      aria-label={editingContactId ? t('customers.contacts.modal.editTitle') : t('customers.contacts.modal.createTitle')}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="zh-modal">
        <div className="zh-modal-header">
          <h2 className="zh-modal-title">
            {editingContactId ? t('customers.contacts.modal.editTitle') : t('customers.contacts.modal.createTitle')}
          </h2>
          <button type="button" className="zh-modal-close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="zh-modal-body">
          <ZHField label={t('customers.contacts.table.customer')} required>
            <select
              className="zh-input"
              value={contactForm.customerId}
              onChange={(e) => setContactForm((p) => ({ ...p, customerId: e.target.value }))}
            >
              <option value="">{t('customers.contacts.modal.selectCustomer')}</option>
              {customers.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.fullName}
                </option>
              ))}
            </select>
          </ZHField>
          <div className="pg-form-grid pg-form-grid--2">
            <ZHField label={t('customers.contacts.modal.name')} required>
              <input
                className="zh-input"
                value={contactForm.name}
                onChange={(e) => setContactForm((p) => ({ ...p, name: e.target.value }))}
              />
            </ZHField>
            <ZHField label={t('customers.contacts.modal.role')}>
              <input
                className="zh-input"
                value={contactForm.role}
                onChange={(e) => setContactForm((p) => ({ ...p, role: e.target.value }))}
              />
            </ZHField>
          </div>
          <div className="pg-form-grid pg-form-grid--2">
            <ZHField label={t('customers.form.email')} required>
              <input
                className="zh-input"
                type="email"
                value={contactForm.email}
                onChange={(e) => setContactForm((p) => ({ ...p, email: e.target.value }))}
              />
            </ZHField>
            <ZHField label={t('customers.form.phone')}>
              <input
                className="zh-input"
                value={contactForm.phone}
                onChange={(e) => setContactForm((p) => ({ ...p, phone: e.target.value }))}
              />
            </ZHField>
          </div>
          <div className="pg-actions-bar">
            <div className="pg-actions-buttons">
              {editingContactId && (
                <ZHBtn variant="destructive" size="md" type="button" onClick={() => onDelete(editingContactId)}>
                  {t('customers.disable')}
                </ZHBtn>
              )}
              <ZHBtn variant="ghost" size="md" type="button" onClick={onClose}>
                {t('common.cancel')}
              </ZHBtn>
              <ZHBtn variant="primary" size="md" type="button" onClick={onSave}>
                {t('common.save')}
              </ZHBtn>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
