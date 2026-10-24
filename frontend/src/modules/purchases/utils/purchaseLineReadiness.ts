import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type { PrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { matchesXmlDocument } from "./purchaseCalc";
import {
  buildMarginRiskWarning,
  buildMissingSalePriceForMarginWarning,
  buildSuspiciousPackagingCostWarning,
} from "./purchaseLinePresentation";

type TFunction = (
  key: string,
  fallbackOrParams?: string | Record<string, string | number>,
) => string;

export type PurchaseLineReadinessStatus =
  | "MISSING_ITEM"
  | "SUPPLIER_CODE_CONFLICT"
  | "MISSING_PRESENTATION"
  | "SUSPICIOUS_PACKAGING_COST"
  | "MISSING_WAREHOUSE"
  | "INVALID_TAX"
  | "ICE_XML_EDIT_BLOCKED"
  | "READY";

export type PurchaseLineReadinessAction =
  | "SELECT_ITEM"
  | "CREATE_ITEM"
  | "SELECT_PRESENTATION"
  | "SAVE_PRESENTATION"
  | "SELECT_WAREHOUSE"
  | "REVIEW_TAX"
  | "NONE";

export type PurchaseLineReadinessTone = "success" | "warning" | "danger";

/**
 * PURCHASE-LINE-MISSING-SALE-PRICE-WARNING-01 — aviso no bloqueante, independiente del `status`
 * principal (que solo admite un valor mutuamente excluyente por línea). Se calcula y adjunta
 * aparte para no competir por prioridad con MISSING_WAREHOUSE/INVALID_TAX/etc.: una línea puede
 * estar bloqueada por otra razón y, a la vez, no tener precio de venta configurado para evaluar
 * margen — ambas cosas deben poder verse.
 * Es el aviso COMERCIAL de la línea: sin PVP, margen negativo o margen cero (excluyentes entre sí:
 * los dos últimos exigen PVP). El modal de confirmación los resume sin enumerarlos por producto.
 */
export interface PurchaseLineReadinessWarning {
  status: "MISSING_SALE_PRICE_FOR_MARGIN" | "NEGATIVE_MARGIN" | "ZERO_MARGIN";
  label: string;
  detail: string;
  tone: "warning";
}

export interface PurchaseLineReadiness {
  status: PurchaseLineReadinessStatus;
  label: string;
  detail: string;
  tone: PurchaseLineReadinessTone;
  blocking: boolean;
  primaryAction: PurchaseLineReadinessAction;
  secondaryAction?: PurchaseLineReadinessAction;
  /** No bloqueante — ver PurchaseLineReadinessWarning. Ausente si no aplica. */
  warning?: PurchaseLineReadinessWarning;
}

export interface PurchaseLineReadinessOptions {
  /** ZH-DESIGN-SYSTEM-PRECISION-06 — policy de la empresa (caller React: `usePrecisionPolicy`). */
  precisionPolicy: PrecisionPolicy;
  globalWarehouseId?: string | null;
  vatRates?: Record<string, number>;
  iceRates?: Record<string, number>;
  t?: TFunction;
}

const fallbackT: TFunction = (_key, fallbackOrParams) =>
  typeof fallbackOrParams === "string" ? fallbackOrParams : _key;

function hasXmlOrigin(line: PurchaseLineFormValues) {
  return !!line.purchaseReceptionLineId || !!line.xmlSupplierCode;
}

function hasWarehouse(line: PurchaseLineFormValues, globalWarehouseId?: string | null) {
  return !!(line.warehouseId?.trim() || globalWarehouseId?.trim());
}

/**
 * PURCHASES-HARDENING-P0 (B8) — misma regla que el backend (Create/Update/ConfirmPurchase): solo
 * una línea con Item inventariable mueve stock y exige bodega. Sin Item (línea libre) o con un Item
 * que el contexto informa como no inventariable (servicio), la bodega no aplica. Contexto aún no
 * cargado → se asume inventariable (fail-closed).
 */
function requiresWarehouse(line: PurchaseLineFormValues) {
  return !!line.itemId && line.context?.participatesInInventory !== false;
}

function hasKnownRate(code: string, rates?: Record<string, number>) {
  if (!rates || Object.keys(rates).length === 0) return true;
  return Object.prototype.hasOwnProperty.call(rates, code);
}

function hasXmlIce(line: PurchaseLineFormValues) {
  return line.xmlIceAmount != null;
}

function hasInvalidTax(line: PurchaseLineFormValues, options: PurchaseLineReadinessOptions) {
  if (!line.vatCode?.trim()) return true;
  if (!hasKnownRate(line.vatCode, options.vatRates)) return true;
  // ICE del XML: se reconoce tal como fue declarado (incluso código inactivo); su edición la
  // evalúa hasBlockedXmlIceEdit. Solo la selección manual exige código activo calculable.
  if (!hasXmlIce(line) && line.iceCode?.trim() && !hasKnownRate(line.iceCode, options.iceRates))
    return true;
  return false;
}

/**
 * Mismo criterio que el backend (PURCHASE_ICE_XML_EDIT_BLOCKED): editar cantidad, precio o
 * descuento de una línea XML con ICE solo es posible si el código es porcentual y activo
 * (`iceRates` contiene únicamente esos). ICE específico/mixto o inactivo no se recalcula.
 */
function hasBlockedXmlIceEdit(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions,
) {
  if (line.xmlAllocation || !hasXmlIce(line) || matchesXmlDocument(line)) return false;
  const code = line.iceCode?.trim();
  return !code || !hasKnownRate(code, options.iceRates);
}

function requiresPresentation(line: PurchaseLineFormValues) {
  return hasXmlOrigin(line) && !!line.itemId && line.context?.participatesInInventory === true;
}

function message(
  t: TFunction,
  status: PurchaseLineReadinessStatus,
): Pick<PurchaseLineReadiness, "label" | "detail"> {
  switch (status) {
    case "MISSING_ITEM":
      return {
        label: t(
          "purchases.lineReadiness.missingItem",
          "Sin producto vinculado",
        ),
        detail: t(
          "purchases.lineReadiness.missingItemDetail",
          "Esta línea del XML todavía no está vinculada a un producto del sistema. Seleccione un producto existente o cree uno nuevo para continuar.",
        ),
      };
    case "SUPPLIER_CODE_CONFLICT":
      return {
        label: t(
          "purchases.lineReadiness.supplierCodeConflict",
          "El código del proveedor está asociado a otro ítem.",
        ),
        detail: t(
          "purchases.lineReadiness.supplierCodeConflictDetail",
          "Corrija el ítem seleccionado o actualice el código del proveedor desde esta pantalla.",
        ),
      };
    case "MISSING_PRESENTATION":
      return {
        label: t(
          "purchases.lineReadiness.missingPresentation",
          "Sin presentación vinculada",
        ),
        detail: t(
          "purchases.lineReadiness.missingPresentationDetail",
          "Este producto ya está vinculado, pero falta indicar la presentación que entrega el proveedor. Seleccione una presentación y guárdela para continuar.",
        ),
      };
    case "MISSING_WAREHOUSE":
      return {
        label: t("purchases.lineReadiness.missingWarehouse", "Falta bodega"),
        detail: t(
          "purchases.lineReadiness.missingWarehouseDetail",
          "Asigne una bodega global o una bodega para esta línea.",
        ),
      };
    case "INVALID_TAX":
      return {
        label: t("purchases.lineReadiness.invalidTax", "Impuesto no reconocido"),
        detail: t(
          "purchases.lineReadiness.invalidTaxDetail",
          "Revise el código IVA/ICE de la línea antes de guardar.",
        ),
      };
    case "ICE_XML_EDIT_BLOCKED":
      return {
        label: t("purchases.lineReadiness.iceXmlEditBlocked", "ICE del XML no recalculable"),
        detail: t(
          "purchases.lineReadiness.iceXmlEditBlockedDetail",
          "El ICE específico, mixto o inactivo del XML no se recalcula sin bases físicas verificadas. Restablezca cantidad, precio y descuento del XML.",
        ),
      };
    case "SUSPICIOUS_PACKAGING_COST":
      return {
        label: t(
          "purchases.lineReadiness.suspiciousPackagingCost",
          "Presentación sospechosa (margen extremo)",
        ),
        detail: "",
      };
    case "READY":
      return {
        label: t("purchases.lineReadiness.ready", "Producto listo"),
        detail: t(
          "purchases.lineReadiness.readyDetail",
          "La línea tiene los datos operativos necesarios para continuar.",
        ),
      };
  }
}

function buildMissingSalePriceWarning(
  line: PurchaseLineFormValues,
  policy: PrecisionPolicy,
  t: TFunction,
  rawT?: TFunction,
): PurchaseLineReadinessWarning | undefined {
  const built = buildMissingSalePriceForMarginWarning(line, policy, rawT);
  if (!built) return undefined;
  return {
    status: "MISSING_SALE_PRICE_FOR_MARGIN",
    label: t(
      "purchases.lineReadiness.missingSalePriceForMargin",
      "Sin precio de venta configurado",
    ),
    detail: built.message,
    tone: "warning",
  };
}

function buildMarginWarning(
  line: PurchaseLineFormValues,
  policy: PrecisionPolicy,
  t: TFunction,
  rawT?: TFunction,
): PurchaseLineReadinessWarning | undefined {
  const built = buildMarginRiskWarning(line, policy, rawT);
  if (!built) return undefined;
  return built.kind === "negative"
    ? {
        status: "NEGATIVE_MARGIN",
        label: t("purchases.lineReadiness.negativeMargin", "Margen negativo"),
        detail: built.message,
        tone: "warning",
      }
    : {
        status: "ZERO_MARGIN",
        label: t("purchases.lineReadiness.zeroMargin", "Margen cero"),
        detail: built.message,
        tone: "warning",
      };
}

export function getPurchaseLineReadiness(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions,
): PurchaseLineReadiness {
  const t = options.t ?? fallbackT;
  // Igual que con buildSuspiciousPackagingCostWarning más abajo: se le pasa options.t crudo
  // (no el `t` ya resuelto con el fallback de este archivo) para que distinga "sin traductor
  // real" de "hay traductor real" y use su propio fallback interpolado en el primer caso.
  const warning =
    buildMissingSalePriceWarning(line, options.precisionPolicy, t, options.t) ??
    buildMarginWarning(line, options.precisionPolicy, t, options.t);

  const primary = computePrimaryReadiness(line, options, t);
  return warning ? { ...primary, warning } : primary;
}

function computePrimaryReadiness(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions,
  t: TFunction,
): PurchaseLineReadiness {
  if (hasXmlOrigin(line) && !line.itemId) {
    return {
      status: "MISSING_ITEM",
      ...message(t, "MISSING_ITEM"),
      tone: "danger",
      blocking: true,
      primaryAction: "SELECT_ITEM",
      secondaryAction: "CREATE_ITEM",
    };
  }

  if (line._readinessIssue === "SUPPLIER_CODE_CONFLICT") {
    return {
      status: "SUPPLIER_CODE_CONFLICT",
      ...message(t, "SUPPLIER_CODE_CONFLICT"),
      tone: "danger",
      blocking: true,
      primaryAction: "SELECT_ITEM",
    };
  }

  if (requiresPresentation(line) && !line.packagingLevelId) {
    return {
      status: "MISSING_PRESENTATION",
      ...message(t, "MISSING_PRESENTATION"),
      tone: "warning",
      blocking: true,
      primaryAction: "SELECT_PRESENTATION",
      secondaryAction: "SAVE_PRESENTATION",
    };
  }

  // t (arriba) siempre está definido (fallbackT como default) para el label/detail estáticos de
  // este módulo — pero buildSuspiciousPackagingCostWarning necesita distinguir "sin traductor
  // real" (usa su propio fallback interpolado) de "hay traductor real" (usa i18next), así que se
  // le pasa options.t directamente, nunca el `t` ya resuelto con el default de este archivo.
  const suspiciousPackagingCost = buildSuspiciousPackagingCostWarning(line, options.precisionPolicy, options.t);
  if (suspiciousPackagingCost) {
    return {
      status: "SUSPICIOUS_PACKAGING_COST",
      ...message(t, "SUSPICIOUS_PACKAGING_COST"),
      detail: suspiciousPackagingCost.message,
      tone: "danger",
      blocking: true,
      primaryAction: "SELECT_PRESENTATION",
    };
  }

  if (requiresWarehouse(line) && !hasWarehouse(line, options.globalWarehouseId)) {
    return {
      status: "MISSING_WAREHOUSE",
      ...message(t, "MISSING_WAREHOUSE"),
      tone: "warning",
      blocking: true,
      primaryAction: "SELECT_WAREHOUSE",
    };
  }

  if (hasBlockedXmlIceEdit(line, options)) {
    return {
      status: "ICE_XML_EDIT_BLOCKED",
      ...message(t, "ICE_XML_EDIT_BLOCKED"),
      tone: "danger",
      blocking: true,
      primaryAction: "REVIEW_TAX",
    };
  }

  if (hasInvalidTax(line, options)) {
    return {
      status: "INVALID_TAX",
      ...message(t, "INVALID_TAX"),
      tone: "danger",
      blocking: true,
      primaryAction: "REVIEW_TAX",
    };
  }

  return {
    status: "READY",
    ...message(t, "READY"),
    tone: "success",
    blocking: false,
    primaryAction: "NONE",
  };
}

/**
 * PURCHASE-DRAFT-RECOVERY B1 — regla de CONFIRMAR: toda línea con un estado bloqueante (producto
 * pendiente, conflicto de código, presentación faltante, costo/presentación sospechosa, bodega,
 * impuesto) impide confirmar. Guardar el borrador usa {@link getPurchaseLineSaveBlockingReasons}.
 */
export function getPurchaseLineBlockingReasons(
  lines: PurchaseLineFormValues[],
  options: PurchaseLineReadinessOptions,
) {
  return lines
    .map((line, index) => ({
      line,
      index,
      readiness: getPurchaseLineReadiness(line, options),
    }))
    .filter((entry) => entry.readiness.blocking);
}

/**
 * PURCHASE-DRAFT-RECOVERY B1 — regla de GUARDAR BORRADOR: solo bloquea lo que el backend rechazaría
 * igual al persistir (código IVA/ICE ausente o desconocido, o ICE del XML editado sin poder
 * recalcularse). Una línea XML pendiente o incompleta
 * se guarda para no perder trabajo; Confirmar sigue exigiendo la línea lista.
 */
export function getPurchaseLineSaveBlockingReasons(
  lines: PurchaseLineFormValues[],
  options: PurchaseLineReadinessOptions,
) {
  return lines
    .map((line, index) => ({ line, index }))
    .filter(
      (entry) =>
        hasInvalidTax(entry.line, options) || hasBlockedXmlIceEdit(entry.line, options),
    );
}

export function isPurchaseLineReady(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions,
) {
  return !getPurchaseLineReadiness(line, options).blocking;
}

export function getPurchaseLinePrimaryAction(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions,
) {
  return getPurchaseLineReadiness(line, options).primaryAction;
}
