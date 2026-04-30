import { useState, type FormEvent } from 'react';
import { Modal } from './Modal';
import { accountingService, type CreateAccountRequest } from '../services/accountingService';

interface Props {
  onClose: () => void;
  onCreated: () => void;
}

const ACCOUNT_TYPES = [
  { value: 0, label: 'Activo' },
  { value: 1, label: 'Pasivo' },
  { value: 2, label: 'Patrimonio' },
  { value: 3, label: 'Ingreso' },
  { value: 4, label: 'Gasto' },
];

const ACCOUNT_NATURES = [
  { value: 0, label: 'Débito' },
  { value: 1, label: 'Crédito' },
];

const EMPTY: CreateAccountRequest = { code: '', name: '', type: 0, nature: 0, parentId: null };

export function CreateAccountModal({ onClose, onCreated }: Props) {
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
          ?.response?.data?.error ?? 'Error al crear la cuenta'
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal title="Nueva cuenta contable" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        <div className="form-grid form-grid--2col">
          <div className="field">
            <label htmlFor="code">Código *</label>
            <input
              id="code"
              value={form.code}
              onChange={(e) => set('code', e.target.value)}
              placeholder="1.1.01"
              required
            />
          </div>

          <div className="field">
            <label htmlFor="name">Nombre *</label>
            <input
              id="name"
              value={form.name}
              onChange={(e) => set('name', e.target.value)}
              placeholder="Caja general"
              required
            />
          </div>

          <div className="field">
            <label htmlFor="type">Tipo *</label>
            <select
              id="type"
              value={form.type}
              onChange={(e) => set('type', Number(e.target.value))}
            >
              {ACCOUNT_TYPES.map((t) => (
                <option key={t.value} value={t.value}>{t.label}</option>
              ))}
            </select>
          </div>

          <div className="field">
            <label htmlFor="nature">Naturaleza *</label>
            <select
              id="nature"
              value={form.nature}
              onChange={(e) => set('nature', Number(e.target.value))}
            >
              {ACCOUNT_NATURES.map((n) => (
                <option key={n.value} value={n.value}>{n.label}</option>
              ))}
            </select>
          </div>

          <div className="field field--span2">
            <label htmlFor="parentId">Cuenta padre (ID opcional)</label>
            <input
              id="parentId"
              value={form.parentId ?? ''}
              onChange={(e) => set('parentId', e.target.value || null)}
              placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
            />
          </div>
        </div>

        {error && <p className="form-error" style={{ marginTop: 14 }}>{error}</p>}

        <div className="form-actions">
          <button type="button" className="btn btn--ghost" onClick={onClose}>Cancelar</button>
          <button type="submit" className="btn btn--primary" disabled={loading}>
            {loading ? 'Guardando...' : 'Crear cuenta'}
          </button>
        </div>
      </form>
    </Modal>
  );
}
