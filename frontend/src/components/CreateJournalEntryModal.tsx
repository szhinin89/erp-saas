import { useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { accountingService, type JournalEntryLineRequest, type CreateJournalEntryRequest } from '../services/accountingService';
import type { Account } from '../types/accounting';
import './CreateJournalEntryModal.css';
import { useI18n } from '../i18n/i18n';

interface Props {
  accounts: Account[];
  onClose: () => void;
  onCreated: () => void;
}

const emptyLine = (): JournalEntryLineRequest => ({
  accountId: '',
  debitAmount: 0,
  creditAmount: 0,
  currency: 'USD',
});

export function CreateJournalEntryModal({ accounts, onClose, onCreated }: Props) {
  const { t } = useI18n();
  const today = new Date().toISOString().split('T')[0];

  const [form, setForm] = useState<Omit<CreateJournalEntryRequest, 'lines'>>({
    reference: '',
    date: today,
    description: '',
  });
  const [lines, setLines]   = useState<JournalEntryLineRequest[]>([emptyLine(), emptyLine()]);
  const [error, setError]   = useState('');
  const [loading, setLoading] = useState(false);

  const setField = (field: keyof typeof form, value: string) =>
    setForm((f) => ({ ...f, [field]: value }));

  const setLine = (i: number, field: keyof JournalEntryLineRequest, value: string | number) =>
    setLines((ls) => ls.map((l, idx) => idx === i ? { ...l, [field]: value } : l));

  const addLine = () => setLines((ls) => [...ls, emptyLine()]);
  const removeLine = (i: number) => setLines((ls) => ls.filter((_, idx) => idx !== i));

  const totalDebit  = lines.reduce((s, l) => s + (Number(l.debitAmount)  || 0), 0);
  const totalCredit = lines.reduce((s, l) => s + (Number(l.creditAmount) || 0), 0);
  const balanced    = Math.abs(totalDebit - totalCredit) < 0.001;

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!balanced) { setError(t('accounting.journal.modal.create.error.unbalanced')); return; }
    if (lines.some((l) => !l.accountId)) { setError(t('accounting.journal.modal.create.error.missingAccount')); return; }

    setError('');
    setLoading(true);
    try {
      await accountingService.createJournalEntry({
        ...form,
        lines: lines.map((l) => ({
          ...l,
          debitAmount:  Number(l.debitAmount),
          creditAmount: Number(l.creditAmount),
        })),
      });
      onCreated();
      onClose();
    } catch (err: unknown) {
      setError(
        (err as { response?: { data?: { error?: string } } })
          ?.response?.data?.error ?? t('accounting.journal.modal.create.error.generic')
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title={t('accounting.journal.modal.create.title')} onClose={onClose} width={680}>
      <form onSubmit={handleSubmit}>
        <div className="form-grid form-grid--2col" style={{ marginBottom: 16 }}>
          <div className="field">
            <label htmlFor="reference">{t('accounting.journal.form.reference')}</label>
            <input
              id="reference"
              value={form.reference}
              onChange={(e) => setField('reference', e.target.value)}
              placeholder={t('accounting.journal.form.reference.placeholder')}
              required
            />
          </div>

          <div className="field">
            <label htmlFor="date">{t('accounting.journal.form.date')}</label>
            <input
              id="date"
              type="date"
              value={form.date}
              onChange={(e) => setField('date', e.target.value)}
              required
            />
          </div>

          <div className="field field--span2">
            <label htmlFor="description">{t('accounting.journal.form.description')}</label>
            <input
              id="description"
              value={form.description}
              onChange={(e) => setField('description', e.target.value)}
              placeholder={t('accounting.journal.form.description.placeholder')}
              required
            />
          </div>
        </div>

        {/* Lines */}
        <div className="je-lines">
          <div className="je-lines-header">
            <span>{t('accounting.journal.lines.account')}</span>
            <span>{t('accounting.journal.lines.debit')}</span>
            <span>{t('accounting.journal.lines.credit')}</span>
            <span>{t('accounting.journal.lines.currency')}</span>
            <span></span>
          </div>

          {lines.map((line, i) => (
            <div key={i} className="je-line">
              <select
                value={line.accountId}
                onChange={(e) => setLine(i, 'accountId', e.target.value)}
                required
              >
                <option value="">{t('accounting.journal.lines.selectAccount')}</option>
                {accounts.map((a) => (
                  <option key={a.id} value={a.id}>{a.code} · {a.name}</option>
                ))}
              </select>

              <input
                type="number"
                min="0"
                step="0.01"
                value={line.debitAmount || ''}
                onChange={(e) => setLine(i, 'debitAmount', e.target.value)}
                placeholder={t('common.amount.placeholder')}
              />

              <input
                type="number"
                min="0"
                step="0.01"
                value={line.creditAmount || ''}
                onChange={(e) => setLine(i, 'creditAmount', e.target.value)}
                placeholder={t('common.amount.placeholder')}
              />

              <input
                value={line.currency}
                onChange={(e) => setLine(i, 'currency', e.target.value)}
                placeholder={t('common.currency.placeholder')}
                style={{ width: 60 }}
              />

              <button
                type="button"
                className="je-remove"
                onClick={() => removeLine(i)}
                disabled={lines.length <= 2}
              >✕</button>
            </div>
          ))}

          <button type="button" className="je-add-line" onClick={addLine}>
            {t('accounting.journal.lines.addLine')}
          </button>

          <div className={`je-totals ${!balanced ? 'je-totals--unbalanced' : ''}`}>
            <span>{t('accounting.journal.lines.totals')}</span>
            <span>{totalDebit.toFixed(2)}</span>
            <span>{totalCredit.toFixed(2)}</span>
            <span className="je-balance-label">
              {balanced ? t('accounting.journal.lines.balanced') : t('accounting.journal.lines.unbalanced')}
            </span>
          </div>
        </div>

        {error && <p className="form-error" style={{ marginTop: 14 }}>{error}</p>}

        <div className="form-actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="btn btn--primary" disabled={loading || !balanced}>
            {loading ? t('common.saving') : t('accounting.journal.modal.create.submit')}
          </button>
        </div>
      </form>
    </Modal>
  );
}
