import { describe, expect, it } from "vitest";
import { receptionPurchaseView } from "./receptionPurchaseState";

describe("FASE 5 PURCHASE-UX — receptionPurchaseView", () => {
  it.each([
    ["NONE", "Sin compra", "warning", "createPurchase", "Crear compra", null, false],
    ["DRAFT", "Compra en borrador", "warning", "continueDraft", "Continuar borrador", null, true],
    ["CONFIRMED", "Compra confirmada", "success", "viewPurchase", "Ver compra", null, true],
    ["CANCELLED", "Compra anulada", "error", "reprocessPurchase", "Procesar nuevamente", "Ver compra anulada", false],
    ["DISCARDED", "Borrador descartado", "neutral", "createPurchase", "Crear compra", "Ver borrador descartado", false],
  ] as const)("%s → %s / %s", (state, badge, variant, action, actionLabel, history, active) => {
    const view = receptionPurchaseView(state);

    expect(view.badge.fallback).toBe(badge);
    expect(view.badge.variant).toBe(variant);
    expect(view.action).toBe(action);
    expect(view.actionLabel.fallback).toBe(actionLabel);
    expect(view.history?.fallback ?? null).toBe(history);
    expect(view.hasActivePurchase).toBe(active);
  });
});
