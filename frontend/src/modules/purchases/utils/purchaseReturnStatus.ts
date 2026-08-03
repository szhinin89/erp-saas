import type { BadgeVariant } from "../../../components/PageShell";

/** Etiqueta en español por estado de `PurchaseReturn` — única fuente de verdad (mismo patrón que `salesReturnStatus.ts`). */
export const PURCHASE_RETURN_STATUS_LABEL: Record<string, string> = {
  Draft: "Borrador",
  Authorized: "Autorizada",
  Cancelled: "Cancelada",
};

/** Variante del componente `Badge` (`PageShell.tsx`) por estado de `PurchaseReturn`. */
export const PURCHASE_RETURN_STATUS_BADGE: Record<string, BadgeVariant> = {
  Draft: "orange",
  Authorized: "green",
  Cancelled: "red",
};

/** Etiqueta en español por estado fiscal (`FiscalStatus`) de `PurchaseReturn`. */
export const PURCHASE_RETURN_FISCAL_STATUS_LABEL: Record<string, string> = {
  NotApplicable: "No aplica",
  PendingSupplierCreditNote: "Pendiente de Nota de Crédito",
  SupplierCreditNoteRegistered: "Nota de Crédito registrada",
};
