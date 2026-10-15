/**
 * FASE 5 PURCHASE-UX — estado visible de la COMPRA (nivel documento), separado del estado de cada
 * línea/producto: "Listo para confirmar" habla de la compra, nunca de si un producto está listo
 * para vender. Solo interpreta flags que el hook ya calcula — no agrega reglas nuevas: las mismas
 * condiciones que deshabilitan "Confirmar compra" son las que vuelven incompleto el borrador.
 */
export type PurchaseDocumentStateKind =
  | "new"
  | "draftIncomplete"
  | "draftReady"
  | "confirmed"
  | "cancelled"
  | "discarded";

export type PurchaseDocumentStateReason =
  | { code: "noLines" }
  | { code: "linesToResolve"; count: number }
  | { code: "pendingProducts"; count: number }
  | { code: "scheduleMismatch" }
  | { code: "unsavedChanges" };

export interface PurchaseDocumentState {
  kind: PurchaseDocumentStateKind;
  /** Motivos por los que un borrador todavía no puede confirmarse (vacío en los demás estados). */
  reasons: PurchaseDocumentStateReason[];
}

export function resolvePurchaseDocumentState(input: {
  /** Estado persistido de la compra; null/undefined = compra nueva aún no guardada. */
  status: string | null | undefined;
  lineCount: number;
  lineBlockerCount: number;
  pendingReceptionItemCount: number;
  scheduleBlocksConfirm: boolean;
  hasUnsavedFormChanges: boolean;
}): PurchaseDocumentState {
  if (input.status === "Confirmed") return { kind: "confirmed", reasons: [] };
  if (input.status === "Cancelled")
    return { kind: "cancelled", reasons: [] };
  if (input.status === "Discarded") return { kind: "discarded", reasons: [] };
  if (!input.status) return { kind: "new", reasons: [] };

  const reasons: PurchaseDocumentStateReason[] = [];
  if (input.lineCount === 0) reasons.push({ code: "noLines" });
  if (input.lineBlockerCount > 0)
    reasons.push({ code: "linesToResolve", count: input.lineBlockerCount });
  else if (input.pendingReceptionItemCount > 0)
    reasons.push({ code: "pendingProducts", count: input.pendingReceptionItemCount });
  if (input.scheduleBlocksConfirm) reasons.push({ code: "scheduleMismatch" });
  if (input.hasUnsavedFormChanges) reasons.push({ code: "unsavedChanges" });

  return { kind: reasons.length > 0 ? "draftIncomplete" : "draftReady", reasons };
}

/**
 * FASE 5 PURCHASE-UX — "Descartar borrador" solo para un borrador YA guardado (una compra nueva sin
 * guardar se abandona con "Cancelar"; una confirmada se anula; anulada/descartada es solo lectura).
 */
export function canDiscardDraft(purchase: { status: string } | null | undefined): boolean {
  return purchase?.status === "Draft";
}

export type PurchaseStatusBadgeVariant = "warning" | "success" | "error" | "neutral";

/**
 * FASE 5 PURCHASE-UX — etiqueta/variante del estado de una compra en listado y detalle:
 * Draft → Borrador; Confirmed → Confirmada; Cancelled → Anulada; Discarded → Descartado.
 */
export function purchaseStatusBadge(
  status: string,
): { key: string; fallback: string; variant: PurchaseStatusBadgeVariant } {
  if (status === "Discarded")
    return { key: "purchases.status.discarded", fallback: "Descartado", variant: "neutral" };
  if (status === "Draft") return { key: "purchases.status.draft", fallback: "Borrador", variant: "warning" };
  if (status === "Confirmed")
    return { key: "purchases.status.confirmed", fallback: "Confirmada", variant: "success" };
  return { key: "purchases.status.cancelled", fallback: "Anulada", variant: "error" };
}
