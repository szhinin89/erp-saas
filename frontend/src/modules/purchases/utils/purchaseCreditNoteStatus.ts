import type { BadgeVariant } from "../../../components/PageShell";

type TFunction = (key: string, fallback?: string) => string;

/** Clave i18n + fallback en español por estado de `PurchaseCreditNote` — misma fuente única de verdad que `purchaseReturnStatus.ts`. */
// PURCHASE-P2-P3-CLEANUP-CLOSE-01 — namespace "statusLabel" (no "status") a propósito: evita
// colisionar con "purchases.creditNote.status.cancelled" (mensaje de página, texto distinto:
// "Nota de crédito cancelada" vs el label corto de badge "Cancelada" aquí).
const PURCHASE_CREDIT_NOTE_STATUS_I18N: Record<string, [key: string, fallback: string]> = {
  Draft: ["purchases.creditNote.statusLabel.draft", "Borrador"],
  Authorized: ["purchases.creditNote.statusLabel.authorized", "Autorizada"],
  Cancelled: ["purchases.creditNote.statusLabel.cancelled", "Cancelada"],
};

/** Etiqueta traducida por estado de `PurchaseCreditNote`. */
export function getPurchaseCreditNoteStatusLabel(status: string, t?: TFunction): string {
  const entry = PURCHASE_CREDIT_NOTE_STATUS_I18N[status];
  if (!entry) return status;
  const [key, fallback] = entry;
  return t?.(key, fallback) ?? fallback;
}

/** Variante del componente `Badge` (`PageShell.tsx`) por estado de `PurchaseCreditNote`. */
export const PURCHASE_CREDIT_NOTE_STATUS_BADGE: Record<string, BadgeVariant> = {
  Draft: "orange",
  Authorized: "green",
  Cancelled: "red",
};
