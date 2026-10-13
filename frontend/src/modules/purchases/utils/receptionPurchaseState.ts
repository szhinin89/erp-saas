import type { ReceptionPurchaseState } from "../api/purchaseReceptionService";

/**
 * FASE 5 PURCHASE-UX — presentación del estado REAL de la compra relacionada con una recepción.
 * `purchaseState` (backend, PurchaseReceptionPurchaseStateResolver) es la única fuente de verdad
 * para badge, textos y acciones de Recepción; `purchaseExists` queda solo por compatibilidad.
 */
export type ReceptionPurchaseBadgeVariant = "warning" | "success" | "error" | "neutral";

/** Acción principal de compra para la fila. */
export type ReceptionPurchaseAction =
  | "createPurchase" // NONE / DISCARDED → "Crear compra"
  | "continueDraft" // DRAFT → "Continuar borrador"
  | "viewPurchase" // CONFIRMED → "Ver compra"
  | "reprocessPurchase"; // CANCELLED → "Reprocesar compra"

/** Acción secundaria de historial (compra Cancelled más reciente). */
export type ReceptionPurchaseHistoryAction = "viewCancelledPurchase" | "viewDiscardedDraft";

export interface ReceptionPurchaseView {
  state: ReceptionPurchaseState;
  badge: { key: string; fallback: string; variant: ReceptionPurchaseBadgeVariant };
  action: ReceptionPurchaseAction;
  actionLabel: { key: string; fallback: string };
  history: { action: ReceptionPurchaseHistoryAction; key: string; fallback: string } | null;
  /** Hay compra ACTIVA (borrador o confirmada): bloquea "Crear compra" y "Crear gasto". */
  hasActivePurchase: boolean;
}

export function receptionPurchaseView(state: ReceptionPurchaseState): ReceptionPurchaseView {
  switch (state) {
    case "DRAFT":
      return {
        state,
        badge: { key: "purchases.reception.purchaseState.draft", fallback: "Compra en borrador", variant: "warning" },
        action: "continueDraft",
        actionLabel: { key: "purchases.reception.actions.continueDraft", fallback: "Continuar borrador" },
        history: null,
        hasActivePurchase: true,
      };
    case "CONFIRMED":
      return {
        state,
        badge: { key: "purchases.reception.purchaseState.confirmed", fallback: "Compra confirmada", variant: "success" },
        action: "viewPurchase",
        actionLabel: { key: "purchases.reception.actions.viewPurchase", fallback: "Ver compra" },
        history: null,
        hasActivePurchase: true,
      };
    case "CANCELLED":
      return {
        state,
        badge: { key: "purchases.reception.purchaseState.cancelled", fallback: "Compra anulada", variant: "error" },
        action: "reprocessPurchase",
        actionLabel: { key: "purchases.reception.actions.reprocessPurchase", fallback: "Reprocesar compra" },
        history: {
          action: "viewCancelledPurchase",
          key: "purchases.reception.actions.viewCancelledPurchase",
          fallback: "Ver compra anulada",
        },
        hasActivePurchase: false,
      };
    case "DISCARDED":
      return {
        state,
        badge: { key: "purchases.reception.purchaseState.discarded", fallback: "Borrador descartado", variant: "neutral" },
        action: "createPurchase",
        actionLabel: { key: "purchases.reception.actions.createPurchase", fallback: "Crear compra" },
        history: {
          action: "viewDiscardedDraft",
          key: "purchases.reception.actions.viewDiscardedDraft",
          fallback: "Ver borrador descartado",
        },
        hasActivePurchase: false,
      };
    case "NONE":
    default:
      return {
        state: "NONE",
        badge: { key: "purchases.reception.purchaseState.none", fallback: "Sin compra", variant: "warning" },
        action: "createPurchase",
        actionLabel: { key: "purchases.reception.actions.createPurchase", fallback: "Crear compra" },
        history: null,
        hasActivePurchase: false,
      };
  }
}
