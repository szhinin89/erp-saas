import { ZHField } from '../../../components/zh/ZHForm';

export type BpFormFieldsState = {
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName: string;
  email: string;
  phone: string;
  countryCode: string;
};

type Setters = {
  setIdentificationType: (v: string) => void;
  setIdentificationNumber: (v: string) => void;
  setLegalName: (v: string) => void;
  setTradeName: (v: string) => void;
  setEmail: (v: string) => void;
  setPhone: (v: string) => void;
  setCountryCode: (v: string) => void;
  setFieldErrors: (fn: (p: Record<string, string>) => Record<string, string>) => void;
};

type Props = BpFormFieldsState & Setters & {
  fieldErrors: Record<string, string>;
  saving: boolean;
  /** 'identity' | 'contact' | 'all' */
  section?: 'identity' | 'contact' | 'all';
};

export function MasterDataBpFormFields({
  identificationType,
  setIdentificationType,
  identificationNumber,
  setIdentificationNumber,
  legalName,
  setLegalName,
  tradeName,
  setTradeName,
  email,
  setEmail,
  phone,
  setPhone,
  countryCode,
  setCountryCode,
  fieldErrors,
  setFieldErrors,
  saving,
  section = 'all',
}: Props) {
  const showIdentity = section === 'identity' || section === 'all';
  const showContact = section === 'contact' || section === 'all';

  return (
    <div className="pg-form-grid pg-form-grid--2">
      {showIdentity && (
        <>
          <ZHField label="Tipo ID" required>
            <select
              className="zh-input"
              value={identificationType}
              onChange={(e) => {
                setIdentificationType(e.target.value);
                setFieldErrors((p) => ({ ...p, identificationType: '' }));
              }}
              disabled={saving}
            >
              <option value="RUC">RUC</option>
              <option value="CI">CI</option>
              <option value="PASSPORT">PASSPORT</option>
              <option value="OTHER">OTHER</option>
            </select>
            {fieldErrors.identificationType && (
              <span className="md-field-error">{fieldErrors.identificationType}</span>
            )}
          </ZHField>
          <ZHField label="Número" required>
            <input
              className={`zh-input mono${fieldErrors.identificationNumber ? ' zh-input--error' : ''}`}
              value={identificationNumber}
              onChange={(e) => {
                setIdentificationNumber(e.target.value);
                setFieldErrors((p) => ({ ...p, identificationNumber: '' }));
              }}
              disabled={saving}
            />
            {fieldErrors.identificationNumber && (
              <span className="md-field-error">{fieldErrors.identificationNumber}</span>
            )}
          </ZHField>
          <ZHField label="Razón social" required>
            <input
              className={`zh-input${fieldErrors.legalName ? ' zh-input--error' : ''}`}
              value={legalName}
              onChange={(e) => {
                setLegalName(e.target.value);
                setFieldErrors((p) => ({ ...p, legalName: '' }));
              }}
              disabled={saving}
            />
            {fieldErrors.legalName && (
              <span className="md-field-error">{fieldErrors.legalName}</span>
            )}
          </ZHField>
          <ZHField label="Nombre comercial">
            <input
              className="zh-input"
              value={tradeName}
              onChange={(e) => setTradeName(e.target.value)}
              disabled={saving}
            />
          </ZHField>
          <ZHField label="País (código ISO)">
            <input
              className="zh-input mono"
              value={countryCode}
              onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 2))}
              disabled={saving}
              placeholder="EC"
              maxLength={2}
            />
          </ZHField>
        </>
      )}
      {showContact && (
        <>
          <ZHField label="Email">
            <input
              className={`zh-input${fieldErrors.email ? ' zh-input--error' : ''}`}
              type="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                setFieldErrors((p) => ({ ...p, email: '' }));
              }}
              disabled={saving}
            />
            {fieldErrors.email && <span className="md-field-error">{fieldErrors.email}</span>}
          </ZHField>
          <ZHField label="Teléfono">
            <input
              className={`zh-input${fieldErrors.phone ? ' zh-input--error' : ''}`}
              value={phone}
              onChange={(e) => {
                setPhone(e.target.value);
                setFieldErrors((p) => ({ ...p, phone: '' }));
              }}
              disabled={saving}
            />
            {fieldErrors.phone && <span className="md-field-error">{fieldErrors.phone}</span>}
          </ZHField>
        </>
      )}
    </div>
  );
}
