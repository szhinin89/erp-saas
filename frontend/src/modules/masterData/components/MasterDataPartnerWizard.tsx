import { useCallback, useEffect, useRef, useState } from 'react';
import { useI18n } from '../../../i18n/i18n';
import { businessPartnerSchema } from '../../../schemas/masterData/businessPartnerSchema';
import { businessPartnerService } from '../api/businessPartnerService';
import type {
  BusinessPartnerDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from '../types/businessPartner.types';
import { MasterDataBpFormFields } from './MasterDataBpFormFields';

type StepId = 1 | 2 | 3 | 4;
type Role = 'customer' | 'supplier';

interface Props {
  role: Role;
  draftKey: string;
  submitting: boolean;
  wizardError?: string | null;
  editingPartner: BusinessPartnerDto | null;
  onSubmitCreate: (body: CreateBusinessPartnerBody) => Promise<boolean>;
  onSubmitUpdate: (body: UpdateBusinessPartnerBody) => Promise<boolean>;
  onAssignRole: (id: string) => Promise<boolean>;
  onCancel: () => void;
  onAssignSuccess?: () => void;
}

const STEPS: { id: StepId; icon: string; labelKey: string; labelFb: string }[] = [
  { id: 1, icon: 'search', labelKey: 'masterdata.wizard.step.search', labelFb: 'Buscar y asignar' },
  { id: 2, icon: 'badge', labelKey: 'masterdata.wizard.step.identity', labelFb: 'Identificación' },
  { id: 3, icon: 'contact_mail', labelKey: 'masterdata.wizard.step.contact', labelFb: 'Contacto' },
  { id: 4, icon: 'check_circle', labelKey: 'masterdata.wizard.step.review', labelFb: 'Revisar y guardar' },
];

function alreadyHasRole(bp: BusinessPartnerDto, role: Role): boolean {
  return role === 'customer' ? bp.isCustomer : bp.isSupplier;
}

interface DraftSnapshot {
  identificationNumber: string;
  legalName: string;
  savedAt: string;
}

function saveDraft(key: string, identificationNumber: string, legalName: string) {
  const snap: DraftSnapshot = {
    identificationNumber,
    legalName,
    savedAt: new Date().toISOString(),
  };
  localStorage.setItem(key, JSON.stringify(snap));
}

function loadDraft(key: string): DraftSnapshot | null {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as DraftSnapshot) : null;
  } catch {
    return null;
  }
}

function clearDraft(key: string) {
  localStorage.removeItem(key);
}

