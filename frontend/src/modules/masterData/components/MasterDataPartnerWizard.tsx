/**
 * MasterDataPartnerWizard V5 — pantalla única por secciones (ZH-MASTERDATA-PARTNER-FORM-UX-01/01B).
 *
 * ETAPAS (sin stepper visual): 1=Buscar, 2=Identificación (única sección de datos).
 * ELIMINADO (01): stepper numerado de 3 pasos — reemplazado por un solo card con secciones.
 * ELIMINADO (01B): sección "Revisar antes de guardar" — al no haber wizard por pasos,
 * repetía los datos recién ingresados en `MasterDataBpFormFields section="identity"` sin
 * aportar valor (solo aumentaba scroll y confundía). El aviso informativo final ("Al guardar
 * quedará disponible como {rol}") se conserva, ahora al pie de la sección de identificación.
 * ELIMINADO (V2): step "Contact" (email/phone) — los contactos van en BusinessPartnerContact
 * (POST /contacts), no en este formulario.
 *
 * V2 changes:
 *   - alreadyHasRole: simplificado — el backend devuelve 422 si el rol ya está activo.
 *   - legalEntityTypeCode: campo obligatorio en la sección de identificación.
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
import { MasterDataBpFormFields } from "./MasterDataBpFormFields";
import { ZHBtn } from "../../../components/zh/ZHForm";

/** 1 = Buscar y asignar; 2 = Identificación + Configuración comercial + Revisar (un solo tramo). */
type StepId = 1 | 2;
type Role = "customer" | "supplier";

/** Solo aplica cuando role='supplier' — ver SupplierRoleConfig (backend). */
export type SupplierConfigAtCreation = {
  refundProviderTypeCode: string;
  paymentTermId: string;
};

/** Precarga del paso 2 (Identidad) saltando el paso 1 (Buscar) — para flujos que ya saben que el
 * BP no existe (p.ej. crear proveedor desde un documento de recepción SRI con RUC/razón social
 * ya conocidos). No confundir con `editingPartner`: aquí seguimos creando, no editando. */
export type PartnerWizardInitialValues = {
  identificationType?: string;
  identificationNumber?: string;
  legalName?: string;
  tradeName?: string;
  countryCode?: string;
};

