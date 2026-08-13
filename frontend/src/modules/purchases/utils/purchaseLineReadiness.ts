import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";

type TFunction = (
  key: string,
  fallbackOrParams?: string | Record<string, string | number>,
) => string;

export type PurchaseLineReadinessStatus =
  | "MISSING_ITEM"
  | "SUPPLIER_CODE_CONFLICT"
  | "MISSING_PRESENTATION"
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
          "Falta seleccionar o crear el ítem",
        ),
        detail: t(
          "purchases.lineReadiness.missingItemDetail",
          "Vincule esta línea XML/TXT con un ítem ERP antes de guardar.",
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
        label: t("purchases.lineReadiness.missingPresentation", "Falta presentación"),
        detail: t(
          "purchases.lineReadiness.missingPresentationDetail",
          "Seleccione una presentación y guárdela para este proveedor.",
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
    case "READY":
      return {
        label: t("purchases.lineReadiness.ready", "Ítem listo"),
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
