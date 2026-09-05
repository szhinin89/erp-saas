/**
 * MasterDataPartnerWizard V6 — formulario único visible desde el inicio, con habilitación
 * progresiva de campos (ZH-MASTERDATA-PARTNER-PROGRESSIVE-FORM-UX-02).
 *
 * V6: se elimina el último resto de wizard por pasos (paso 1 "Buscar" / paso 2
 * "Identificación"):
 *   - La búsqueda y el formulario conviven siempre en la misma pantalla — el formulario se
 *     renderiza desde el inicio, con los campos deshabilitados (<fieldset disabled>) hasta
 *     que el usuario busca sin encontrar resultados o elige "Crear nuevo registro".
 *   - Sin botones "Anterior"/"Continuar": la única navegación adicional es "Cambiar
 *     búsqueda" (vuelve a bloquear el formulario), visible solo cuando ya se habilitó a
 *     partir de una búsqueda.
 *   - Si la búsqueda encuentra resultados, se ofrece "Asignar como {rol}" como acción
 *     principal; "Crear nuevo registro" sigue disponible como acción secundaria.
 *   - En edición (o cuando el caller precarga `initialValues`, p. ej. desde Compras) el
 *     formulario sigue arrancando habilitado y sin bloque de búsqueda, igual que antes.
 *
 * Historial previo (ver git log para detalle de V2 a V5): eliminación del stepper numerado
 * (01), de la sección "Revisar antes de guardar" (01B), migración a RHF+zod (V3).
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
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";

type Role = "customer" | "supplier";

/**
 * Estado de un resultado de búsqueda respecto al rol que se está creando/asignando
 * (ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06). El backend expone isCustomer/isSupplier
 * directamente en `BusinessPartnerSummaryDto` (ver ZH-MASTERDATA-PARTNER-SEARCH-ROLE-FLAGS-API-07)
 * — una sola llamada de búsqueda trae todo lo necesario, sin inferencias ni llamadas extra.
 */
export type PartnerSearchResultState =
  | "canAssignNoRole"
  | "canAssignOtherRole"
  | "alreadyTarget"
  | "alreadyBoth";

/** Pura y testeable sin mocks de API — ver ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06 §9. */
export function getPartnerSearchResultState(
  bp: BusinessPartnerSummaryDto,
  targetRole: Role,
): PartnerSearchResultState {
  const hasTarget = targetRole === "customer" ? bp.isCustomer : bp.isSupplier;
  const hasOther = targetRole === "customer" ? bp.isSupplier : bp.isCustomer;
  if (hasTarget && hasOther) return "alreadyBoth";
  if (hasTarget) return "alreadyTarget";
  if (hasOther) return "canAssignOtherRole";
  return "canAssignNoRole";
}

/** Solo aplica cuando role='supplier' — ver SupplierRoleConfig (backend). */
export type SupplierConfigAtCreation = {
  refundProviderTypeCode: string;
  paymentTermId: string;
};

