import { ZHField } from '../../../components/zh/ZHForm';
import { useSriIdTypes } from '../api/useSriIdTypes';
import { PersonTypeEnum } from '../types/businessPartner.types';

const PERSON_TYPE_OPTIONS = [
  { value: PersonTypeEnum.Natural,      label: 'Natural (persona física)' },
  { value: PersonTypeEnum.Legal,        label: 'Jurídica (empresa/sociedad)' },
  { value: PersonTypeEnum.Government,   label: 'Gubernamental' },
  { value: PersonTypeEnum.Organization, label: 'Organización / ONG' },
];

/** Shared form field state — V2: sin email, phone, legalRepresentativeName */
export type BpFormFieldsState = {
  identificationType:   string;
  identificationNumber: string;
  personType:           number;
  legalName:            string;
  tradeName:            string;
  countryCode:          string;
};

type Setters = {
  setIdentificationType:   (v: string) => void;
  setIdentificationNumber: (v: string) => void;
  setPersonType:           (v: number) => void;
  setLegalName:            (v: string) => void;
  setTradeName:            (v: string) => void;
  setCountryCode:          (v: string) => void;
  setFieldErrors:          (fn: (p: Record<string, string>) => Record<string, string>) => void;
};

type Props = BpFormFieldsState & Setters & {
  fieldErrors: Record<string, string>;
  saving:      boolean;
  /** 'identity' shows all fields; 'review' shows read-only summary. */
  section?: 'identity' | 'review';
};

export function MasterDataBpFormFields({
  identificationType, setIdentificationType,
  identificationNumber, setIdentificationNumber,
  personType, setPersonType,
  legalName, setLegalName,
  tradeName, setTradeName,
  countryCode, setCountryCode,
  fieldErrors, setFieldErrors,
  saving,
  section = 'identity',
}: Props) {
  const { options: idTypes, loading: loadingTypes } = useSriIdTypes();

  const clr = (f: string) => setFieldErrors((p) => ({ ...p, [f]: '' }));
  const err = (f: string) => fieldErrors[f] ? ` zh-input--error` : '';

  if (section === 'review') {
    return (
      <dl className="prd-review-grid">
        <dt>Tipo de identificación</dt>
        <dd className="mono">{identificationType}</dd>
        <dt>Número</dt>
        <dd className="mono">{identificationNumber}</dd>
        <dt>Tipo de persona</dt>
        <dd>{PERSON_TYPE_OPTIONS.find((o) => o.value === personType)?.label ?? personType}</dd>
        <dt>Razón social</dt>
        <dd>{tradeName.trim() || legalName}</dd>
        {tradeName.trim() && <><dt>Nombre legal</dt><dd>{legalName}</dd></>}
        <dt>País</dt>
        <dd className="mono">{countryCode || 'EC'}</dd>
      </dl>
    );
  }

  return (
    <div className="pg-form-grid pg-form-grid--2">
      <ZHField label="Tipo de identificación" required>
        <select className={`zh-input${err('identificationType')}`}
          value={identificationType}
          onChange={(e) => { setIdentificationType(e.target.value); clr('identificationType'); }}
          disabled={saving || loadingTypes}>
          {loadingTypes
            ? <option value="">Cargando…</option>
            : idTypes.map((t) => <option key={t.code} value={t.code}>{t.code} — {t.name}</option>)
          }
        </select>
        {fieldErrors.identificationType && <span className="md-field-error">{fieldErrors.identificationType}</span>}
      </ZHField>

      <ZHField label="Número de identificación" required>
        <input className={`zh-input mono${err('identificationNumber')}`}
          value={identificationNumber}
          onChange={(e) => { setIdentificationNumber(e.target.value); clr('identificationNumber'); }}
          disabled={saving} />
        {fieldErrors.identificationNumber && <span className="md-field-error">{fieldErrors.identificationNumber}</span>}
      </ZHField>

      <ZHField label="Tipo de persona" required>
        <select className={`zh-input${err('personType')}`}
          value={personType}
          onChange={(e) => { setPersonType(Number(e.target.value)); clr('personType'); }}
          disabled={saving}>
          {PERSON_TYPE_OPTIONS.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
        {fieldErrors.personType && <span className="md-field-error">{fieldErrors.personType}</span>}
      </ZHField>

      <ZHField label="Razón social" required>
        <input className={`zh-input${err('legalName')}`}
          value={legalName}
          onChange={(e) => { setLegalName(e.target.value); clr('legalName'); }}
          disabled={saving} />
        {fieldErrors.legalName && <span className="md-field-error">{fieldErrors.legalName}</span>}
      </ZHField>

      <ZHField label="Nombre comercial">
        <input className="zh-input" value={tradeName}
          onChange={(e) => setTradeName(e.target.value)} disabled={saving} />
      </ZHField>

      <ZHField label="País (ISO alpha-2)">
        <input className="zh-input mono" value={countryCode} maxLength={2}
          onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 2))}
          disabled={saving} placeholder="EC" />
      </ZHField>
    </div>
  );
}
