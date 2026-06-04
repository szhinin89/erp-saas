import { useState, useRef } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { businessPartnerSchema } from '../../../schemas/masterData/businessPartnerSchema';
import { businessPartnerService } from '../api/businessPartnerService';
import { useSriIdTypes } from '../api/useSriIdTypes';
import type {
  BusinessPartnerSummaryDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import { PersonTypeEnum } from '../types/businessPartner.types';

type Step = 'search' | 'results' | 'create';

type CreateMode = {
  mode?:         'create';
  roleLabel:     string;   // "Cliente" | "Proveedor" | etc.
  onSubmit:      (body: CreateBusinessPartnerBody) => void;
  onAssignRole:  (id: string) => Promise<void>;
  onUpdate?:     never;
  initialValues?: never;
};

type EditMode = {
  mode:          'edit';
  roleLabel?:    never;
  onSubmit?:     never;
  onAssignRole?: never;
  onUpdate:      (body: UpdateBusinessPartnerBody) => void;
  initialValues: UpdateBusinessPartnerBody & { identificationType?: string; identificationNumber?: string };
};

type Props = (CreateMode | EditMode) & {
  title:   string;
  saving:  boolean;
  error?:  string | null;
  onClose: () => void;
};

const PERSON_TYPE_OPTIONS = [
  { value: PersonTypeEnum.Natural,      label: 'Natural (persona física)' },
  { value: PersonTypeEnum.Legal,        label: 'Jurídica (empresa/sociedad)' },
  { value: PersonTypeEnum.Government,   label: 'Gubernamental' },
  { value: PersonTypeEnum.Organization, label: 'Organización / ONG' },
];

/** Checks if a BP from search results already has the target role. */
function bpHasRole(bp: BusinessPartnerSummaryDto, roleLabel: string): boolean {
  // BP summary doesn't carry roles — use the flag we get from search context
  // (search was already filtered by role so this is for "already has this role" detection
  // via a detail load). For simplicity, always allow assign — backend returns 422 if already active.
  void bp; void roleLabel;
  return false;
}

export function MasterDataBpFormModal(props: Props) {
  const isEdit = props.mode === 'edit';
  const { options: idTypes, loading: loadingTypes } = useSriIdTypes();

  // ── Create-mode search state ───────────────────────────────────────────────
  const [step,         setStep]         = useState<Step>('search');
  const [query,        setQuery]        = useState('');
  const [searching,    setSearching]    = useState(false);
  const [searchError,  setSearchError]  = useState('');
  const [results,      setResults]      = useState<BusinessPartnerSummaryDto[]>([]);
  const [assigning,    setAssigning]    = useState<string | null>(null);

  // ── Form state ────────────────────────────────────────────────────────────
  const [identificationType,   setIdentificationType]   = useState('04');
  const [identificationNumber, setIdentificationNumber] = useState('');
  const [personType,           setPersonType]           = useState<number>(PersonTypeEnum.Legal);
  const [legalName,            setLegalName]            = useState(props.initialValues?.legalName ?? '');
  const [tradeName,            setTradeName]            = useState(props.initialValues?.tradeName ?? '');
  const [countryCode,          setCountryCode]          = useState(props.initialValues?.countryCode ?? 'EC');
  const [fieldErrors,          setFieldErrors]          = useState<Record<string, string>>({});
  const queryRef = useRef<HTMLInputElement>(null);

  const prefillFromQuery = (q: string) => {
    const t = q.trim();
    if (/^\d+$/.test(t)) setIdentificationNumber(t);
    else setLegalName(t);
  };

  const handleSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setSearching(true);
    setSearchError('');
    try {
      const rows = await businessPartnerService.search({ q, take: 10 });
      setResults(rows);
      setStep('results');
    } catch {
      setSearchError('Error al buscar. Intente de nuevo.');
    } finally {
      setSearching(false);
    }
  };

  const handleAssign = async (bp: BusinessPartnerSummaryDto) => {
    if (!props.onAssignRole) return;
    setAssigning(bp.id);
    try { await props.onAssignRole(bp.id); }
    finally { setAssigning(null); }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const parseResult = businessPartnerSchema.safeParse({
      identificationType,
      identificationNumber: identificationNumber.trim(),
      personType,
      legalName:   legalName.trim(),
      tradeName:   tradeName.trim() || undefined,
      countryCode: countryCode.trim() || undefined,
    });

    if (!parseResult.success) {
      const errs: Record<string, string> = {};
      for (const issue of parseResult.error.issues) {
        const field = issue.path[0] as string;
        if (!errs[field]) errs[field] = issue.message;
      }
      setFieldErrors(errs);
      return;
    }

    setFieldErrors({});

    if (isEdit) {
      props.onUpdate!({
        legalName:   legalName.trim(),
        personType,
        tradeName:   tradeName.trim()   || null,
        countryCode: countryCode.trim() || null,
      });
    } else {
      props.onSubmit!({
        identificationType,
        identificationNumber: identificationNumber.trim(),
        personType,
        legalName:   legalName.trim(),
        tradeName:   tradeName.trim()   || null,
        countryCode: countryCode.trim() || null,
      });
    }
  };

  const errClass = (f: string) => fieldErrors[f] ? ' zh-input--error' : '';

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={handleSubmit}>
        <h2>{props.title}</h2>
        {props.error && (
          <ZHPageNotice variant="error" message={props.error} className="md-modal-notice" />
        )}

        {/* ── EDIT MODE ─────────────────────────────────────────────────── */}
        {isEdit && (
          <>
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Razón social" required>
                <input className={`zh-input${errClass('legalName')}`} value={legalName}
                  onChange={(e) => { setLegalName(e.target.value); setFieldErrors((p) => ({ ...p, legalName: '' })); }}
                  disabled={props.saving} />
                {fieldErrors.legalName && <span className="md-field-error">{fieldErrors.legalName}</span>}
              </ZHField>
              <ZHField label="Nombre comercial">
                <input className="zh-input" value={tradeName}
                  onChange={(e) => setTradeName(e.target.value)} disabled={props.saving} />
              </ZHField>
              <ZHField label="Tipo de persona" required>
                <select className="zh-input" value={personType}
                  onChange={(e) => setPersonType(Number(e.target.value))} disabled={props.saving}>
                  {PERSON_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
              </ZHField>
              <ZHField label="País (ISO alpha-2)">
                <input className="zh-input mono" value={countryCode} maxLength={2}
                  onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 2))}
                  disabled={props.saving} placeholder="EC" />
              </ZHField>
            </div>
            <p className="md-info-hint">
              Para actualizar la identificación fiscal usa "Cambiar identificación" en el detalle del BP.
            </p>
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={props.onClose} disabled={props.saving}>Cancelar</ZHBtn>
              <ZHBtn variant="primary" type="submit" disabled={props.saving}>
                {props.saving ? 'Guardando…' : 'Guardar cambios'}
              </ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE: step SEARCH ───────────────────────────────────────── */}
        {!isEdit && step === 'search' && (
          <>
            <p className="md-search-hint">
              Busca primero. Si ya existe el BP lo asignamos como <strong>{props.roleLabel}</strong> directamente.
            </p>
            <div className="md-search-row">
              <input ref={queryRef} className="zh-input md-search-input"
                placeholder="RUC, cédula o razón social…" value={query} autoFocus
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void handleSearch(); } }}
                disabled={searching} />
              <ZHBtn variant="primary" type="button" onClick={() => void handleSearch()}
                disabled={searching || !query.trim()}>
                {searching ? 'Buscando…' : 'Buscar'}
              </ZHBtn>
            </div>
            {searchError && <span className="md-field-error">{searchError}</span>}
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={props.onClose}>Cancelar</ZHBtn>
              <ZHBtn variant="secondary" type="button" onClick={() => { prefillFromQuery(query); setStep('create'); }}>
                + Crear sin buscar
              </ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE: step RESULTS ─────────────────────────────────────── */}
        {!isEdit && step === 'results' && (
          <>
            <p className="md-search-hint">
              Resultados para <strong>"{query}"</strong>. Selecciona uno o crea nuevo.
            </p>
            {results.length === 0 ? (
              <p className="md-search-empty">No se encontraron registros.</p>
            ) : (
              <ul className="md-search-results">
                {results.map((bp) => {
                  const alreadyHas = bpHasRole(bp, props.roleLabel!);
                  const busy       = assigning === bp.id || props.saving;
                  return (
                    <li key={bp.id} className="md-search-result-item">
                      <div className="md-search-result-info">
                        <span className="md-search-result-name">{bp.legalName}</span>
                        <span className="md-search-result-id mono">{bp.identificationNumber}</span>
                      </div>
                      {alreadyHas ? (
                        <span className="md-search-already">Ya es {props.roleLabel}</span>
                      ) : (
                        <ZHBtn variant="primary" size="sm" type="button"
                          disabled={busy} onClick={() => void handleAssign(bp)}>
                          {busy ? 'Asignando…' : `+ ${props.roleLabel}`}
                        </ZHBtn>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={() => setStep('search')}>← Buscar</ZHBtn>
              <ZHBtn variant="primary" type="button" onClick={() => { prefillFromQuery(query); setStep('create'); }}>
                + Crear nuevo
              </ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE: step CREATE ───────────────────────────────────────── */}
        {!isEdit && step === 'create' && (
          <>
            <p className="md-search-hint">Nuevo <strong>{props.roleLabel}</strong>.</p>
            <div className="pg-form-grid pg-form-grid--2">
              <ZHField label="Tipo de identificación" required>
                <select className={`zh-input${errClass('identificationType')}`}
                  value={identificationType}
                  onChange={(e) => { setIdentificationType(e.target.value); setFieldErrors((p) => ({ ...p, identificationType: '' })); }}
                  disabled={props.saving || loadingTypes}>
                  {loadingTypes ? <option value="">Cargando…</option> : idTypes.map((t) => (
                    <option key={t.code} value={t.code}>{t.code} — {t.name}</option>
                  ))}
                </select>
                {fieldErrors.identificationType && <span className="md-field-error">{fieldErrors.identificationType}</span>}
              </ZHField>
              <ZHField label="Número" required>
                <input className={`zh-input mono${errClass('identificationNumber')}`}
                  value={identificationNumber}
                  onChange={(e) => { setIdentificationNumber(e.target.value); setFieldErrors((p) => ({ ...p, identificationNumber: '' })); }}
                  disabled={props.saving} />
                {fieldErrors.identificationNumber && <span className="md-field-error">{fieldErrors.identificationNumber}</span>}
              </ZHField>
              <ZHField label="Tipo de persona" required>
                <select className={`zh-input${errClass('personType')}`}
                  value={personType}
                  onChange={(e) => { setPersonType(Number(e.target.value)); setFieldErrors((p) => ({ ...p, personType: '' })); }}
                  disabled={props.saving}>
                  {PERSON_TYPE_OPTIONS.map((o) => (
                    <option key={o.value} value={o.value}>{o.label}</option>
                  ))}
                </select>
                {fieldErrors.personType && <span className="md-field-error">{fieldErrors.personType}</span>}
              </ZHField>
              <ZHField label="Razón social" required>
                <input className={`zh-input${errClass('legalName')}`} value={legalName}
                  onChange={(e) => { setLegalName(e.target.value); setFieldErrors((p) => ({ ...p, legalName: '' })); }}
                  disabled={props.saving} />
                {fieldErrors.legalName && <span className="md-field-error">{fieldErrors.legalName}</span>}
              </ZHField>
              <ZHField label="Nombre comercial">
                <input className="zh-input" value={tradeName}
                  onChange={(e) => setTradeName(e.target.value)} disabled={props.saving} />
              </ZHField>
              <ZHField label="País (ISO alpha-2)">
                <input className="zh-input mono" value={countryCode} maxLength={2}
                  onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 2))}
                  disabled={props.saving} placeholder="EC" />
              </ZHField>
            </div>
            <p className="md-info-hint">
              Tras crear, puedes agregar contactos (email, teléfono, representante legal) desde el detalle del BP.
            </p>
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button"
                onClick={() => setStep(results.length > 0 ? 'results' : 'search')}
                disabled={props.saving}>
                ← Volver
              </ZHBtn>
              <ZHBtn variant="primary" type="submit" disabled={props.saving}>
                {props.saving ? 'Guardando…' : `Crear ${props.roleLabel}`}
              </ZHBtn>
            </div>
          </>
        )}
      </form>
    </div>
  );
}