/** Precarga del formulario saltando la búsqueda — para flujos que ya saben que el BP no
 * existe (p.ej. crear proveedor desde un documento de recepción SRI con RUC/razón social ya
 * conocidos). No confundir con `editingPartner`: aquí seguimos creando, no editando. */
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
  // Oculta la búsqueda y arranca con el formulario habilitado tanto al editar como cuando el
  // caller ya sabe que el BP no existe y trae datos precargados (`initialValues`).
  const skipSearch = isEdit || !!initialValues;
  const [formEnabled, setFormEnabled] = useState(skipSearch);

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
    setNotFound(null);
    setResults([]);
    try {
      const rows = await businessPartnerService.search({ q, take: 10 });
      setResults(rows);
      setHasSearched(true);
      if (rows.length === 0) {
        prefill(q);
        setNotFound(q);
        setFormEnabled(true);
      }
    } catch (err) {
      setSearchError(
        formatApiRequestError(err, {
          generic: t(
            "masterdata.wizard.search.error",
            "Error al buscar. Intente de nuevo.",
          ),
        }),
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
        formatApiRequestError(err, {
          generic: t(
            "masterdata.wizard.search.assignError",
            "Error al asignar el rol.",
          ),
        }),
      );
    } finally {
      setAssigning(null);
    }
  };

  const handleCreateNew = () => {
    prefill(query);
    setSearchError("");
    setFormEnabled(true);
  };

  const handleChangeSearch = () => {
    setFormEnabled(false);
    setNotFound(null);
    setResults([]);
    setHasSearched(false);
    setSearchError("");
    queryRef.current?.focus();
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
            generic: t(
              "masterdata.wizard.submit.error",
              "Error al guardar. Revisa los datos.",
            ),
          }),
        );
    }
  };

  return (
    <FormProvider {...form}>
      <div className="prd-wizard prd-fadein">
        {/* Card único con secciones (sin stepper) — ver ZH-MASTERDATA-PARTNER-PROGRESSIVE-FORM-UX-02. */}
        <div className="pg-section md-partner-card">
          <div className="pg-section-header md-partner-card-header">
            <div className="md-partner-card-title-block">
              <div className="pg-section-header-left">
                <span className="material-symbols-outlined pg-section-icon">
                  {role === "customer" ? "group" : "local_shipping"}
                </span>
                <span className="pg-section-label">
                  {isEdit ? `Editar ${roleLabel}` : `Nuevo ${roleLabel}`}
                </span>
              </div>
              {/* Descripción general — pertenece al título, no al body
                  (ZH-MASTERDATA-PARTNER-HEADER-MESSAGE-ALIGNMENT-04F). */}
              <p className="md-partner-card-description">
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
            </div>

            {/* Buscador integrado en la cabecera del card — ver
                ZH-MASTERDATA-PARTNER-HEADER-SEARCH-UX-04E. Ya no es una caja/sección aparte.
                El mensaje de ayuda/no-encontrado/error vive junto al input, no en el body
                (ZH-MASTERDATA-PARTNER-HEADER-MESSAGE-ALIGNMENT-04F). */}
            {!skipSearch && (
              <div className="md-partner-header-search">
                <div className="md-partner-header-search-row">
                  <div className="prd-search-box">
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
                  </div>
                  <ZHBtn
                    type="button"
                    variant="primary"
                    size="md"
                    disabled={searching || !query.trim()}
                    onClick={() => void handleSearch()}
                  >
                    {searching ? "Buscando…" : "Buscar"}
                  </ZHBtn>
                  {!formEnabled && (
                    <ZHBtn
                      type="button"
                      variant="secondary"
                      size="md"
                      onClick={handleCreateNew}
                    >
                      {t(
                        "masterdata.wizard.search.skip",
                        "Crear nuevo registro",
                      )}
                    </ZHBtn>
                  )}
                </div>

                <div className="md-partner-search-notice">
                  {searchError && (
                    <ZHPageNotice
                      variant="error"
                      message={searchError}
                      className="md-partner-notice-compact"
                    />
                  )}

                  {!searchError && !formEnabled && !hasSearched && !notFound && (
                    <ZHPageNotice
                      variant="info"
                      className="md-partner-notice-compact"
                      message={
                        role === "customer"
                          ? t(
                              "masterdata.wizard.search.crossRoleHint.customer",
                              "Busca primero para evitar duplicados. Si ya existe como proveedor, puedes asignarlo también como cliente sin duplicarlo.",
                            )
                          : t(
                              "masterdata.wizard.search.crossRoleHint.supplier",
                              "Busca primero para evitar duplicados. Si ya existe como cliente, puedes asignarlo también como proveedor sin duplicarlo.",
                            )
                      }
                    />
                  )}

                  {!searchError && notFound && (
                    <ZHPageNotice
                      variant="info"
                      className="md-partner-notice-compact"
                      message={t("masterdata.wizard.search.notFound", {
                        query: notFound,
                        role: roleLabelLower,
                      })}
                    />
                  )}
                </div>
              </div>
            )}
          </div>
          <div className="pg-section-body">
            {!skipSearch && hasSearched && results.length > 0 && (
              <ul className="md-search-results">
                {results.map((bp) => {
                  const busy = assigning === bp.id || submitting;
                  const resultState = getPartnerSearchResultState(bp, role);
                  const otherRoleLabelLower =
                    role === "customer" ? "proveedor" : "cliente";
                  return (
                    <li key={bp.id} className="md-search-result-item">
                      <div className="md-search-result-info">
                        <span className="md-search-result-name">
                          {bp.legalName}
                        </span>
                        <span className="md-search-result-id mono">
                          {bp.identificationNumber}
                        </span>
                        <span className="md-search-result-status">
                          {resultState === "alreadyBoth" &&
                            t(
                              "masterdata.wizard.search.alreadyBoth",
                              "Ya está registrado como cliente y proveedor.",
                            )}
                          {resultState === "alreadyTarget" &&
                            t("masterdata.wizard.search.alreadyTarget", {
                              role: roleLabelLower,
                            })}
                          {resultState === "canAssignOtherRole" &&
                            t("masterdata.wizard.search.existsAsOtherRole", {
                              role: otherRoleLabelLower,
                            })}
                          {resultState === "canAssignNoRole" &&
                            t("masterdata.wizard.search.noRoleYet", {
                              role: roleLabelLower,
                            })}
                        </span>
                      </div>
                      {(resultState === "canAssignNoRole" ||
                        resultState === "canAssignOtherRole") && (
                        <ZHBtn
                          type="button"
                          variant="primary"
                          size="sm"
                          disabled={busy}
                          onClick={() => void handleAssign(bp)}
                        >
                          {busy
                            ? t(
                                "masterdata.wizard.search.assigning",
                                "Asignando…",
                              )
                            : t("masterdata.wizard.search.assign", {
                                role: roleLabel,
                              })}
                        </ZHBtn>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}

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
                      setFormEnabled(true);
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
              <ZHPageNotice variant="error" message={bannerError} />
            )}

            <form
              className="prd-wiz-form"
              onSubmit={form.handleSubmit(onValidSubmit)}
            >
              {/* ── Formulario (visible desde el inicio, habilitado progresivamente) ── */}
              <div className="md-partner-subsection">
                <h3 className="prd-wiz-panel__title">
                  {isEdit
                    ? "Editar datos principales"
                    : `Datos principales del ${roleLabelLower}`}
                </h3>
                <fieldset
                  className="md-partner-fieldset"
                  disabled={!formEnabled}
                >
                  <MasterDataBpFormFields
                    section="identity"
                    saving={submitting}
                    usage={role}
                  />
                </fieldset>
                <ZHPageNotice
                  variant="info"
                  className="md-partner-final-notice md-partner-notice-compact"
                  message={
                    role === "customer"
                      ? t(
                          "masterdata.wizard.finalNotice.customer",
                          "Quedará disponible para ventas, facturación y cuentas por cobrar.",
                        )
                      : t(
                          "masterdata.wizard.finalNotice.supplier",
                          "Quedará disponible para compras, gastos, retenciones y cuentas por pagar.",
                        )
                  }
                />
              </div>

              {/* ── Footer ───────────────────────────────────────────────── */}
              <div className="prd-wiz-footer">
                <div className="prd-wiz-footer__left">
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    size="md"
                    onClick={onCancel}
                  >
                    Cancelar
                  </ZHBtn>
                </div>
                <div className="prd-wiz-footer__right">
                  {!isEdit && !skipSearch && formEnabled && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="md"
                      onClick={handleChangeSearch}
                    >
                      {t(
                        "masterdata.wizard.search.changeSearch",
                        "Cambiar búsqueda",
                      )}
                    </ZHBtn>
                  )}
                  {!isEdit && !embedded && (
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="md"
                      disabled={!formEnabled}
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
                  <ZHBtn
                    type="submit"
                    variant="success"
                    size="md"
                    disabled={submitting || (!isEdit && !formEnabled)}
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
                </div>
              </div>
            </form>
          </div>
        </div>
      </div>
    </FormProvider>
  );
}
