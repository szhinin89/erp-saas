import { ZHField } from '../../../components/zh/ZHForm';
import { useSriIdTypes } from '../api/useSriIdTypes';

export type BpFormFieldsState = {
  identificationType: string;
  identificationNumber: string;
  legalName: string;
  tradeName: string;
  /** Representante legal — solo visible y editable cuando identificationType === '04' (RUC). */
  legalRepresentativeName: string;
  email: string;
  phone: string;
  countryCode: string;
};

type Setters = {
  setIdentificationType: (v: string) => void;
  setIdentificationNumber: (v: string) => void;
  setLegalName: (v: string) => void;
  setTradeName: (v: string) => void;
  setLegalRepresentativeName: (v: string) => void;
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
  legalRepresentativeName,
  setLegalRepresentativeName,
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
  const { options: idTypes, loading: loadingTypes } = useSriIdTypes();
  const showIdentity = section === 'identity' || section === 'all';
  const showContact  = section === 'contact'  || section === 'all';
  const isRuc = identificationType === '04';

  return (
    <div className="pg-form-grid pg-form-grid--2">
      {showIdentity && (
        <>
          <ZHField label="Tipo de identificación" required>
            <select
              className="zh-input"
              value={identificationType}
              onChange={(e) => {
                setIdentificationType(e.target.value);
                setFieldErrors((p) => ({ ...p, identificationType: '' }));
              }}
              disabled={saving || loadingTypes}
            >
              {loadingTypes ? (
                <option value="">Cargando…</option>
              ) : (
                idTypes.map((t) => (
                  <option key={t.code} value={t.code}>
                    {t.code} — {t.name}
                  </option>
                ))
              )}
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

          {/* Representante legal — solo visible para RUC (Persona Jurídica, código 04) */}
          {isRuc && (
            <ZHField label="Representante legal">
              <input
                className="zh-input"
                value={legalRepresentativeName}
                onChange={(e) => setLegalRepresentativeName(e.target.value)}
                disabled={saving}
                placeholder="Nombre completo del representante"
              />
            </ZHField>
          )}

          <ZHField label="País (código ISO)">
            <input
              className="zh-input mono"
              value={countryCode}
              onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 3))}
              disabled={saving}
              placeholder="ECU"
              maxLength={3}
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
