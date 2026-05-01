import { useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormAlert, ZHFormActions } from './zh/ZHForm';
import { ZHColSpan } from './zh/ZHLayout';
import { ZHModalHeader } from './zh/ZHModalHeader';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

const EMPTY: CreateAccountRequest = { code: '', name: '', type: 0, nature: 0, parentId: null };

export function CreateAccountModal({ onClose, onCreated }: Props) {
  const { t } = useI18n();
  const tenantId = useAuthStore((s) => s.user?.tenantId ?? '');
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
    <Modal
      title=""
      onClose={onClose}
      header={
        <ZHModalHeader
          title={t('accounting.accounts.modal.create.title')}
          subtitle={t('accounting.accounts.form.name')}
          onClose={onClose}
          closeLabel={t('common.close')}
        />
      }
    >
      <form onSubmit={handleSubmit}>
        <input type="hidden" name="tenantId" value={tenantId} />
        <div className="zh-form">
          <ZHFormBody>
            {error ? <ZHFormAlert type="error" message={t('common.errorPrefix')} detail={error} /> : null}

            <ZHFormSection title={t('accounting.accounts.modal.create.title')}>
              <ZHGrid cols={2}>
                <ZHField label={t('accounting.accounts.form.code')} required>
                  <input
                    id="code"
                    value={form.code}
                    onChange={(e) => set('code', e.target.value)}
                    placeholder={t('accounting.accounts.form.code.placeholder')}
                    required
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('accounting.accounts.form.name')} required>
                  <input
                    id="name"
                    value={form.name}
                    onChange={(e) => set('name', e.target.value)}
                    placeholder={t('accounting.accounts.form.name.placeholder')}
                    required
                    disabled={loading}
                  />
                </ZHField>

                <ZHField label={t('accounting.accounts.form.type')} required>
                  <select id="type" value={form.type} onChange={(e) => set('type', Number(e.target.value))} disabled={loading}>
                    {accountTypes.map((x) => (
                      <option key={x.value} value={x.value}>{x.label}</option>
                    ))}
                  </select>
                </ZHField>

                <ZHField label={t('accounting.accounts.form.nature')} required>
                  <select id="nature" value={form.nature} onChange={(e) => set('nature', Number(e.target.value))} disabled={loading}>
                    {accountNatures.map((x) => (
                      <option key={x.value} value={x.value}>{x.label}</option>
                    ))}
                  </select>
                </ZHField>

                <ZHColSpan span={2}>
                  <ZHField label={t('accounting.accounts.form.parentId')} hint={t('common.guid.placeholder')} hintType="info">
                    <input
                      id="parentId"
                      value={form.parentId ?? ''}
                      onChange={(e) => set('parentId', e.target.value || null)}
                      placeholder={t('common.guid.placeholder')}
                      disabled={loading}
                    />
                  </ZHField>
                </ZHColSpan>
              </ZHGrid>
            </ZHFormSection>

            <ZHFormActions
              onCancel={onClose}
              onDraft={undefined}
              onSave={undefined}
              disableDraft
              disableSave={loading}
              saveButtonType="submit"
              labels={{ cancel: t('common.cancel'), draft: t('common.saveDraft') ?? 'Guardar borrador', save: t('accounting.accounts.modal.create.submit') }}
            />
          </ZHFormBody>
        </div>
      </form>
    </Modal>
  );
}
