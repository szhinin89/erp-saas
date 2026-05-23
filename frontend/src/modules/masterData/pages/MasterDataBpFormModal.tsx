import { useState } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import type { CreateBusinessPartnerBody, UpdateBusinessPartnerBody } from '../types/businessPartner.types';

type CreateMode = {
  mode?: 'create';
  defaultAsCustomer?: boolean;
  defaultAsSupplier?: boolean;
  onSubmit: (body: CreateBusinessPartnerBody) => void;
  onUpdate?: never;
  initialValues?: never;
};

type EditMode = {
  mode: 'edit';
  defaultAsCustomer?: never;
  defaultAsSupplier?: never;
  onSubmit?: never;
  onUpdate: (body: UpdateBusinessPartnerBody) => void;
  initialValues: UpdateBusinessPartnerBody;
};

type Props = (CreateMode | EditMode) & {
  title: string;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
};

export function MasterDataBpFormModal(props: Props) {
  const isEdit = props.mode === 'edit';

  const [identificationType, setIdentificationType] = useState(
    props.initialValues?.identificationType ?? 'RUC',
  );
  const [identificationNumber, setIdentificationNumber] = useState(
    props.initialValues?.identificationNumber ?? '',
  );
  const [legalName, setLegalName]   = useState(props.initialValues?.legalName ?? '');
  const [tradeName, setTradeName]   = useState(props.initialValues?.tradeName ?? '');
  const [email, setEmail]           = useState(props.initialValues?.email ?? '');
  const [phone, setPhone]           = useState(props.initialValues?.phone ?? '');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    const body = {
      identificationType,
      identificationNumber: identificationNumber.trim(),
      legalName: legalName.trim(),
      tradeName: (tradeName as string).trim() || null,
      email:     (email as string).trim()     || null,
      phone:     (phone as string).trim()     || null,
    };
    if (isEdit) {
      props.onUpdate(body);
    } else {
      props.onSubmit!({
        ...body,
        asCustomer: props.defaultAsCustomer ?? false,
        asSupplier: props.defaultAsSupplier ?? false,
      });
    }
  };

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={handleSubmit}>
        <h2>{props.title}</h2>

        {props.error && (
          <ZHPageNotice variant="error" message={props.error} className="md-modal-notice" />
        )}

        <div className="pg-form-grid pg-form-grid--2">
          <ZHField label="Tipo ID" required>
            <select
              className="zh-input"
              value={identificationType}
              onChange={(e) => setIdentificationType(e.target.value)}
              disabled={props.saving}
            >
              <option value="RUC">RUC</option>
              <option value="CI">CI</option>
              <option value="PASSPORT">PASSPORT</option>
              <option value="OTHER">OTHER</option>
            </select>
          </ZHField>
          <ZHField label="Número" required>
            <input
              className="zh-input mono"
              value={identificationNumber}
              onChange={(e) => setIdentificationNumber(e.target.value)}
              disabled={props.saving}
              required
            />
          </ZHField>
          <ZHField label="Razón social" required>
            <input
              className="zh-input"
              value={legalName}
              onChange={(e) => setLegalName(e.target.value)}
              disabled={props.saving}
              required
            />
          </ZHField>
          <ZHField label="Nombre comercial">
            <input
              className="zh-input"
              value={tradeName as string}
              onChange={(e) => setTradeName(e.target.value)}
              disabled={props.saving}
            />
          </ZHField>
          <ZHField label="Email">
            <input
              className="zh-input"
              type="email"
              value={email as string}
              onChange={(e) => setEmail(e.target.value)}
              disabled={props.saving}
            />
          </ZHField>
          <ZHField label="Teléfono">
            <input
              className="zh-input"
              value={phone as string}
              onChange={(e) => setPhone(e.target.value)}
              disabled={props.saving}
            />
          </ZHField>
        </div>

        <div className="md-modal-actions">
          <ZHBtn variant="ghost" type="button" onClick={props.onClose} disabled={props.saving}>
            Cancelar
          </ZHBtn>
          <ZHBtn variant="primary" type="submit" disabled={props.saving}>
            {props.saving ? 'Guardando…' : isEdit ? 'Guardar cambios' : 'Crear'}
          </ZHBtn>
        </div>
      </form>
    </div>
  );
}