export function MasterDataPartnerWizard({
  role,
  draftKey,
  submitting,
  wizardError,
  editingPartner,
  onSubmitCreate,
  onSubmitUpdate,
  onAssignRole,
  onCancel,
  onAssignSuccess,
}: Props) {
  const { t } = useI18n();
  const isEdit = !!editingPartner;
  const [step, setStep] = useState<StepId>(isEdit ? 2 : 1);

  const [query, setQuery] = useState('');
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState('');
  const [results, setResults] = useState<BusinessPartnerDto[]>([]);
  const [assigning, setAssigning] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const [notFoundQuery, setNotFoundQuery] = useState<string | null>(null);

  const [identificationType, setIdentificationType] = useState(
    editingPartner?.identificationType ?? 'RUC',
  );
  const [identificationNumber, setIdentificationNumber] = useState(
    editingPartner?.identificationNumber ?? '',
  );
  const [legalName, setLegalName] = useState(editingPartner?.legalName ?? '');
  const [tradeName, setTradeName] = useState(editingPartner?.tradeName ?? '');
  const [legalRepresentativeName, setLegalRepresentativeName] = useState(
    editingPartner?.legalRepresentativeName ?? '',
  );
  const [email, setEmail] = useState(editingPartner?.email ?? '');
  const [phone, setPhone] = useState(editingPartner?.phone ?? '');
  const [countryCode, setCountryCode] = useState(editingPartner?.countryCode ?? 'EC');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [draftBanner, setDraftBanner] = useState<DraftSnapshot | null>(null);
  const queryRef = useRef<HTMLInputElement>(null);

  const roleLabel = t(
    role === 'customer' ? 'masterdata.wizard.role.customer' : 'masterdata.wizard.role.supplier',
    role === 'customer' ? 'Cliente' : 'Proveedor',
  );

  useEffect(() => {
    if (!isEdit) {
      const d = loadDraft(draftKey);
      if (d) setDraftBanner(d);
    }
  }, [draftKey, isEdit]);

  const prefillFromQuery = (q: string) => {
    const trimmed = q.trim();
    if (/^\d+$/.test(trimmed)) {
      setIdentificationNumber(trimmed);
    } else {
      setLegalName(trimmed);
    }
  };

  const validateStep = useCallback(
    (s: StepId): boolean => {
      if (s === 1) return true;
      const fields: Record<string, string> = {};
      if (s === 2 || s === 3 || s === 4) {
        const parsed = businessPartnerSchema.safeParse({
          identificationType,
          identificationNumber: identificationNumber.trim(),
          legalName: legalName.trim(),
          tradeName: tradeName.trim() || undefined,
          email: email.trim() || undefined,
          phone: phone.trim() || undefined,
        });
        if (!parsed.success) {
          for (const issue of parsed.error.issues) {
            const field = issue.path[0] as string;
            if (!fields[field]) fields[field] = issue.message;
          }
          setFieldErrors(fields);
          return false;
        }
        if (s === 2) {
          const step2Fields = ['identificationType', 'identificationNumber', 'legalName'];
          const hasStep2Error = step2Fields.some((f) => fields[f]);
          if (hasStep2Error) return false;
        }
      }
      setFieldErrors({});
      return true;
    },
    [identificationType, identificationNumber, legalName, tradeName, legalRepresentativeName, email, phone],
  );

  const handleSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setSearching(true);
    setSearchError('');
    try {
      const rows = await businessPartnerService.search({ q, take: 10 });
      setResults(rows);
      setHasSearched(true);
      if (rows.length === 0) {
        prefillFromQuery(q);
        setNotFoundQuery(q);
        setStep(2);
      }
    } catch {
      setSearchError(t('masterdata.wizard.search.error', 'Error al buscar. Intente de nuevo.'));
    } finally {
      setSearching(false);
    }
  };

  const handleAssign = async (bp: BusinessPartnerDto) => {
    setAssigning(bp.id);
    try {
      const ok = await onAssignRole(bp.id);
      if (ok) onAssignSuccess?.();
    } finally {
      setAssigning(null);
    }
  };

  const goNext = () => {
    if (!validateStep(step)) return;
    if (step < 4) setStep((step + 1) as StepId);
  };

  const goPrev = () => {
    if (isEdit && step <= 2) return;
    if (step > 1) setStep((step - 1) as StepId);
  };

  const skipToCreate = () => {
    prefillFromQuery(query);
    setStep(2);
  };

  const buildBody = () => ({
    identificationType,
    identificationNumber: identificationNumber.trim(),
    legalName: legalName.trim(),
    tradeName: tradeName.trim() || null,
    legalRepresentativeName: legalRepresentativeName.trim() || null,
    email: email.trim() || null,
    phone: phone.trim() || null,
    countryCode: countryCode.trim() || null,
  });

  const handleSaveDraft = () => {
    saveDraft(draftKey, identificationNumber, legalName);
    setDraftBanner(loadDraft(draftKey));
  };

  const restoreDraft = () => {
    const d = loadDraft(draftKey);
    if (!d) return;
    setIdentificationNumber(d.identificationNumber);
    setLegalName(d.legalName);
    setDraftBanner(null);
    setStep(2);
  };

  const handleFormSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (step !== 4 || !validateStep(4)) return;

    const body = buildBody();
    if (isEdit && editingPartner) {
      const ok = await onSubmitUpdate(body);
      if (ok) clearDraft(draftKey);
      return;
    }
    const ok = await onSubmitCreate({
      ...body,
      asCustomer: role === 'customer',
      asSupplier: role === 'supplier',
    });
    if (ok) clearDraft(draftKey);
  };

  const displayName = tradeName.trim() || legalName.trim() || '—';

  return (
    <div className="prd-wizard prd-fadein">
      <div className="prd-wiz-progress" role="navigation" aria-label={t('masterdata.wizard.progress', 'Progreso del asistente')}>
        {STEPS.map((s, idx) => {
          if (isEdit && s.id === 1) return null;
          const done = s.id < step;
          const active = s.id === step;
          const clickable = !isEdit && s.id < step;
          return (
            <button
              key={s.id}
              type="button"
              className={`prd-wiz-step ${active ? 'prd-wiz-step--active' : ''} ${done ? 'prd-wiz-step--done' : ''}`}
              disabled={!clickable}
              onClick={() => clickable && setStep(s.id)}
              aria-current={active ? 'step' : undefined}
            >
              <span className="prd-wiz-step__num">
                {done ? (
                  <span className="material-symbols-outlined" style={{ fontSize: 16 }}>check</span>
                ) : (
                  s.id
                )}
              </span>
              <span className="material-symbols-outlined prd-wiz-step__icon">{s.icon}</span>
              <span className="prd-wiz-step__label">{t(s.labelKey, s.labelFb)}</span>
              {idx < STEPS.length - 1 && <span className="prd-wiz-step__line" aria-hidden />}
            </button>
          );
        })}
      </div>

      {draftBanner && !isEdit && (
        <div className="prd-draft-banner" role="note">
          <span className="material-symbols-outlined" style={{ fontSize: 18 }}>history</span>
          <span>
            {t('masterdata.wizard.draft.found', 'Borrador guardado')}: <strong>{draftBanner.legalName || draftBanner.identificationNumber}</strong>
          </span>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={restoreDraft}>
            {t('masterdata.wizard.draft.restore', 'Restaurar')}
          </button>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" onClick={() => { clearDraft(draftKey); setDraftBanner(null); }}>
            {t('masterdata.wizard.draft.discard', 'Descartar')}
          </button>
        </div>
      )}

      {wizardError && (
        <div className="prd-wiz-error-banner" role="alert">
          <span className="material-symbols-outlined">error</span>
          {wizardError}
        </div>
      )}

      <form className="prd-wiz-form" onSubmit={(e) => void handleFormSubmit(e)}>
        {step === 1 && !isEdit && (
          <div className="prd-wiz-panel">
            <h3 className="prd-wiz-panel__title">{t('masterdata.wizard.search.title', '¿Ya existe en el sistema?')}</h3>
            <p className="prd-wiz-panel__desc">
              {t('masterdata.wizard.search.desc', { role: roleLabel })}
            </p>
            <div className="prd-search-box" style={{ marginBottom: 16 }}>
              <span className="material-symbols-outlined prd-search-icon">search</span>
              <input
                ref={queryRef}
                className="prd-search-input"
                placeholder={t('masterdata.wizard.search.placeholder', 'RUC, cédula o razón social…')}
                value={query}
                autoFocus
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    void handleSearch();
                  }
                }}
                disabled={searching}
              />
              <button
                type="button"
                className="zh-btn zh-btn--primary zh-btn--md"
                disabled={searching || !query.trim()}
                onClick={() => void handleSearch()}
              >
                {searching ? t('masterdata.wizard.search.loading', 'Buscando…') : t('masterdata.wizard.search.btn', 'Buscar')}
              </button>
            </div>
            {searchError && <span className="prd-wiz-error-msg">{searchError}</span>}

            {hasSearched && (
              <div className="md-wiz-results">
                {results.length === 0 ? (
                  <p className="prd-wiz-hint">{t('masterdata.wizard.search.empty', 'No se encontraron registros.')}</p>
                ) : (
                  <ul className="md-search-results">
                    {results.map((bp) => {
                      const hasRole = alreadyHasRole(bp, role);
                      const busy = assigning === bp.id || submitting;
                      return (
                        <li key={bp.id} className="md-search-result-item">
                          <div className="md-search-result-info">
                            <span className="md-search-result-name">{bp.legalName}</span>
                            <span className="md-search-result-id mono">{bp.identificationNumber}</span>
                            <span className="md-search-result-roles">
                              {bp.isCustomer && <span className="md-badge md-badge--ok">{t('masterdata.wizard.badge.customer', 'Cliente')}</span>}
                              {bp.isSupplier && <span className="md-badge md-badge--ok">{t('masterdata.wizard.badge.supplier', 'Proveedor')}</span>}
                            </span>
                          </div>
                          {hasRole ? (
                            <span className="md-search-already">
                              {t('masterdata.wizard.search.alreadyRole', { role: roleLabel })}
                            </span>
                          ) : (
                            <button
                              type="button"
                              className="zh-btn zh-btn--primary zh-btn--sm"
                              disabled={busy}
                              onClick={() => void handleAssign(bp)}
                            >
                              {busy
                                ? t('masterdata.wizard.search.assigning', 'Asignando…')
                                : t('masterdata.wizard.search.assign', { role: roleLabel })}
                            </button>
                          )}
                        </li>
                      );
                    })}
                  </ul>
                )}
              </div>
            )}
          </div>
        )}

        {step === 2 && (
          <div className="prd-wiz-panel">
            {notFoundQuery && !isEdit && (
              <div className="prd-wiz-notfound-banner" role="status">
                <span className="material-symbols-outlined">search_off</span>
                <span>
                  {t('masterdata.wizard.search.notFound', 'No se encontró')}{' '}
                  <strong>"{notFoundQuery}"</strong>{' '}
                  {t('masterdata.wizard.search.notFoundHint', `— completa los datos para crear un nuevo ${roleLabel}.`)}
                </span>
              </div>
            )}
            <h3 className="prd-wiz-panel__title">
              {isEdit
                ? t('masterdata.wizard.identity.editTitle', 'Datos de identificación')
                : t('masterdata.wizard.identity.createTitle', { role: roleLabel })}
            </h3>
            <MasterDataBpFormFields
              section="identity"
              identificationType={identificationType}
              setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber}
              setIdentificationNumber={setIdentificationNumber}
              legalName={legalName}
              setLegalName={setLegalName}
              tradeName={tradeName}
              setTradeName={setTradeName}
              legalRepresentativeName={legalRepresentativeName}
              setLegalRepresentativeName={setLegalRepresentativeName}
              email={email}
              setEmail={setEmail}
              phone={phone}
              setPhone={setPhone}
              countryCode={countryCode}
              setCountryCode={setCountryCode}
              fieldErrors={fieldErrors}
              setFieldErrors={setFieldErrors}
              saving={submitting}
            />
          </div>
        )}

        {step === 3 && (
          <div className="prd-wiz-panel">
            <h3 className="prd-wiz-panel__title">{t('masterdata.wizard.contact.title', 'Datos de contacto')}</h3>
            <p className="prd-wiz-panel__desc">{t('masterdata.wizard.contact.desc', 'Email y teléfono opcionales para facturación y seguimiento.')}</p>
            <MasterDataBpFormFields
              section="contact"
              identificationType={identificationType}
              setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber}
              setIdentificationNumber={setIdentificationNumber}
              legalName={legalName}
              setLegalName={setLegalName}
              tradeName={tradeName}
              setTradeName={setTradeName}
              email={email}
              setEmail={setEmail}
              phone={phone}
              setPhone={setPhone}
              countryCode={countryCode}
              setCountryCode={setCountryCode}
              fieldErrors={fieldErrors}
              setFieldErrors={setFieldErrors}
              saving={submitting}
            />
          </div>
        )}

        {step === 4 && (
          <div className="prd-wiz-panel">
            <h3 className="prd-wiz-panel__title">{t('masterdata.wizard.review.title', 'Revisar antes de guardar')}</h3>
            <dl className="prd-review-grid">
              <dt>{t('masterdata.wizard.review.role', 'Rol')}</dt>
              <dd>{roleLabel}</dd>
              <dt>{t('masterdata.customers.col.identification', 'Identificación')}</dt>
              <dd className="mono">{identificationType} — {identificationNumber}</dd>
              <dt>{t('masterdata.customers.col.legalName', 'Razón social')}</dt>
              <dd>{displayName}</dd>
              {identificationType === 'RUC' && legalRepresentativeName.trim() && (
                <>
                  <dt>{t('masterdata.wizard.review.legalRep', 'Representante legal')}</dt>
                  <dd>{legalRepresentativeName}</dd>
                </>
              )}
              {email.trim() && (
                <>
                  <dt>Email</dt>
                  <dd>{email}</dd>
                </>
              )}
              {phone.trim() && (
                <>
                  <dt>{t('masterdata.wizard.review.phone', 'Teléfono')}</dt>
                  <dd>{phone}</dd>
                </>
              )}
              <dt>{t('masterdata.wizard.review.country', 'País')}</dt>
              <dd className="mono">{countryCode || '—'}</dd>
            </dl>
            {isEdit && editingPartner && (
              <div className="md-modal-roles" style={{ marginTop: 16 }}>
                <span className="md-modal-roles-label">{t('masterdata.wizard.review.roles', 'Roles activos')}</span>
                <label className="md-role-check">
                  <input type="checkbox" checked={editingPartner.isCustomer} disabled readOnly />
                  {t('masterdata.wizard.badge.customer', 'Cliente')}
                </label>
                <label className="md-role-check">
                  <input type="checkbox" checked={editingPartner.isSupplier} disabled readOnly />
                  {t('masterdata.wizard.badge.supplier', 'Proveedor')}
                </label>
              </div>
            )}
            <div className="prd-review-warning" role="note">
              <span className="material-symbols-outlined" style={{ fontSize: 18 }}>info</span>
              <span>{t('masterdata.wizard.review.hint', 'Al guardar, el registro quedará disponible en listados y documentos del ERP.')}</span>
            </div>
          </div>
        )}

        <div className="prd-wiz-footer">
          <div className="prd-wiz-footer__left">
            {((!isEdit && step > 1) || (isEdit && step > 2)) && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={goPrev}>
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_back</span>
                {t('masterdata.wizard.btn.prev', 'Anterior')}
              </button>
            )}
          </div>
          <div className="prd-wiz-footer__right">
            {(isEdit || step > 1) && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={onCancel}>
                {t('common.cancel', 'Cancelar')}
              </button>
            )}
            {!isEdit && step === 1 && (
              <button type="button" className="zh-btn zh-btn--secondary zh-btn--md" onClick={skipToCreate}>
                {t('masterdata.wizard.search.skip', '+ Crear sin buscar')}
              </button>
            )}
            {step >= 2 && (
              <button type="button" className="zh-btn zh-btn--ghost zh-btn--md" onClick={handleSaveDraft} title={t('masterdata.wizard.draft.saveTitle', 'Guardar borrador en el navegador')}>
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>save</span>
                {t('masterdata.wizard.draft.save', 'Guardar borrador')}
              </button>
            )}
            {step < 4 ? (
              <button
                type="button"
                className="zh-btn zh-btn--primary zh-btn--md"
                onClick={goNext}
                disabled={step === 1 && !hasSearched}
              >
                {t('masterdata.wizard.btn.next', 'Siguiente')}
                <span className="material-symbols-outlined" style={{ fontSize: 16 }}>arrow_forward</span>
              </button>
            ) : (
              <button type="submit" className="zh-btn zh-btn--md prd-btn--success" disabled={submitting}>
                {submitting ? (
                  <>
                    <span className="prd-spinner" aria-hidden />
                    {t('masterdata.wizard.btn.saving', 'Guardando…')}
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined" style={{ fontSize: 16 }}>check</span>
                    {isEdit
                      ? t('masterdata.wizard.btn.update', 'Guardar cambios')
                      : t('masterdata.wizard.btn.create', { role: roleLabel })}
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      </form>
    </div>
  );
}
