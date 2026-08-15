import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { buildSuspiciousPackagingCostWarning } from "./purchaseLinePresentation";

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

export interface PurchaseLineReadiness {
  status: PurchaseLineReadinessStatus;
  label: string;
  detail: string;
  tone: PurchaseLineReadinessTone;
  blocking: boolean;
  primaryAction: PurchaseLineReadinessAction;
  secondaryAction?: PurchaseLineReadinessAction;
}

export interface PurchaseLineReadinessOptions {
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

function hasKnownRate(code: string, rates?: Record<string, number>) {
  if (!rates || Object.keys(rates).length === 0) return true;
  return Object.prototype.hasOwnProperty.call(rates, code);
}

function hasInvalidTax(line: PurchaseLineFormValues, options: PurchaseLineReadinessOptions) {
  if (!line.vatCode?.trim()) return true;
  if (!hasKnownRate(line.vatCode, options.vatRates)) return true;
  if (line.iceCode?.trim() && !hasKnownRate(line.iceCode, options.iceRates)) return true;
  return false;
}

function requiresPresentation(line: PurchaseLineFormValues) {
  return hasXmlOrigin(line) && !!line.itemId && line.context?.tracksStock === true;
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

export function getPurchaseLineReadiness(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions = {},
): PurchaseLineReadiness {
  const t = options.t ?? fallbackT;

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
  const suspiciousPackagingCost = buildSuspiciousPackagingCostWarning(line, options.t);
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

  if (!hasWarehouse(line, options.globalWarehouseId)) {
    return {
      status: "MISSING_WAREHOUSE",
      ...message(t, "MISSING_WAREHOUSE"),
      tone: "warning",
      blocking: true,
      primaryAction: "SELECT_WAREHOUSE",
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

export function getPurchaseLineBlockingReasons(
  lines: PurchaseLineFormValues[],
  options: PurchaseLineReadinessOptions = {},
) {
  return lines
    .map((line, index) => ({
      line,
      index,
      readiness: getPurchaseLineReadiness(line, options),
    }))
    .filter((entry) => entry.readiness.blocking);
}

export function isPurchaseLineReady(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions = {},
) {
  return !getPurchaseLineReadiness(line, options).blocking;
}

export function getPurchaseLinePrimaryAction(
  line: PurchaseLineFormValues,
  options: PurchaseLineReadinessOptions = {},
) {
  return getPurchaseLineReadiness(line, options).primaryAction;
}
