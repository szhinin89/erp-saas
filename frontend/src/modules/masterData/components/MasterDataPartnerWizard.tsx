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
 *
 * V3 (RHF migration):
 *   - useForm<BusinessPartnerFormValues> con zodResolver — fuente de verdad del formulario.
 *   - FormProvider — MasterDataBpFormFields consume useFormContext().
 *   - applyServerErrors — errores 422 aparecen bajo el campo correspondiente.
 */
import { useEffect, useRef, useState } from "react";
import { useForm, FormProvider } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import {
  businessPartnerSchema,
  type BusinessPartnerFormValues,
} from "../../../schemas/masterData/businessPartnerSchema";
import { businessPartnerService } from "../api/businessPartnerService";
import type {
  BusinessPartnerSummaryDto,
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import { PersonTypeEnum } from "../types/businessPartner.types";
import { MasterDataBpFormFields } from "./MasterDataBpFormFields";
import { ZHBtn } from "../../../components/zh/ZHForm";

type StepId = 1 | 2 | 3;
type Role = "customer" | "supplier";

/** Solo aplica cuando role='supplier' — ver SupplierRoleConfig (backend). */
export type SupplierConfigAtCreation = {
  refundProviderTypeCode: string;
  paymentTermId: string;
};

interface Props {
  role: Role;
  draftKey: string;
  submitting: boolean;
  editingPartner: BusinessPartnerSummaryDto | null;
  onSubmitCreate: (
    body: CreateBusinessPartnerBody,
    supplierConfig?: SupplierConfigAtCreation,
  ) => Promise<void>;
  onSubmitUpdate: (body: UpdateBusinessPartnerBody) => Promise<void>;
  onAssignRole: (id: string) => Promise<void>;
  onCancel: () => void;
  onAssignSuccess?: () => void;
}

const STEPS = [
  { id: 1 as StepId, icon: "search", labelFb: "Buscar y asignar" },
  { id: 2 as StepId, icon: "badge", labelFb: "Identificación" },
  { id: 3 as StepId, icon: "check_circle", labelFb: "Revisar y guardar" },
];

interface DraftSnapshot {
  identificationNumber: string;
  legalName: string;
  savedAt: string;
}

function saveDraft(
  key: string,
  identificationNumber: string,
  legalName: string,
) {
  localStorage.setItem(
    key,
    JSON.stringify({
      identificationNumber,
      legalName,
      savedAt: new Date().toISOString(),
    }),
  );
}
function loadDraft(key: string): DraftSnapshot | null {
  try {
    return JSON.parse(localStorage.getItem(key) ?? "null");
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

  const [query, setQuery] = useState("");
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState("");
  const [results, setResults] = useState<BusinessPartnerSummaryDto[]>([]);
  const [assigning, setAssigning] = useState<string | null>(null);
  const [hasSearched, setHasSearched] = useState(false);
  const [notFound, setNotFound] = useState<string | null>(null);
  const [draftBanner, setDraftBanner] = useState<DraftSnapshot | null>(null);
  const [bannerError, setBannerError] = useState<string | null>(null);
  const queryRef = useRef<HTMLInputElement>(null);

  const roleLabel = role === "customer" ? "Cliente" : "Proveedor";

  const form = useForm<BusinessPartnerFormValues>({
    resolver: zodResolver(businessPartnerSchema(role)),
    defaultValues: {
      identificationType: editingPartner?.identificationType ?? "04",
      identificationNumber: editingPartner?.identificationNumber ?? "",
      personType:
        PersonTypeEnum[
          editingPartner?.personType as keyof typeof PersonTypeEnum
        ] ?? PersonTypeEnum.Legal,
      legalName: editingPartner?.legalName ?? "",
      tradeName: editingPartner?.tradeName ?? "",
      countryCode: editingPartner?.countryCode ?? "EC",
      refundProviderTypeCode: "",
      paymentTermId: "",
    },
  });

  useEffect(() => {
    if (!isEdit) {
      const d = loadDraft(draftKey);
      if (d) setDraftBanner(d);
    }
  }, [draftKey, isEdit]);

  const prefill = (q: string) => {
    const s = q.trim();
    if (/^\d+$/.test(s))
      form.setValue("identificationNumber", s, { shouldValidate: false });
    else form.setValue("legalName", s, { shouldValidate: false });
  };

  const handleSearch = async () => {
    const q = query.trim();
    if (!q) return;
    setSearching(true);
    setSearchError("");
    try {
      const rows = await businessPartnerService.search({ q, take: 10 });
      setResults(rows);
      setHasSearched(true);
      if (rows.length === 0) {
        prefill(q);
        setNotFound(q);
        setStep(2);
      }
    } catch {
      setSearchError(
        t(
          "masterdata.wizard.search.error",
          "Error al buscar. Intente de nuevo.",
        ),
      );
    } finally {
      setSearching(false);
    }
  };

  const handleAssign = async (bp: BusinessPartnerSummaryDto) => {
    setAssigning(bp.id);
    setSearchError("");
    try {
      await onAssignRole(bp.id);
      onAssignSuccess?.();
    } catch (err) {
      setSearchError(
        formatApiRequestError(err, { generic: "Error al asignar el rol." }),
      );
    } finally {
      setAssigning(null);
    }
  };

  const goNext = async () => {
    if (step === 2) {
      const valid = await form.trigger();
      if (!valid) return;
    }
    if (step < 3) setStep((step + 1) as StepId);
  };
  const goPrev = () => {
    if (step > 1) setStep((step - 1) as StepId);
  };

  const buildCreateBody = (): CreateBusinessPartnerBody => {
    const v = form.getValues();
    return {
      identificationType: v.identificationType,
      identificationNumber: v.identificationNumber.trim(),
      personType: v.personType,
      legalName: v.legalName.trim(),
      tradeName: v.tradeName?.trim() || null,
      countryCode: v.countryCode?.trim() || null,
    };
  };

  const buildUpdateBody = (): UpdateBusinessPartnerBody => {
    const v = form.getValues();
    return {
      legalName: v.legalName.trim(),
      personType: v.personType,
      tradeName: v.tradeName?.trim() || null,
      countryCode: v.countryCode?.trim() || null,
    };
  };

  const onValidSubmit = async () => {
    setBannerError(null);
    try {
      if (isEdit) {
        await onSubmitUpdate(buildUpdateBody());
        clearDraft(draftKey);
      } else {
        const v = form.getValues();
        const supplierConfig =
          role === "supplier"
            ? {
                refundProviderTypeCode: v.refundProviderTypeCode || "",
                paymentTermId: v.paymentTermId || "",
              }
            : undefined;
        await onSubmitCreate(buildCreateBody(), supplierConfig);
        clearDraft(draftKey);
      }
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setBannerError(msg),
      );
      if (!applied)
        setBannerError(
          formatApiRequestError(err, {
            generic: "Error al guardar. Revisa los datos.",
          }),
        );
      if (applied) setStep(2);
    }
  };

  return (
    <FormProvider {...form}>
      <div className="prd-wizard prd-fadein">
        {/* Progress */}
        <div className="prd-wiz-progress">
          {STEPS.map((s) => {
            if (isEdit && s.id === 1) return null;
            const done = s.id < step;
            const active = s.id === step;
            return (
              <button
                key={s.id}
                type="button"
                className={`prd-wiz-step ${active ? "prd-wiz-step--active" : ""} ${done ? "prd-wiz-step--done" : ""}`}
                disabled={!done}
                onClick={() => done && setStep(s.id)}
              >
                <span className="prd-wiz-step__num">
                  {done ? (
                    <span className="material-symbols-outlined zh-icon-md">
                      check
                    </span>
                  ) : (
                    s.id
                  )}
                </span>
                <span className="material-symbols-outlined prd-wiz-step__icon">
                  {s.icon}
                </span>
                <span className="prd-wiz-step__label">{s.labelFb}</span>
              </button>
            );
          })}
        </div>

        {draftBanner && !isEdit && (
          <div className="prd-draft-banner">
            <span>
              Borrador guardado:{" "}
              <strong>
                {draftBanner.legalName || draftBanner.identificationNumber}
              </strong>
            </span>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => {
                const d = loadDraft(draftKey);
                if (d) {
                  form.setValue(
                    "identificationNumber",
                    d.identificationNumber,
                    { shouldValidate: false },
                  );
                  form.setValue("legalName", d.legalName, {
                    shouldValidate: false,
                  });
                  setDraftBanner(null);
                  setStep(2);
                }
              }}
            >
              Restaurar
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => {
                clearDraft(draftKey);
                setDraftBanner(null);
              }}
            >
              Descartar
            </ZHBtn>
          </div>
        )}

        {bannerError && (
          <div className="prd-wiz-error-banner" role="alert">
            <span className="material-symbols-outlined">error</span>{" "}
            {bannerError}
          </div>
        )}

        <form
          className="prd-wiz-form"
          onSubmit={form.handleSubmit(onValidSubmit)}
        >
          {/* ── Step 1: Search ───────────────────────────────────────── */}
          {step === 1 && !isEdit && (
            <div className="prd-wiz-panel">
              <h3 className="prd-wiz-panel__title">
                ¿Ya existe en el sistema?
              </h3>
              <p className="prd-wiz-panel__desc">
                Busca primero para evitar duplicados. Si ya existe, lo asignamos
                como {roleLabel} directamente.
              </p>
              <div className="prd-search-box zh-mb-16">
                <span className="material-symbols-outlined prd-search-icon">
                  search
                </span>
                <input
                  ref={queryRef}
                  className="prd-search-input"
                  placeholder="RUC, cédula o razón social…"
                  value={query}
                  autoFocus
                  onChange={(e) => setQuery(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      e.preventDefault();
                      void handleSearch();
                    }
                  }}
                  disabled={searching}
                />
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="md"
                  disabled={searching || !query.trim()}
                  onClick={() => void handleSearch()}
                >
                  {searching ? "Buscando…" : "Buscar"}
                </ZHBtn>
              </div>
              {searchError && (
                <span className="prd-wiz-error-msg">{searchError}</span>
              )}
              {hasSearched && results.length > 0 && (
                <ul className="md-search-results">
                  {results.map((bp) => {
                    const busy = assigning === bp.id || submitting;
                    return (
                      <li key={bp.id} className="md-search-result-item">
                        <div className="md-search-result-info">
                          <span className="md-search-result-name">
                            {bp.legalName}
                          </span>
                          <span className="md-search-result-id mono">
                            {bp.identificationNumber}
                          </span>
                        </div>
                        <ZHBtn
                          type="button"
                          variant="primary"
                          size="sm"
                          disabled={busy}
                          onClick={() => void handleAssign(bp)}
                        >
                          {busy ? "Asignando…" : `+ ${roleLabel}`}
                        </ZHBtn>
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
                  <span>
                    No se encontró <strong>"{notFound}"</strong> — completa los
                    datos para crear nuevo {roleLabel}.
                  </span>
                </div>
              )}
              <h3 className="prd-wiz-panel__title">
                {isEdit ? "Editar identidad" : `Nuevo ${roleLabel}`}
              </h3>
              <p className="prd-wiz-panel__desc">
                Email, teléfono y representante legal se agregan desde el
                detalle del BP (pestaña Contactos).
              </p>
              <MasterDataBpFormFields
                section="identity"
                saving={submitting}
                usage={role}
              />
            </div>
          )}

          {/* ── Step 3: Review ───────────────────────────────────────── */}
          {step === 3 && (
            <div className="prd-wiz-panel">
              <h3 className="prd-wiz-panel__title">Revisar antes de guardar</h3>
              <MasterDataBpFormFields
                section="review"
                saving={submitting}
                usage={role}
              />
              <div className="prd-review-warning" role="note">
                <span className="material-symbols-outlined zh-icon-lg">
                  info
                </span>
                <span>
                  Tras crear el BP, asignaremos el rol{" "}
                  <strong>{roleLabel}</strong> automáticamente.
                </span>
              </div>
            </div>
          )}

          {/* ── Footer ───────────────────────────────────────────────── */}
          <div className="prd-wiz-footer">
            <div className="prd-wiz-footer__left">
              {((!isEdit && step > 1) || (isEdit && step > 2)) && (
                <ZHBtn type="button" variant="ghost" size="md" onClick={goPrev}>
                  <span className="material-symbols-outlined zh-icon-md">
                    arrow_back
                  </span>
                  Anterior
                </ZHBtn>
              )}
            </div>
            <div className="prd-wiz-footer__right">
              {(isEdit || step > 1) && (
                <ZHBtn
                  type="button"
                  variant="ghost"
                  size="md"
                  onClick={onCancel}
                >
                  Cancelar
                </ZHBtn>
              )}
              {!isEdit && step === 1 && (
                <ZHBtn
                  type="button"
                  variant="secondary"
                  size="md"
                  onClick={() => {
                    prefill(query);
                    setStep(2);
                  }}
                >
                  + Crear sin buscar
                </ZHBtn>
              )}
              {step === 2 && (
                <ZHBtn
                  type="button"
                  variant="ghost"
                  size="md"
                  onClick={() => {
                    const v = form.getValues();
                    saveDraft(draftKey, v.identificationNumber, v.legalName);
                    setDraftBanner(loadDraft(draftKey));
                  }}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    save
                  </span>{" "}
                  Guardar borrador
                </ZHBtn>
              )}
              {step < 3 ? (
                <ZHBtn
                  type="button"
                  variant="primary"
                  size="md"
                  onClick={() => void goNext()}
                  disabled={step === 1 && !hasSearched && !notFound}
                >
                  Siguiente{" "}
                  <span className="material-symbols-outlined zh-icon-md">
                    arrow_forward
                  </span>
                </ZHBtn>
              ) : (
                <ZHBtn
                  type="submit"
                  variant="secondary"
                  size="md"
                  className="prd-btn--success"
                  disabled={submitting}
                >
                  {submitting ? (
                    <>
                      <span className="prd-spinner" aria-hidden /> Guardando…
                    </>
                  ) : (
                    <>
                      <span className="material-symbols-outlined zh-icon-md">
                        check
                      </span>
                      {isEdit ? "Guardar cambios" : `Crear ${roleLabel}`}
                    </>
                  )}
                </ZHBtn>
              )}
            </div>
          </div>
        </form>
      </div>
    </FormProvider>
  );
}
