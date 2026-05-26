import { useRef, useState } from 'react';
import { useFieldArray, useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Modal } from './Modal';
import { accountingService, type JournalEntryLineRequest } from '../modules/accounting/api/accountingService';
import type { Account } from '../types/accounting';
import './CreateJournalEntryModal.css';
import { useI18n } from '../i18n/i18n';
import { useAuthStore } from '../store/authStore';
import { ZHFormBody, ZHFormSection, ZHGrid, ZHField, ZHFormActions, ZHBtn } from './zh/ZHForm';
import { ZhDecimalInput, ZhDateInput } from './zh/inputs';
import { ZHPageNotice } from './zh/ZHPageNotice';
import { ZHColSpan } from './zh/ZHLayout';
import { ZHModalHeader } from './zh/ZHModalHeader';
import { journalEntryFormSchema, type JournalEntryFormValues } from '../schemas/accounting/journalEntrySchema';

interface Props {
  accounts: Account[];
  onClose: () => void;
  onCreated: () => void;
}

const emptyLine = (): JournalEntryFormValues['lines'][number] => ({
  accountId: '',
  debitAmount: 0,
  creditAmount: 0,
  currency: 'USD',
});

export function CreateJournalEntryModal({ accounts, onClose, onCreated }: Props) {
  const { t } = useI18n();
  const subscriberId = useAuthStore((s) => s.user?.subscriberId ?? '');
  const today = new Date().toISOString().split('T')[0];

  const formRef = useRef<HTMLFormElement>(null);
  const [apiError, setApiError] = useState('');

  const {
    control,
    register,
    handleSubmit,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<JournalEntryFormValues>({
    resolver: zodResolver(journalEntryFormSchema),
    defaultValues: {
      reference: '',
      date: today,
      description: '',
      lines: [emptyLine(), emptyLine()],
    },
  });

  const { fields, append, remove } = useFieldArray({ control, name: 'lines' });

  const linesWatch = watch('lines');
  const totalDebit = linesWatch.reduce((s, l) => s + (Number(l.debitAmount) || 0), 0);
  const totalCredit = linesWatch.reduce((s, l) => s + (Number(l.creditAmount) || 0), 0);
  const balanced = Math.abs(totalDebit - totalCredit) < 0.001;
  const anyMissingAccount = linesWatch.some((l) => !l.accountId);

  const onValid = async (data: JournalEntryFormValues) => {
    setApiError('');
    try {
      await accountingService.createJournalEntry({
        reference: data.reference,
        date: data.date,
        description: data.description,
        lines: data.lines.map((l) => ({
          ...(l as unknown as JournalEntryLineRequest),
          debitAmount: Number(l.debitAmount),
          creditAmount: Number(l.creditAmount),
        })),
      });
      onCreated();
      onClose();
    } catch (err: unknown) {
      setApiError(
        (err as { response?: { data?: { error?: string } } })?.response?.data?.error ??
          t('finance.journal.modal.create.error.generic')
      );
    }
  };

  return (
    <Modal
      title=""
      onClose={onClose}
      width={680}
      header={
        <ZHModalHeader
          title={t('finance.journal.modal.create.title')}
          subtitle={t('finance.journal.form.description')}
          onClose={onClose}
          closeLabel={t('common.close')}
          right={
            <ZHBtn
              variant="primary"
              size="md"
              type="button"
              disabled={isSubmitting || !balanced || anyMissingAccount}
              onClick={() => formRef.current?.requestSubmit()}
            >
              {isSubmitting ? t('common.saving') : t('finance.journal.confirmEntry')}
            </ZHBtn>
          }
        />
      }
    >
      <form ref={formRef} onSubmit={handleSubmit(onValid)} noValidate>
        <input type="hidden" name="subscriberId" value={subscriberId} />
        <div className="zh-form">
          <ZHFormBody>
            {apiError ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={apiError} /> : null}
            {errors.lines?.message ? (
              <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={errors.lines.message} />
            ) : null}

            <ZHFormSection title={t('finance.journal.modal.create.title')}>
              <ZHGrid cols={2}>
                <ZHField label={t('finance.journal.form.reference')} required fieldError={errors.reference?.message}>
                  <input
                    id="reference"
                    placeholder={t('finance.journal.form.reference.placeholder')}
                    disabled={isSubmitting}
                    {...register('reference')}
                  />
                </ZHField>
                <ZHField label={t('finance.journal.form.date')} required fieldError={errors.date?.message}>
                  <ZhDateInput id="date" disabled={isSubmitting} {...register('date')} />
                </ZHField>
                <ZHColSpan span={2}>
                  <ZHField label={t('finance.journal.form.description')} required fieldError={errors.description?.message}>
                    <input
                      id="description"
                      placeholder={t('finance.journal.form.description.placeholder')}
                      disabled={isSubmitting}
                      {...register('description')}
                    />
                  </ZHField>
                </ZHColSpan>
              </ZHGrid>
            </ZHFormSection>

            <div className="je-lines">
              <div className="je-lines-header">
                <span>{t('finance.journal.lines.account')}</span>
                <span>{t('finance.journal.lines.debit')}</span>
                <span>{t('finance.journal.lines.credit')}</span>
                <span>{t('finance.journal.lines.currency')}</span>
                <span></span>
              </div>

              {fields.map((field, i) => (
                <div key={field.id} className="je-line">
                  <div className="je-line-account">
                    <select disabled={isSubmitting} {...register(`lines.${i}.accountId` as const)}>
                      <option value="">{t('finance.journal.lines.selectAccount')}</option>
                      {accounts.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.code} · {a.name}
                        </option>
                      ))}
                    </select>
                    {errors.lines?.[i]?.accountId?.message ? (
                      <p className="zh-field-hint zh-field-hint--error">{errors.lines[i]?.accountId?.message}</p>
                    ) : null}
                  </div>

                  <ZhDecimalInput
                    decimals={2}
                    positiveOnly
                    placeholder={t('common.amount.placeholder')}
                    disabled={isSubmitting}
                    {...register(`lines.${i}.debitAmount` as const)}
                  />

                  <ZhDecimalInput
                    decimals={2}
                    positiveOnly
                    placeholder={t('common.amount.placeholder')}
                    disabled={isSubmitting}
                    {...register(`lines.${i}.creditAmount` as const)}
                  />

                  <input
                    placeholder={t('common.currency.placeholder')}
                    className="je-currency"
                    disabled={isSubmitting}
                    {...register(`lines.${i}.currency` as const)}
                  />

                  <button type="button" className="je-remove" onClick={() => remove(i)} disabled={fields.length <= 2}>
                    ✕
                  </button>
                </div>
              ))}

              <ZHBtn variant="secondary" size="md" type="button" className="je-add-line" onClick={() => append(emptyLine())}>
                {t('finance.journal.lines.addLine')}
              </ZHBtn>

              <div className={`je-totals ${!balanced ? 'je-totals--unbalanced' : ''}`}>
                <span>{t('finance.journal.lines.totals')}</span>
                <span>{totalDebit.toFixed(2)}</span>
                <span>{totalCredit.toFixed(2)}</span>
                <span className="je-balance-label">
                  {balanced ? t('finance.journal.lines.balanced') : t('finance.journal.lines.unbalanced')}
                </span>
              </div>
            </div>
            <ZHFormActions buttonSize="md" hideSave hideDraft onCancel={onClose} labels={{ cancel: t('common.cancel') }} />
          </ZHFormBody>
        </div>
      </form>
    </Modal>
  );
}
