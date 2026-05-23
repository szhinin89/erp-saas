import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { BusinessPartnerDto } from '../types/businessPartner.types';

type Props = {
  partner: BusinessPartnerDto;
  saving: boolean;
  onClose: () => void;
  onSave: (payload: { creditLimit?: number | null; paymentDays: number; isBlocked: boolean }) => void;
};

export function MasterDataCompanySettingsModal({ partner, saving, onClose, onSave }: Props) {
  const [paymentDays, setPaymentDays] = useState('30');
  const [creditLimit, setCreditLimit] = useState('');
  const [isBlocked, setIsBlocked] = useState(false);

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form
        className="md-modal"
        onSubmit={(e) => {
          e.preventDefault();
          onSave({
            paymentDays: Number(paymentDays) || 0,
            creditLimit: creditLimit.trim() ? Number(creditLimit) : null,
            isBlocked,
          });
        }}
      >
        <h2>Condiciones por empresa</h2>
        <p className="md-modal-sub">
          {partner.legalName} · {partner.identificationNumber}
        </p>
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Días de pago" required>
            <input className="zh-input" type="number" min={0} value={paymentDays} onChange={(e) => setPaymentDays(e.target.value)} />
          </ZHField>
          <ZHField label="Límite de crédito">
            <input className="zh-input" type="number" min={0} step="0.01" value={creditLimit} onChange={(e) => setCreditLimit(e.target.value)} />
          </ZHField>
          <label className="md-page-check">
            <input type="checkbox" checked={isBlocked} onChange={(e) => setIsBlocked(e.target.checked)} />
            Bloqueado en esta empresa
          </label>
        </div>
        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose}>
            Cerrar
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? 'Guardando…' : 'Guardar'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
