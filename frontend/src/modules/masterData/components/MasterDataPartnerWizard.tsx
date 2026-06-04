/**
 * MasterDataPartnerWizard V2 — 3-step flow.
 *
 * STEPS: 1=Search, 2=Identity, 3=Review
 * ELIMINADO: step 4 (Contact/email/phone) — los contactos van en BusinessPartnerContact (POST /contacts).
 *
 * V2 changes:
 *   - alreadyHasRole: simplificado — el backend devuelve 422 si el rol ya está activo.
 *   - PersonType: nuevo campo obligatorio en step 2.
 *   - Sin email/phone/legalRepresentativeName.
 *   - onSubmitCreate: ya no incluye asCustomer/asSupplier — role se asigna por separado.
 */
import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '../../../i18n/i18n';
import { businessPartnerSchema } from '../../../schemas/masterData/businessPartnerSchema';
import { businessPartnerService } from '../api/businessPartnerService';
import type {
  BusinessPartnerSummaryDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import { PersonTypeEnum } from '../types/businessPartner.types';
import { MasterDataBpFormFields } from './MasterDataBpFormFields';

type StepId = 1 | 2 | 3;
type Role = 'customer' | 'supplier';

interface Props {
  role:            Role;
  draftKey:        string;
  submitting:      boolean;
  wizardError?:    string | null;
  editingPartner:  BusinessPartnerSummaryDto | null;
  onSubmitCreate:  (body: CreateBusinessPartnerBody) => Promise<boolean>;
  onSubmitUpdate:  (body: UpdateBusinessPartnerBody) => Promise<boolean>;
  onAssignRole:    (id: string) => Promise<boolean>;
  onCancel:        () => void;
  onAssignSuccess?: () => void;
}

const STEPS = [
  { id: 1 as StepId, icon: 'search',       labelFb: 'Buscar y asignar' },
  { id: 2 as StepId, icon: 'badge',        labelFb: 'Identificación'   },
  { id: 3 as StepId, icon: 'check_circle', labelFb: 'Revisar y guardar' },
];

interface DraftSnapshot { identificationNumber: string; legalName: string; savedAt: string; }

function saveDraft(key: string, identificationNumber: string, legalName: string) {
  localStorage.setItem(key, JSON.stringify({ identificationNumber, legalName, savedAt: new Date().toISOString() }));
}
function loadDraft(key: string): DraftSnapshot | null {
  try { return JSON.parse(localStorage.getItem(key) ?? 'null'); } catch { return null; }
}
function clearDraft(key: string) { localStorage.removeItem(key); }

export function MasterDataPartnerWizard({
  role, draftKey, submitting, wizardError, editingPartner,
  onSubmitCreate, onSubmitUpdate, onAssignRole, onCancel, onAssignSuccess,
}: Props) {
  const { t }   = useI18n();
  const isEdit  = !!editingPartner;
  const [step, setStep] = useState<StepId>(isEdit ? 2 : 1);

  const [query,       setQuery]      = useState('');
  const [searching,   setSearching]  = useState(false);
  const [searchError, setSearchError]= useState('');
  const [results,     setResults]    = useState<BusinessPartnerSummaryDto[]>([]);
  const [assigning,   setAssigning]  = useState<string | null>(null);
  const [hasSearched, setHasSearched]= useState(false);
  const [notFound,    setNotFound]   = useState<string | null>(null);
  const [draftBanner, setDraftBanner]= useState<DraftSnapshot | null>(null);
  const queryRef = useRef<HTMLInputElement>(null);

  const roleLabel = role === 'customer' ? 'Cliente' : 'Proveedor';

  // Form state
  const [identificationType,   setIdentificationType]   = useState('04');
  const [identificationNumber, setIdentificationNumber] = useState('');
  const [personType,           setPersonType]           = useState<number>(PersonTypeEnum.Legal);
  const [legalName,            setLegalName]            = useState(editingPartner?.legalName ?? '');
  const [tradeName,            setTradeName]            = useState(editingPartner?.tradeName ?? '');
  const [countryCode,          setCountryCode]          = useState(editingPartner?.countryCode ?? 'EC');
  const [fieldErrors,          setFieldErrors]          = useState<Record<string, string>>({});

  useEffect(() => {
    if (!isEdit) { const d = loadDraft(draftKey); if (d) setDraftBanner(d); }
  }, [draftKey, isEdit]);

  const prefill = (q: string) => {
    const s = q.trim();
    if (/^\d+$/.test(s)) setIdentificationNumber(s); else setLegalName(s);
  };

  const validate = useCallback((): boolean => {
    const parsed = businessPartnerSchema.safeParse({
      identificationType, identificationNumber: identificationNumber.trim(),
      personType, legalName: legalName.trim(),
      tradeName: tradeName.trim() || undefined,
      countryCode: countryCode.trim() || undefined,
    });
    if (!parsed.success) {
      const errs: Record<string, string> = {};
      for (const i of parsed.error.issues) { const f = i.path[0] as string; if (!errs[f]) errs[f] = i.message; }
      setFieldErrors(errs);
      return false;
    }
    setFieldErrors({});
    return true;
  }, [identificationType, identificationNumber, personType, legalName, tradeName, countryCode]);

  const handleSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setSearching(true); setSearchError('');
    try {
      const rows = await businessPartnerService.search({ q, take: 10 });
      setResults(rows); setHasSearched(true);
      if (rows.length === 0) { prefill(q); setNotFound(q); setStep(2); }
    } catch {
      setSearchError(t('masterdata.wizard.search.error', 'Error al buscar. Intente de nuevo.'));
    } finally { setSearching(false); }
  };

  const handleAssign = async (bp: BusinessPartnerSummaryDto) => {
    setAssigning(bp.id);
    try { const ok = await onAssignRole(bp.id); if (ok) onAssignSuccess?.(); }
    finally { setAssigning(null); }
  };

  const goNext = () => { if (step === 2 && !validate()) return; if (step < 3) setStep((step + 1) as StepId); };
  const goPrev = () => { if (step > 1) setStep((step - 1) as StepId); };

  const buildCreateBody = (): CreateBusinessPartnerBody => ({
    identificationType,
    identificationNumber: identificationNumber.trim(),
    personType,
    legalName:   legalName.trim(),
    tradeName:   tradeName.trim()   || null,
    countryCode: countryCode.trim() || null,
  });

  const buildUpdateBody = (): UpdateBusinessPartnerBody => ({
    legalName:   legalName.trim(),
    personType,
    tradeName:   tradeName.trim()   || null,
    countryCode: countryCode.trim() || null,
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (step !== 3 || !validate()) return;
    if (isEdit) {
      const ok = await onSubmitUpdate(buildUpdateBody());
      if (ok) clearDraft(draftKey);
    } else {
      const ok = await onSubmitCreate(buildCreateBody());
      if (ok) clearDraft(draftKey);
    }
  };

  return (
    <div className="prd-wizard prd-fadein">
      {/* Progress */}
      <div className="prd-wiz-progress">
        {STEPS.map((s) => {
          if (isEdit && s.id === 1) return null;
          const done = s.id < step; const active = s.id === step;
          return (
            <button key={s.id} type="button"
              className={`prd-wiz-step ${active ? 'prd-wiz-step--active' : ''} ${done ? 'prd-wiz-step--done' : ''}`}
              disabled={!done} onClick={() => done && setStep(s.id)}>
              <span className="prd-wiz-step__num">
                {done ? <span className="material-symbols-outlined" style={{ fontSize: 16 }}>check</span> : s.id}
              </span>
              <span className="material-symbols-outlined prd-wiz-step__icon">{s.icon}</span>
              <span className="prd-wiz-step__label">{s.labelFb}</span>
            </button>
          );
        })}
      </div>

      {draftBanner && !isEdit && (
        <div className="prd-draft-banner">
          <span>Borrador guardado: <strong>{draftBanner.legalName || draftBanner.identificationNumber}</strong></span>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => { const d = loadDraft(draftKey); if (d) { setIdentificationNumber(d.identificationNumber); setLegalName(d.legalName); setDraftBanner(null); setStep(2); } }}>
            Restaurar
          </button>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm"
            onClick={() => { clearDraft(draftKey); setDraftBanner(null); }}>Descartar</button>
        </div>
      )}

      {wizardError && (
        <div className="prd-wiz-error-banner" role="alert">
          <span className="material-symbols-outlined">error</span> {wizardError}
        </div>
      )}

      <form className="prd-wiz-form" onSubmit={(e) => void handleSubmit(e)}>

        {/* ── Step 1: Search ───────────────────────────────────────── */}
        {step === 1 && !isEdit && (
          <div className="prd-wiz-panel">
            <h3 className="prd-wiz-panel__title">¿Ya existe en el sistema?</h3>
            <p className="prd-wiz-panel__desc">
              Busca primero para evitar duplicados. Si ya existe, lo asignamos como {roleLabel} directamente.
            </p>
            <div className="prd-search-box" style={{ marginBottom: 16 }}>
              <span className="material-symbols-outlined prd-search-icon">search</span>
              <input ref={queryRef} className="prd-search-input"
                placeholder="RUC, cédula o razón social…" value={query} autoFocus
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void handleSearch(); } }}
                disabled={searching} />
              <button type="button" className="zh-btn zh-btn--primary zh-btn--md"
                disabled={searching || !query.trim()} onClick={() => void handleSearch()}>
                {searching ? 'Buscando…' : 'Buscar'}
              </button>
            </div>
            {searchError && <span className="prd-wiz-error-msg">{searchError}</span>}
            {hasSearched && results.length > 0 && (
              <ul className="md-search-results">
                {results.map((bp) => {
                  const busy = assigning === bp.id || submitting;
                  return (
                    <li key={bp.id} className="md-search-result-item">
                      <div className="md-search-result-info">
                        <span className="md-search-result-name">{bp.legalName}</span>
                        <span className="md-search-result-id mono">{bp.identificationNumber}</span>
                      </div>
                      <button type="button" className="zh-btn zh-btn--primary zh-btn--sm"
                        disabled={busy} onClick={() => void handleAssign(bp)}>
                        {busy ? 'Asignando…' : `+ ${roleLabel}`}
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        )}

        {/* ── Step 2: Identity ─────────────────────────────────────── */}
        {step === 2 && (
          <div className="prd-wiz-panel">
            {notFound && !isEdit && (
              <div className="prd-wiz-notfound-banner" role="status">
                <span className="material-symbols-outlined">search_off</span>
                <span>No se encontró <strong>"{notFound}"</strong> — completa los datos para crear nuevo {roleLabel}.</span>
              </div>
            )}
            <h3 className="prd-wiz-panel__title">
              {isEdit ? 'Editar identidad' : `Nuevo ${roleLabel}`}
            </h3>
            <p className="prd-wiz-panel__desc">
              Email, teléfono y representante legal se agregan desde el detalle del BP (pestaña Contactos).
            </p>
            <MasterDataBpFormFields
              section="identity"
              identificationType={identificationType} setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber} setIdentificationNumber={setIdentificationNumber}
              personType={personType} setPersonType={setPersonType}
              legalName={legalName} setLegalName={setLegalName}
              tradeName={tradeName} setTradeName={setTradeName}
              countryCode={countryCode} setCountryCode={setCountryCode}
              fieldErrors={fieldErrors} setFieldErrors={setFieldErrors}
              saving={submitting}
            />
          </div>
        )}

        {/* ── Step 3: Review ───────────────────────────────────────── */}
        {step === 3 && (
          <div className="prd-wiz-panel">
            <h3 className="prd-wiz-panel__title">Revisar antes de guardar</h3>
            <MasterDataBpFormFields
              section="review"
              identificationType={identificationType} setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber} setIdentificationNumber={setIdentificationNumber}
              personType={personType} setPersonType={setPersonType}
              legalName={legalName} setLegalName={setLegalName}
              tradeName={tradeName} setTradeName={setTradeName}
              countryCode={countryCode} setCountryCode={setCountryCode}
              fieldErrors={{}} setFieldErrors={setFieldErrors}
              saving={submitting}
            />
            <div className="prd-review-warning" role="note">
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>info</span>
              <span>Tras crear el BP, asignaremos el rol <strong>{roleLabel}</strong> automáticamente.</span>
            </div>
          </div>
        )}

        {/* ── Footer ───────────────────────────────────────────────── */}
        <div className="prd-wiz-footer">
          <div className="prd-wiz-footer__left">
            {((!isEdit && step > 1) || (isEdit && step > 2)) && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={goPrev}>
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_back</span>
                Anterior
              </button>
            )}
          </div>
          <div className="prd-wiz-footer__right">
            {(isEdit || step > 1) && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={onCancel}>Cancelar</button>
            )}
            {!isEdit && step === 1 && (
              <button type="button" className="zh-btn zh-btn--secondary zh-btn--md"
                onClick={() => { prefill(query); setStep(2); }}>
                + Crear sin buscar
              </button>
            )}
            {step === 2 && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md"
                onClick={() => { saveDraft(draftKey, identificationNumber, legalName); setDraftBanner(loadDraft(draftKey)); }}>
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>save</span> Guardar borrador
              </button>
            )}
            {step < 3 ? (
              <button type="button" className="zh-btn zh-btn--primary zh-btn--md" onClick={goNext}
                disabled={step === 1 && !hasSearched && !notFound}>
                Siguiente <span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_forward</span>
              </button>
            ) : (
              <button type="submit" className="zh-btn zh-btn--md prd-btn--success" disabled={submitting}>
                {submitting ? (
                  <><span className="prd-spinner" aria-hidden /> Guardando…</>
                ) : (
                  <><span className="material-symbols-outlined" style={{ fontSize: 16 }}>check</span>
                    {isEdit ? 'Guardar cambios' : `Crear ${roleLabel}`}</>
                )}
              </button>
            )}
          </div>
        </div>
      </form>
    </div>
  );
}
