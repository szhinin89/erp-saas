import { buildNotice, type NoticeVM, type NoticeSeverity } from "../../../notices";
import type { useI18n } from "../../../i18n/i18n";
import type { buildPurchaseLinePresentation } from "./purchaseLinePresentation";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";

const ISSUE_SEVERITY_RANK: Record<NoticeSeverity, number> = { danger: 3, warning: 2, info: 1, success: 0 };
const toneToSeverity = (tone: "success" | "warning" | "danger"): NoticeSeverity => tone;

/** One presentation model for the line tag and the form's navigation summary. No validation rules. */
export function buildPurchaseLineIssues({ vm, readiness, t, hasFieldErrors, readinessHasFieldError,
  showReadinessActions, lineLocked, onReset, onGoToField }: {
  vm: ReturnType<typeof buildPurchaseLinePresentation>;
  readiness?: PurchaseLineReadiness;
  t: ReturnType<typeof useI18n>["t"];
  hasFieldErrors?: boolean;
  readinessHasFieldError?: string | false;
  showReadinessActions?: string | false;
  lineLocked?: boolean;
  onReset?: () => void;
  onGoToField?: () => void;
}) {
  const lineIssues: NoticeVM[] = [];
  if (hasFieldErrors) lineIssues.push(buildNotice({
    severity: "danger", intent: "blocking", source: "api-validation",
    label: t("purchases.validation.lineFields", "Campos por corregir"),
    detail: t("purchases.validation.lineFieldsDetail", "Revise los campos marcados de esta línea."),
    ...(onGoToField ? { actionLabel: t("purchases.validation.goToField", "Ir al campo"), onAction: onGoToField } : {}),
  }));
  if (!readinessHasFieldError && readiness?.blocking) {
    lineIssues.push(buildNotice({
      severity: toneToSeverity(readiness.tone),
      intent: "blocking",
      source: "domain-status",
      label: readiness.label,
      detail: showReadinessActions
        ? `${readiness.detail} ${t("purchases.lineReadiness.nextAction", "Acción")}: ${showReadinessActions}`
        : readiness.detail,
    }));
  } else if (!readinessHasFieldError && readiness && readiness.status !== "READY" && readiness.status !== "MISSING_ITEM") {
    lineIssues.push(buildNotice({
      severity: toneToSeverity(readiness.tone),
      intent: "status",
      source: "domain-status",
      label: readiness.label,
      detail: readiness.detail,
    }));
  } else if (!readinessHasFieldError && !readiness && vm.status.tone !== "success") {
    lineIssues.push(buildNotice({
      severity: toneToSeverity(vm.status.tone),
      intent: "status",
      source: "domain-status",
      label: vm.status.label,
    }));
  }
  if (vm.registration.originDiffers) {
    lineIssues.push(buildNotice({
      severity: "warning",
      intent: "actionRequired",
      source: "domain-status",
      label: t("purchases.lines.registrationOriginDiffers", "Cantidad o costo distintos al documento del proveedor"),
      detail: vm.registration.originDetail,
      ...(lineLocked || !onReset
        ? {}
        : {
            actionLabel: t("purchases.lines.resetToSupplierOrigin", "Recalcular desde XML"),
            onAction: onReset,
          }),
    }));
  }
  // PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — no bloqueante; puede coexistir con un bloqueo.
  if (readiness?.warning) {
    lineIssues.push(buildNotice({
      severity: toneToSeverity(readiness.warning.tone),
      intent: "status",
      source: "domain-status",
      label: readiness.warning.label,
      detail: readiness.warning.detail,
    }));
  }
  if (vm.commercial.costs.showDeviationAlert) {
    lineIssues.push(buildNotice({
      severity: "warning",
      intent: "status",
      source: "domain-status",
      label: t("purchases.lines.costDeviation", "Costo fuera de rango"),
      detail: vm.commercial.costs.deviationLabel,
    }));
  }
  // Orden estable por severidad: la etiqueta muestra la primera (mayor prioridad).
  lineIssues.sort((a, b) => ISSUE_SEVERITY_RANK[b.severity] - ISSUE_SEVERITY_RANK[a.severity]);
  return lineIssues;
}
