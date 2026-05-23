import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { BusinessPartnerDto, UpdateSupplierProfileBody } from '../types/businessPartner.types';

type Props = {
  partner: BusinessPartnerDto;
  saving: boolean;
  onClose: () => void;
  onSave: (body: UpdateSupplierProfileBody) => void;
};

export function MasterDataSupplierProfileModal({ partner, saving, onClose, onSave }: Props) {
  const [taxSupportCode, setTaxSupportCode] = useState('');
  const [retentionVatCode, setRetentionVatCode] = useState('');
  const [retentionIncomeCode, setRetentionIncomeCode] = useState('');
  const [paymentTerms, setPaymentTerms] = useState('');

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form
        className="md-modal"
        onSubmit={(e) => {
          e.preventDefault();
          onSave({
            defaultTaxSupportCode: taxSupportCode.trim() || null,
            defaultRetentionVatCode: retentionVatCode.trim() || null,
            defaultRetentionIncomeCode: retentionIncomeCode.trim() || null,
            paymentTerms: paymentTerms.trim() || null,
          });
        }}
      >
        <h2>Perfil SRI proveedor</h2>
        <p className="md-modal-sub">
          {partner.legalName} · {partner.identificationNumber}
        </p>
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Sustento tributario (SRI)">
            <input className="zh-input mono" value={taxSupportCode} onChange={(e) => setTaxSupportCode(e.target.value)} placeholder="01" maxLength={10} />
          </ZHField>
          <ZHField label="Ret. IVA (SRI)">
            <input className="zh-input mono" value={retentionVatCode} onChange={(e) => setRetentionVatCode(e.target.value)} placeholder="725" maxLength={10} />
          </ZHField>
          <ZHField label="Ret. Renta (SRI)">
            <input className="zh-input mono" value={retentionIncomeCode} onChange={(e) => setRetentionIncomeCode(e.target.value)} placeholder="303" maxLength={10} />
          </ZHField>
          <ZHField label="Condición de pago">
            <input className="zh-input" value={paymentTerms} onChange={(e) => setPaymentTerms(e.target.value)} placeholder="30D, COD…" maxLength={30} />
          </ZHField>
        </div>
        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose}>
            Cancelar
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? 'Guardando…' : 'Guardar'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
