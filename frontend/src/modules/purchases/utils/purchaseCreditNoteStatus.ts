import type { BadgeVariant } from "../../../components/PageShell";

/** Etiqueta en español por estado de `PurchaseCreditNote` — misma fuente única de verdad que `purchaseReturnStatus.ts`. */
export const PURCHASE_CREDIT_NOTE_STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Authorized: "Autorizada",
  Cancelled: "Cancelada",
};

/** Variante del componente `Badge` (`PageShell.tsx`) por estado de `PurchaseCreditNote`. */
export const PURCHASE_CREDIT_NOTE_STATUS_BADGE: Record<string, BadgeVariant> = {
  Draft: "orange",
  Authorized: "green",
  Cancelled: "red",
};
