import type { AdjustmentMovementType, AdjustmentStatus } from "../types";

type TFunction = (key: string, fallback?: string) => string;

/**
 * INVENTORY-ADJUSTMENTS-03 — mapeo estado → Badge. Mismo patrón de tres estados que
 * `statusBadge()` en `StockTransferPage` (Draft naranja / terminal verde / anulado gris), con los
 * estados propios de Ajustes (Draft/Executed/Cancelled). Vive en un módulo aparte porque lo
 * consumen las dos pantallas de Ajustes (lista y editor) — no se duplica el mapeo en cada una.
 */
export function adjustmentStatusBadge(
  status: AdjustmentStatus | string,
  t: TFunction,
): { label: string; variant: "orange" | "green" | "gray" } {
  if (status === "Executed")
    return {
      label: t("inventory.adjustments.status.executed", "Ejecutado"),
      variant: "green",
    };
  if (status === "Cancelled")
    return {
      label: t("inventory.adjustments.status.cancelled", "Anulado"),
      variant: "gray",
    };
  return {
    label: t("inventory.adjustments.status.draft", "Borrador"),
    variant: "orange",
  };
}

/** Ingreso suma stock (azul), Egreso lo resta (rojo) — misma paleta semántica del DS. */
export function movementTypeBadge(
  movementType: AdjustmentMovementType | string,
  t: TFunction,
): { label: string; variant: "blue" | "red" } {
  if (movementType === "Egreso")
    return {
      label: t("inventory.adjustments.movementType.egreso", "Egreso"),
      variant: "red",
    };
  return {
    label: t("inventory.adjustments.movementType.ingreso", "Ingreso"),
    variant: "blue",
  };
}
