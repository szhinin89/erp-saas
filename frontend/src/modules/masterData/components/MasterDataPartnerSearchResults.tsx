/**
 * Lista de resultados de búsqueda del MasterDataPartnerWizard con la acción "Asignar como {rol}"
 * (ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06). Extraído del wizard sin cambios de
 * comportamiento; el estado (búsqueda, asignación) sigue viviendo en el wizard.
 */
import { useI18n } from "../../../i18n/i18n";
import type { BusinessPartnerSummaryDto } from "../types/businessPartner.types";
import { ZHBtn } from "../../../components/zh/ZHForm";
import {
  SRI_ID_TYPE_CEDULA,
  SRI_ID_TYPE_PASSPORT,
} from "../constants/sriIdentificationCodes";

type Role = "customer" | "supplier" | "employee";

/** EMP-BR-02: un empleado se identifica con cédula o pasaporte; el backend es la autoridad. */
const EMPLOYEE_ID_TYPES: readonly string[] = [SRI_ID_TYPE_CEDULA, SRI_ID_TYPE_PASSPORT];

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
  | "alreadyBoth"
  | "employeeCandidate"
  | "employeeIdNotAllowed";

/** Pura y testeable sin mocks de API — ver ZH-MASTERDATA-PARTNER-FUNCTIONAL-CASE-MATRIX-06 §9. */
export function getPartnerSearchResultState(
  bp: BusinessPartnerSummaryDto,
  targetRole: Role,
): PartnerSearchResultState {
  // La búsqueda no informa el rol Employee: se ofrece asignar y el backend rechaza si ya está activo.
  if (targetRole === "employee") {
    return EMPLOYEE_ID_TYPES.includes(bp.identificationType)
      ? "employeeCandidate"
      : "employeeIdNotAllowed";
  }
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
  const roleLabel = { customer: "Cliente", supplier: "Proveedor", employee: "Empleado" }[role];
  const roleLabelLower = roleLabel.toLowerCase();
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
                {resultState === "employeeCandidate" &&
                  t(
                    "masterdata.wizard.search.employeeCandidate",
                    "Registro encontrado. Asígnele el rol Empleado si aún no lo tiene.",
                  )}
                {resultState === "employeeIdNotAllowed" &&
                  t(
                    "masterdata.wizard.search.employeeIdNotAllowed",
                    "Su tipo de identificación no admite el rol Empleado. Solo cédula o pasaporte.",
                  )}
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
              resultState === "canAssignOtherRole" ||
              resultState === "employeeCandidate") && (
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