interface Props {
  role: Role;
  draftKey: string;
  submitting: boolean;
  editingPartner: BusinessPartnerSummaryDto | null;
  initialValues?: PartnerWizardInitialValues;
  /** Modo embebido en un modal ajeno a MasterData (p. ej. "Crear proveedor" desde Recepción
   * electrónica): oculta "Guardar borrador" — ese chrome multi-sesión no aporta en un formulario
   * de una sola pasada con datos ya precargados en un modal chico.
   * El wizard normal de MasterData (`MasterDataSuppliersPage`/`MasterDataCustomersPage`) no pasa
   * esta prop y sigue exactamente igual. */
  embedded?: boolean;
  onSubmitCreate: (
    body: CreateBusinessPartnerBody,
    supplierConfig?: SupplierConfigAtCreation,
  ) => Promise<void>;
  onSubmitUpdate: (body: UpdateBusinessPartnerBody) => Promise<void>;
  onAssignRole: (id: string) => Promise<void>;
  onCancel: () => void;
  onAssignSuccess?: () => void;
}

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
  initialValues,
  embedded = false,
  onSubmitCreate,
  onSubmitUpdate,
  onAssignRole,
  onCancel,
  onAssignSuccess,
}: Props) {
  const { t } = useI18n();
  const isEdit = !!editingPartner;
  // Salta el paso 1 (Buscar) tanto al editar como cuando el caller ya sabe que el BP no existe y
  // trae datos precargados (`initialValues`) — en ambos casos el paso 2 arranca con datos.
  const skipStep1 = isEdit || !!initialValues;
  const [step, setStep] = useState<StepId>(skipStep1 ? 2 : 1);

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
  const roleLabelLower = role === "customer" ? "cliente" : "proveedor";

  const form = useForm<BusinessPartnerFormValues>({
    resolver: zodResolver(businessPartnerSchema(role)),
    defaultValues: {
      identificationType:
        editingPartner?.identificationType ??
        initialValues?.identificationType ??
        "04",
      identificationNumber:
        editingPartner?.identificationNumber ??
        initialValues?.identificationNumber ??
        "",
      // Sin default: el backend infiere la naturaleza jurídica de RUC/CI; para el resto de
      // tipos de identificación el usuario debe elegirla explícitamente (ver MasterDataBpFormFields).
      legalEntityTypeCode: editingPartner?.legalEntityTypeCode ?? undefined,
      legalName: editingPartner?.legalName ?? initialValues?.legalName ?? "",
      tradeName: editingPartner?.tradeName ?? initialValues?.tradeName ?? "",
      countryCode:
        editingPartner?.countryCode ?? initialValues?.countryCode ?? "EC",
      refundProviderTypeCode: "",
      paymentTermId: "",
    },
  });

  useEffect(() => {
    if (!isEdit && !embedded) {
      const d = loadDraft(draftKey);
      if (d) setDraftBanner(d);
    }
  }, [draftKey, isEdit, embedded]);

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

  /** Avanza de "Buscar" a "Identificación + Revisar" — no valida (la validación real ocurre en
   * el submit, ya que ambas secciones viven en la misma pantalla). */
  const goNext = () => {
    if (step === 1) setStep(2);
  };
  const goPrev = () => {
    if (step === 2) setStep(1);
  };

  const buildCreateBody = (): CreateBusinessPartnerBody => {
    const v = form.getValues();
    return {
      identificationType: v.identificationType,
      identificationNumber: v.identificationNumber.trim(),
      legalEntityTypeCode: v.legalEntityTypeCode,
      legalName: v.legalName.trim(),
      tradeName: v.tradeName?.trim() || null,
      countryCode: v.countryCode?.trim() || null,
    };
  };

  const buildUpdateBody = (): UpdateBusinessPartnerBody => {
    const v = form.getValues();
    return {
      legalName: v.legalName.trim(),
      legalEntityTypeCode: v.legalEntityTypeCode,
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
        {/* Card único con secciones (sin stepper) — ver B/C/D/E de ZH-MASTERDATA-PARTNER-FORM-UX-01. */}
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                {role === "customer" ? "group" : "local_shipping"}
              </span>
              <span className="pg-section-label">
                {isEdit ? `Editar ${roleLabel}` : `Nuevo ${roleLabel}`}
              </span>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="prd-wiz-panel__desc md-partner-intro">
              {role === "customer"
                ? t(
                    "masterdata.wizard.intro.customer",
                    "Crea o asigna una persona o empresa para ventas, facturación y cuentas por cobrar.",
                  )
                : t(
                    "masterdata.wizard.intro.supplier",
                    "Crea o asigna una persona o empresa para compras, gastos, retenciones y cuentas por pagar.",
                  )}
            </p>

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
              {/* ── Sección: ¿Ya existe en el sistema? (búsqueda) ─────────── */}
              {step === 1 && !skipStep1 && (
                <div className="md-partner-subsection">
                  <h3 className="prd-wiz-panel__title">
                    {t(
                      "masterdata.wizard.search.title",
                      "¿Ya existe en el sistema?",
                    )}
                  </h3>
                  <p className="prd-wiz-panel__desc">
                    Busca por RUC, cédula o razón social para evitar
                    duplicados.
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

              {/* ── Sección: Identificación/Config. comercial (sin resumen repetido — ver
                  ZH-MASTERDATA-PARTNER-FORM-UX-01B: "Revisar antes de guardar" duplicaba
                  los datos recién ingresados sin aportar valor, ya que no hay wizard por
                  pasos que justifique una revisión aparte). ── */}
              {step === 2 && (
                <div className="md-partner-subsection">
                  {notFound && !isEdit && (
                    <div className="prd-wiz-notfound-banner" role="status">
                      <span className="material-symbols-outlined">
                        search_off
                      </span>
                      <span>
                        No se encontró <strong>"{notFound}"</strong> —
                        completa los datos para registrar un nuevo{" "}
                        {roleLabelLower}.
                      </span>
                    </div>
                  )}
                  <h3 className="prd-wiz-panel__title">
                    {isEdit
                      ? "Editar datos principales"
                      : `Datos principales del ${roleLabelLower}`}
                  </h3>
                  <p className="prd-wiz-panel__desc">
                    Después de guardar podrás completar contactos, teléfonos y
                    direcciones desde la ficha.
                  </p>
                  <MasterDataBpFormFields
                    section="identity"
                    saving={submitting}
                    usage={role}
                  />
                  <div className="prd-review-warning" role="note">
                    <span className="material-symbols-outlined zh-icon-lg">
                      info
                    </span>
                    <span>
                      Al guardar quedará disponible como{" "}
                      <strong>{roleLabelLower}</strong>.
                    </span>
                  </div>
                </div>
              )}

              {/* ── Footer ───────────────────────────────────────────────── */}
              <div className="prd-wiz-footer">
                <div className="prd-wiz-footer__left">
                  {!skipStep1 && step === 2 && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="md"
                      onClick={goPrev}
                    >
                      <span className="material-symbols-outlined zh-icon-md">
                        arrow_back
                      </span>
                      {t("masterdata.wizard.btn.prev", "Anterior")}
                    </ZHBtn>
                  )}
                </div>
                <div className="prd-wiz-footer__right">
                  {(isEdit || step === 2) && (
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
                      {t("masterdata.wizard.search.skip", "+ Crear sin buscar")}
                    </ZHBtn>
                  )}
                  {step === 2 && !embedded && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="md"
                      onClick={() => {
                        const v = form.getValues();
                        saveDraft(
                          draftKey,
                          v.identificationNumber,
                          v.legalName,
                        );
                        setDraftBanner(loadDraft(draftKey));
                      }}
                    >
                      <span className="material-symbols-outlined zh-icon-md">
                        save
                      </span>{" "}
                      Guardar borrador
                    </ZHBtn>
                  )}
                  {step === 1 ? (
                    <ZHBtn
                      type="button"
                      variant="primary"
                      size="md"
                      onClick={goNext}
                      disabled={!hasSearched && !notFound}
                    >
                      Continuar{" "}
                      <span className="material-symbols-outlined zh-icon-md">
                        arrow_forward
                      </span>
                    </ZHBtn>
                  ) : (
                    <ZHBtn
                      type="submit"
                      variant="success"
                      size="md"
                      disabled={submitting}
                    >
                      {submitting ? (
                        <>
                          <span className="prd-spinner" aria-hidden />{" "}
                          {t("masterdata.wizard.btn.saving", "Guardando…")}
                        </>
                      ) : (
                        <>
                          <span className="material-symbols-outlined zh-icon-md">
                            check
                          </span>
                          {isEdit
                            ? t("masterdata.wizard.btn.update", "Guardar cambios")
                            : t("masterdata.wizard.btn.create", {
                                role: roleLabel,
                              })}
                        </>
                      )}
                    </ZHBtn>
                  )}
                </div>
              </div>
            </form>
          </div>
        </div>
      </div>
    </FormProvider>
  );
}
