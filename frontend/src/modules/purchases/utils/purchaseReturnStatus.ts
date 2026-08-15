import type { BadgeVariant } from "../../../components/PageShell";

type TFunction = (key: string, fallback?: string) => string;

/** Clave i18n + fallback en español por estado de `PurchaseReturn`. */
const PURCHASE_RETURN_STATUS_I18N: Record<string, [key: string, fallback: string]> = {
  Draft: ["purchases.return.status.draft", "Borrador"],
  Authorized: ["purchases.return.status.authorized", "Autorizada"],
  Cancelled: ["purchases.return.status.cancelled", "Cancelada"],
};

/** Etiqueta traducida por estado de `PurchaseReturn` — única fuente de verdad (mismo patrón que `salesReturnStatus.ts`). */
export function getPurchaseReturnStatusLabel(status: string, t?: TFunction): string {
  const entry = PURCHASE_RETURN_STATUS_I18N[status];
  if (!entry) return status;
  const [key, fallback] = entry;
  return t?.(key, fallback) ?? fallback;
}

/** Variante del componente `Badge` (`PageShell.tsx`) por estado de `PurchaseReturn`. */
export const PURCHASE_RETURN_STATUS_BADGE: Record<string, BadgeVariant> = {
  Draft: "orange",
  Authorized: "green",
  Cancelled: "red",
};

/** Clave i18n + fallback en español por estado fiscal (`FiscalStatus`) de `PurchaseReturn`. */
const PURCHASE_RETURN_FISCAL_STATUS_I18N: Record<string, [key: string, fallback: string]> = {
  NotApplicable: ["purchases.return.fiscalStatus.notApplicable", "No aplica"],
  PendingSupplierCreditNote: [
    "purchases.return.fiscalStatus.pendingSupplierCreditNote",
    "Pendiente de Nota de Crédito",
  ],
  SupplierCreditNoteRegistered: [
    "purchases.return.fiscalStatus.supplierCreditNoteRegistered",
    "Nota de Crédito registrada",
  ],
};

/** Etiqueta traducida por estado fiscal (`FiscalStatus`) de `PurchaseReturn`. */
export function getPurchaseReturnFiscalStatusLabel(status: string, t?: TFunction): string {
  const entry = PURCHASE_RETURN_FISCAL_STATUS_I18N[status];
  if (!entry) return status;
  const [key, fallback] = entry;
  return t?.(key, fallback) ?? fallback;
}
