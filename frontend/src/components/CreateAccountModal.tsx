import { useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';
import { useI18n } from '../i18n/i18n';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

const EMPTY: CreateAccountRequest = { code: '', name: '', type: 0, nature: 0, parentId: null };

export function CreateAccountModal({ onClose, onCreated }: Props) {
  const { t } = useI18n();
  const [form, setForm]     = useState<CreateAccountRequest>(EMPTY);
  const [error, setError]   = useState('');
  const [loading, setLoading] = useState(false);

  const set = (field: keyof CreateAccountRequest, value: unknown) =>
    setForm((f) => ({ ...f, [field]: value }));

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await accountingService.createAccount({
        ...form,
        parentId: form.parentId || null,
      });
      onCreated();
      onClose();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { error?: string } } })
          ?.response?.data?.error ?? t('accounting.accounts.modal.create.error')
      );
    } finally {
      setLoading(false);
    }
  };

  const accountTypes = [
    { value: 0, label: t('accounting.accounts.type.asset') },
    { value: 1, label: t('accounting.accounts.type.liability') },
    { value: 2, label: t('accounting.accounts.type.equity') },
    { value: 3, label: t('accounting.accounts.type.income') },
    { value: 4, label: t('accounting.accounts.type.expense') },
  ];

  const accountNatures = [
    { value: 0, label: t('accounting.accounts.nature.debit') },
    { value: 1, label: t('accounting.accounts.nature.credit') },
  ];

  return (
    <Modal title={t('accounting.accounts.modal.create.title')} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <div className="form-grid form-grid--2col">
          <div className="field">
            <label htmlFor="code">{t('accounting.accounts.form.code')}</label>
            <input
              id="code"
              value={form.code}
              onChange={(e) => set('code', e.target.value)}
              placeholder={t('accounting.accounts.form.code.placeholder')}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="name">{t('accounting.accounts.form.name')}</label>
            <input
              id="name"
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder={t('accounting.accounts.form.name.placeholder')}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="type">{t('accounting.accounts.form.type')}</label>
            <select
              id="type"
              value={form.type}
              onChange={(e) => set('type', Number(e.target.value))}
            >
              {accountTypes.map((x) => (
                <option key={x.value} value={x.value}>{x.label}</option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="nature">{t('accounting.accounts.form.nature')}</label>
            <select
              id="nature"
              value={form.nature}
              onChange={(e) => set('nature', Number(e.target.value))}
            >
              {accountNatures.map((x) => (
                <option key={x.value} value={x.value}>{x.label}</option>
              ))}
            </select>
          </div>

          <div className="field field--span2">
            <label htmlFor="parentId">{t('accounting.accounts.form.parentId')}</label>
            <input
              id="parentId"
              value={form.parentId ?? ''}
              onChange={(e) => set('parentId', e.target.value || null)}
              placeholder={t('common.guid.placeholder')}
            />
          </div>
        </div>

        {error && <p className="form-error" style={{ marginTop: 14 }}>{error}</p>}

        <div className="form-actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? t('common.saving') : t('accounting.accounts.modal.create.submit')}
          </button>
        </div>
      </form>
    </Modal>
  );
}
