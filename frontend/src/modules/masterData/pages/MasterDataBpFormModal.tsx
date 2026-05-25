import { useState, useRef } from 'react';
import { ZHBtn, ZHField } from '../../../components/zh/ZHForm';
import { ZHPageNotice } from '../../../components/zh/ZHPageNotice';
import { businessPartnerSchema } from '../../../schemas/masterData/businessPartnerSchema';
import { businessPartnerService } from '../api/businessPartnerService';
import type { BusinessPartner } from '../api/businessPartnerService';
import type { CreateBusinessPartnerBody, UpdateBusinessPartnerBody } from '../types/businessPartner.types';

type Step = 'search' | 'results' | 'create';

type CreateMode = {
  mode?: 'create';
  roleToAssign: 'customer' | 'supplier';
  onSubmit: (body: CreateBusinessPartnerBody) => void;
  onAssignRole: (id: string) => Promise<void>;
  onUpdate?: never;
  initialValues?: never;
  currentIsCustomer?: never;
  currentIsSupplier?: never;
};

type EditMode = {
  mode: 'edit';
  roleToAssign?: never;
  onSubmit?: never;
  onAssignRole?: never;
  onUpdate: (body: UpdateBusinessPartnerBody) => void;
  initialValues: UpdateBusinessPartnerBody;
  currentIsCustomer?: boolean;
  currentIsSupplier?: boolean;
};

type Props = (CreateMode | EditMode) & {
  title: string;
  saving: boolean;
  error?: string | null;
  onClose: () => void;
};

const ROLE_LABEL: Record<'customer' | 'supplier', string> = {
  customer: 'Cliente',
  supplier: 'Proveedor',
};

function alreadyHasRole(bp: BusinessPartner, role: 'customer' | 'supplier'): boolean {
  return role === 'customer' ? bp.isCustomer : bp.isSupplier;
}

