import type { BadgeVariant } from "../../../components/PageShell";
import type { useI18n } from "../../../i18n/i18n";
import type { LineMarginState } from "./purchaseLinePresentation";
import type { PurchaseLineReadinessAction } from "./purchaseLineReadiness";

/** Margin indicator: tone by state; without a sale price there is no margin to show. */
export const MARGIN_BADGE_VARIANT = {
  positive: "success",
  zero: "warning",
  negative: "error",
  noSalePrice: "neutral",
  unavailable: "neutral",
} as const satisfies Record<LineMarginState, BadgeVariant>;

export function marginBadgeLabel(
  profitability: { marginState: LineMarginState; marginPct: string },
  t: ReturnType<typeof useI18n>["t"],
) {
  if (profitability.marginState === "noSalePrice") return t("purchases.lines.marginNoSalePrice", "Sin PVP");
  if (profitability.marginState === "unavailable") return profitability.marginPct;
  return `${profitability.marginPct}%`;
}

type TFunction = ReturnType<typeof useI18n>["t"];

export function readinessActionLabel(
  action: PurchaseLineReadinessAction | undefined,
  t: TFunction,
) {
  switch (action) {
    case "SELECT_ITEM":
      return t("purchases.lineReadiness.action.selectItem", "Seleccionar producto");
    case "CREATE_ITEM":
      return t("purchases.lineReadiness.action.createItem", "Crear producto");
    case "SELECT_PRESENTATION":
      return t(
        "purchases.lineReadiness.action.selectPresentation",
        "Seleccionar presentación",
      );
    case "SAVE_PRESENTATION":
      return t(
        "purchases.lineReadiness.action.savePresentation",
        "Guardar presentación",
      );
    case "SELECT_WAREHOUSE":
      return t("purchases.lineReadiness.action.selectWarehouse", "Asignar bodega");
    case "REVIEW_TAX":
      return t("purchases.lineReadiness.action.reviewTax", "Revisar impuesto");
    case "NONE":
    case undefined:
      return "";
  }
}
