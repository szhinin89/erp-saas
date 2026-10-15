import { describe, expect, it } from "vitest";
import {
  canDiscardDraft,
  purchaseStatusBadge,
  resolvePurchaseDocumentState,
} from "./purchaseDocumentState";

const base = {
  status: "Draft",
  lineCount: 3,
  lineBlockerCount: 0,
  pendingReceptionItemCount: 0,
  scheduleBlocksConfirm: false,
  hasUnsavedFormChanges: false,
};

describe("FASE 5 PURCHASE-UX — resolvePurchaseDocumentState", () => {
  it("compra nueva sin guardar", () => {
    expect(resolvePurchaseDocumentState({ ...base, status: null }).kind).toBe("new");
  });

  it("borrador sin bloqueos → listo para confirmar", () => {
    expect(resolvePurchaseDocumentState(base)).toEqual({ kind: "draftReady", reasons: [] });
  });

  it("borrador con líneas por resolver, cuotas descuadradas y cambios sin guardar → incompleto con motivos", () => {
    const state = resolvePurchaseDocumentState({
      ...base,
      lineBlockerCount: 2,
      pendingReceptionItemCount: 1,
      scheduleBlocksConfirm: true,
      hasUnsavedFormChanges: true,
    });

    expect(state.kind).toBe("draftIncomplete");
    expect(state.reasons).toEqual([
      { code: "linesToResolve", count: 2 },
      { code: "scheduleMismatch" },
      { code: "unsavedChanges" },
    ]);
  });

  it("productos XML sin vincular cuentan aunque no haya bloqueo de readiness", () => {
    expect(
      resolvePurchaseDocumentState({ ...base, pendingReceptionItemCount: 1 }).reasons,
    ).toEqual([{ code: "pendingProducts", count: 1 }]);
  });

  it("borrador sin líneas → incompleto", () => {
    expect(resolvePurchaseDocumentState({ ...base, lineCount: 0 }).reasons).toEqual([
      { code: "noLines" },
    ]);
  });

  it("confirmada y anulada no exponen motivos aunque haya flags", () => {
    expect(
      resolvePurchaseDocumentState({ ...base, status: "Confirmed", hasUnsavedFormChanges: true }),
    ).toEqual({ kind: "confirmed", reasons: [] });
    expect(resolvePurchaseDocumentState({ ...base, status: "Cancelled" }).kind).toBe("cancelled");
  });
});

describe("FASE 5 PURCHASE-UX — Descartar borrador (UI)", () => {
  it("el botón 'Descartar borrador' solo aparece en un borrador ya guardado", () => {
    expect(canDiscardDraft({ status: "Draft" })).toBe(true);
    expect(canDiscardDraft(null)).toBe(false); // compra nueva sin guardar: solo "Cancelar"
    expect(canDiscardDraft({ status: "Confirmed" })).toBe(false); // se anula
    expect(canDiscardDraft({ status: "Cancelled" })).toBe(false); // solo lectura
  });

  it("etiquetas: Borrador / Confirmada / Anulada / Descartado", () => {
    expect(purchaseStatusBadge("Draft")).toMatchObject({ fallback: "Borrador", variant: "warning" });
    expect(purchaseStatusBadge("Confirmed")).toMatchObject({ fallback: "Confirmada", variant: "success" });
    expect(purchaseStatusBadge("Cancelled")).toMatchObject({ fallback: "Anulada", variant: "error" });
    expect(purchaseStatusBadge("Discarded")).toMatchObject({
      key: "purchases.status.discarded",
      fallback: "Descartado",
      variant: "neutral",
    });
  });

  it("estado del documento: descartado ≠ anulado", () => {
    expect(resolvePurchaseDocumentState({ ...base, status: "Discarded" }).kind)
      .toBe("discarded");
    expect(resolvePurchaseDocumentState({ ...base, status: "Cancelled" }).kind).toBe("cancelled");
  });
});
