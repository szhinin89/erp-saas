import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import type { CreateBusinessPartnerBody } from '../types/businessPartner.types';

type Props = {
  title: string;
  saving: boolean;
  defaultAsCustomer?: boolean;
  defaultAsSupplier?: boolean;
  onClose: () => void;
  onSubmit: (body: CreateBusinessPartnerBody) => void;
};

export function MasterDataBpFormModal({
  title,
  saving,
  defaultAsCustomer = false,
  defaultAsSupplier = false,
  onClose,
  onSubmit,
}: Props) {
  const [identificationType, setIdentificationType] = useState('RUC');
  const [identificationNumber, setIdentificationNumber] = useState('');
  const [legalName, setLegalName] = useState('');
  const [tradeName, setTradeName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      identificationType,
      identificationNumber: identificationNumber.trim(),
      legalName: legalName.trim(),
      tradeName: tradeName.trim() || null,
      email: email.trim() || null,
      phone: phone.trim() || null,
      asCustomer: defaultAsCustomer,
      asSupplier: defaultAsSupplier,
    });
  };

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={handleSubmit}>
        <h2>{title}</h2>
        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Tipo ID" required>
            <select className="zh-input" value={identificationType} onChange={(e) => setIdentificationType(e.target.value)}>
              <option value="RUC">RUC</option>
              <option value="CI">CI</option>
            </select>
          </ZHField>
          <ZHField label="Número" required>
            <input className="zh-input mono" value={identificationNumber} onChange={(e) => setIdentificationNumber(e.target.value)} required />
          </ZHField>
          <ZHField label="Razón social" required>
            <input className="zh-input" value={legalName} onChange={(e) => setLegalName(e.target.value)} required />
          </ZHField>
          <ZHField label="Nombre comercial">
            <input className="zh-input" value={tradeName} onChange={(e) => setTradeName(e.target.value)} />
          </ZHField>
          <ZHField label="Email">
            <input className="zh-input" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
          </ZHField>
          <ZHField label="Teléfono">
            <input className="zh-input" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </ZHField>
        </div>
        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={onClose}>
            Cancelar
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={saving}>
            {saving ? 'Guardando…' : 'Crear'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
