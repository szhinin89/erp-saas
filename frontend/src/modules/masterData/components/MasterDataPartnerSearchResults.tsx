/**
 * Lista de resultados de búsqueda del MasterDataPartnerWizard con la acción "Asignar como {rol}"
 * (ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06). Extraído del wizard sin cambios de
 * comportamiento; el estado (búsqueda, asignación) sigue viviendo en el wizard.
 */
import { useI18n } from "../../../i18n/i18n";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";
import { ZHBtn } from "../../../components/zh/ZHForm";

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

interface Props {
  role: Role;
  results: BusinessPartnerSummaryDto[];
  assigning: string | null;
  submitting: boolean;
  onAssign: (bp: BusinessPartnerSummaryDto) => void;
}

export function MasterDataPartnerSearchResults({
  role,
  results,
  assigning,
  submitting,
  onAssign,
}: Props) {
  const { t } = useI18n();
  const roleLabel = role === "customer" ? "Cliente" : "Proveedor";
  const roleLabelLower = role === "customer" ? "cliente" : "proveedor";
  const otherRoleLabelLower = role === "customer" ? "proveedor" : "cliente";

  return (
    <ul className="md-search-results">
      {results.map((bp) => {
        const busy = assigning === bp.id || submitting;
        const resultState = getPartnerSearchResultState(bp, role);
        return (
          <li key={bp.id} className="md-search-result-item">
            <div className="md-search-result-info">
              <span className="md-search-result-name">{bp.legalName}</span>
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
                onClick={() => onAssign(bp)}
              >
                {busy
                  ? t("masterdata.wizard.search.assigning", "Asignando…")
                  : t("masterdata.wizard.search.assign", { role: roleLabel })}
              </ZHBtn>
            )}
          </li>
        );
      })}
    </ul>
  );
}