export function MasterDataBpFormModal(props: Props) {
  const isEdit = props.mode === 'edit';

  // ── Create-mode state ──────────────────────────────────────────────────────
  const [step, setStep]               = useState<Step>('search');
  const [query, setQuery]             = useState('');
  const [searching, setSearching]     = useState(false);
  const [searchError, setSearchError] = useState('');
  const [results, setResults]         = useState<BusinessPartner[]>([]);
  const [assigning, setAssigning]     = useState<string | null>(null);

  // ── Form state (create + edit) ─────────────────────────────────────────────
  const [identificationType, setIdentificationType] = useState(
    props.initialValues?.identificationType ?? 'RUC',
  );
  const [identificationNumber, setIdentificationNumber] = useState(
    props.initialValues?.identificationNumber ?? '',
  );
  const [legalName, setLegalName]     = useState(props.initialValues?.legalName ?? '');
  const [tradeName, setTradeName]     = useState(props.initialValues?.tradeName ?? '');
  const [email, setEmail]             = useState(props.initialValues?.email ?? '');
  const [phone, setPhone]             = useState(props.initialValues?.phone ?? '');
  const [countryCode, setCountryCode] = useState(props.initialValues?.countryCode ?? 'EC');
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const queryRef = useRef<HTMLInputElement>(null);

  // ── Helpers ────────────────────────────────────────────────────────────────
  const prefillFromQuery = (q: string) => {
    const trimmed = q.trim();
    if (/^\d+$/.test(trimmed)) {
      setIdentificationNumber(trimmed);
    } else {
      setLegalName(trimmed);
    }
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

  const handleAssign = async (bp: BusinessPartner) => {
    if (!props.onAssignRole) return;
    setAssigning(bp.id);
    try {
      await props.onAssignRole(bp.id);
    } finally {
      setAssigning(null);
    }
  };

  const goToCreate = () => {
    prefillFromQuery(query);
    setStep('create');
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const parsed = businessPartnerSchema.safeParse({
      identificationType,
      identificationNumber: identificationNumber.trim(),
      legalName: legalName.trim(),
      tradeName: (tradeName as string).trim() || undefined,
      email:     (email as string).trim()     || undefined,
      phone:     (phone as string).trim()     || undefined,
    });

    if (!parsed.success) {
      const errs: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as string;
        if (!errs[field]) errs[field] = issue.message;
      }
      setFieldErrors(errs);
      return;
    }

    setFieldErrors({});
    const body = {
      identificationType,
      identificationNumber: identificationNumber.trim(),
      legalName:   legalName.trim(),
      tradeName:   (tradeName as string).trim()   || null,
      email:       (email as string).trim()       || null,
      phone:       (phone as string).trim()       || null,
      countryCode: countryCode.trim()             || null,
    };

    if (isEdit) {
      props.onUpdate(body);
    } else {
      props.onSubmit!({
        ...body,
        asCustomer: props.roleToAssign === 'customer',
        asSupplier: props.roleToAssign === 'supplier',
      });
    }
  };

  const roleLabel = !isEdit ? ROLE_LABEL[props.roleToAssign!] : '';

  return (
    <div className="md-modal-backdrop" role="dialog" aria-modal="true">
      <form className="md-modal" onSubmit={handleSubmit}>
        <h2>{props.title}</h2>

        {props.error && (
          <ZHPageNotice variant="error" message={props.error} className="md-modal-notice" />
        )}

        {/* ── EDIT MODE: normal form ───────────────────────────────────────── */}
        {isEdit && (
          <>
            <FormFields
              identificationType={identificationType} setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber} setIdentificationNumber={setIdentificationNumber}
              legalName={legalName} setLegalName={setLegalName}
              tradeName={tradeName as string} setTradeName={setTradeName}
              email={email as string} setEmail={setEmail}
              phone={phone as string} setPhone={setPhone}
              countryCode={countryCode} setCountryCode={setCountryCode}
              fieldErrors={fieldErrors} setFieldErrors={setFieldErrors}
              saving={props.saving}
            />
            <div className="md-modal-roles">
              <span className="md-modal-roles-label">Rol</span>
              <label className="md-role-check">
                <input type="checkbox" checked={props.currentIsCustomer ?? false}
                  disabled readOnly />
                Cliente
                {props.currentIsCustomer && <span className="md-role-active">(activo)</span>}
              </label>
              <label className="md-role-check">
                <input type="checkbox" checked={props.currentIsSupplier ?? false}
                  disabled readOnly />
                Proveedor
                {props.currentIsSupplier && <span className="md-role-active">(activo)</span>}
              </label>
            </div>
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={props.onClose} disabled={props.saving}>Cancelar</ZHBtn>
              <ZHBtn variant="primary" type="submit" disabled={props.saving}>
                {props.saving ? 'Guardando…' : 'Guardar cambios'}
              </ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE MODE: step SEARCH ─────────────────────────────────────── */}
        {!isEdit && step === 'search' && (
          <>
            <p className="md-search-hint">
              Busca por número de identificación o nombre. Si ya existe lo asignamos como <strong>{roleLabel}</strong> directamente.
            </p>
            <div className="md-search-row">
              <input
                ref={queryRef}
                className="zh-input md-search-input"
                placeholder="RUC, cédula o razón social…"
                value={query}
                autoFocus
                onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); void handleSearch(); } }}
                disabled={searching}
              />
              <ZHBtn variant="primary" type="button" onClick={() => void handleSearch()} disabled={searching || !query.trim()}>
                {searching ? 'Buscando…' : 'Buscar'}
              </ZHBtn>
            </div>
            {searchError && <span className="md-field-error">{searchError}</span>}
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={props.onClose}>Cancelar</ZHBtn>
              <ZHBtn variant="secondary" type="button" onClick={goToCreate} disabled={searching}>
                + Crear sin buscar
              </ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE MODE: step RESULTS ─────────────────────────────────────── */}
        {!isEdit && step === 'results' && (
          <>
            <p className="md-search-hint">
              Resultados para <strong>"{query}"</strong>. Selecciona uno para asignarlo como <strong>{roleLabel}</strong>, o crea uno nuevo.
            </p>
            {results.length === 0 ? (
              <p className="md-search-empty">No se encontraron registros.</p>
            ) : (
              <ul className="md-search-results">
                {results.map((bp) => {
                  const hasRole = alreadyHasRole(bp, props.roleToAssign!);
                  const busy = assigning === bp.id || props.saving;
                  return (
                    <li key={bp.id} className="md-search-result-item">
                      <div className="md-search-result-info">
                        <span className="md-search-result-name">{bp.legalName}</span>
                        <span className="md-search-result-id mono">{bp.identificationNumber}</span>
                        <span className="md-search-result-roles">
                          {bp.isCustomer && <span className="md-badge md-badge--ok">Cliente</span>}
                          {bp.isSupplier && <span className="md-badge md-badge--ok">Proveedor</span>}
                        </span>
                      </div>
                      {hasRole ? (
                        <span className="md-search-already">Ya es {roleLabel}</span>
                      ) : (
                        <ZHBtn variant="primary" size="sm" type="button"
                          disabled={busy} onClick={() => void handleAssign(bp)}>
                          {busy ? 'Asignando…' : `Asignar como ${roleLabel}`}
                        </ZHBtn>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button" onClick={() => setStep('search')}>← Buscar de nuevo</ZHBtn>
              <ZHBtn variant="primary" type="button" onClick={goToCreate}>+ Crear nuevo</ZHBtn>
            </div>
          </>
        )}

        {/* ── CREATE MODE: step CREATE ─────────────────────────────────────── */}
        {!isEdit && step === 'create' && (
          <>
            <p className="md-search-hint">
              Nuevo <strong>{roleLabel}</strong>. Completa los datos y guarda.
            </p>
            <FormFields
              identificationType={identificationType} setIdentificationType={setIdentificationType}
              identificationNumber={identificationNumber} setIdentificationNumber={setIdentificationNumber}
              legalName={legalName} setLegalName={setLegalName}
              tradeName={tradeName as string} setTradeName={setTradeName}
              email={email as string} setEmail={setEmail}
              phone={phone as string} setPhone={setPhone}
              countryCode={countryCode} setCountryCode={setCountryCode}
              fieldErrors={fieldErrors} setFieldErrors={setFieldErrors}
              saving={props.saving}
            />
            <div className="md-modal-actions">
              <ZHBtn variant="ghost" type="button"
                onClick={() => setStep(results.length > 0 ? 'results' : 'search')}
                disabled={props.saving}>
                ← Volver
              </ZHBtn>
              <ZHBtn variant="primary" type="submit" disabled={props.saving}>
                {props.saving ? 'Guardando…' : `Crear ${roleLabel}`}
              </ZHBtn>
            </div>
          </>
        )}
      </form>
    </div>
  );
}

// ── Shared form fields component ───────────────────────────────────────────────
type FieldsProps = {
  identificationType: string; setIdentificationType: (v: string) => void;
  identificationNumber: string; setIdentificationNumber: (v: string) => void;
  legalName: string; setLegalName: (v: string) => void;
  tradeName: string; setTradeName: (v: string) => void;
  email: string; setEmail: (v: string) => void;
  phone: string; setPhone: (v: string) => void;
  countryCode: string; setCountryCode: (v: string) => void;
  fieldErrors: Record<string, string>; setFieldErrors: (fn: (p: Record<string, string>) => Record<string, string>) => void;
  saving: boolean;
};

function FormFields({ identificationType, setIdentificationType, identificationNumber, setIdentificationNumber,
  legalName, setLegalName, tradeName, setTradeName, email, setEmail, phone, setPhone,
  countryCode, setCountryCode, fieldErrors, setFieldErrors, saving }: FieldsProps) {
  return (
    <div className="pg-form-grid pg-form-grid--2">
      <ZHField label="Tipo ID" required>
        <select className="zh-input" value={identificationType}
          onChange={(e) => { setIdentificationType(e.target.value); setFieldErrors((p) => ({ ...p, identificationType: '' })); }}
          disabled={saving}>
          <option value="RUC">RUC</option>
          <option value="CI">CI</option>
          <option value="PASSPORT">PASSPORT</option>
          <option value="OTHER">OTHER</option>
        </select>
        {fieldErrors.identificationType && <span className="md-field-error">{fieldErrors.identificationType}</span>}
      </ZHField>
      <ZHField label="Número" required>
        <input className={`zh-input mono${fieldErrors.identificationNumber ? ' zh-input--error' : ''}`}
          value={identificationNumber}
          onChange={(e) => { setIdentificationNumber(e.target.value); setFieldErrors((p) => ({ ...p, identificationNumber: '' })); }}
          disabled={saving} />
        {fieldErrors.identificationNumber && <span className="md-field-error">{fieldErrors.identificationNumber}</span>}
      </ZHField>
      <ZHField label="Razón social" required>
        <input className={`zh-input${fieldErrors.legalName ? ' zh-input--error' : ''}`}
          value={legalName}
          onChange={(e) => { setLegalName(e.target.value); setFieldErrors((p) => ({ ...p, legalName: '' })); }}
          disabled={saving} />
        {fieldErrors.legalName && <span className="md-field-error">{fieldErrors.legalName}</span>}
      </ZHField>
      <ZHField label="Nombre comercial">
        <input className="zh-input" value={tradeName} onChange={(e) => setTradeName(e.target.value)} disabled={saving} />
      </ZHField>
      <ZHField label="Email">
        <input className={`zh-input${fieldErrors.email ? ' zh-input--error' : ''}`} type="email" value={email}
          onChange={(e) => { setEmail(e.target.value); setFieldErrors((p) => ({ ...p, email: '' })); }}
          disabled={saving} />
        {fieldErrors.email && <span className="md-field-error">{fieldErrors.email}</span>}
      </ZHField>
      <ZHField label="Teléfono">
        <input className={`zh-input${fieldErrors.phone ? ' zh-input--error' : ''}`} value={phone}
          onChange={(e) => { setPhone(e.target.value); setFieldErrors((p) => ({ ...p, phone: '' })); }}
          disabled={saving} />
        {fieldErrors.phone && <span className="md-field-error">{fieldErrors.phone}</span>}
      </ZHField>
      <ZHField label="País (código ISO)">
        <input className="zh-input mono" value={countryCode}
          onChange={(e) => setCountryCode(e.target.value.toUpperCase().slice(0, 2))}
          disabled={saving} placeholder="EC" maxLength={2} />
      </ZHField>
    </div>
  );
}
