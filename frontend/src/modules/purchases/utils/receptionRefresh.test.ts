import { describe, expect, it } from "vitest";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { chunkIds, mergeRefreshedReceptionItems } from "./receptionRefresh";

const item = (documentId: string, overrides: Partial<PurchaseReceptionItem> = {}) =>
  ({
    documentId,
    supplierRuc: "0990011223001",
    supplierName: "DINADEC S.A.",
    supplierTradeName: null,
    purchaseState: "NONE",
    purchaseId: null,
    cancelledPurchaseId: null,
    documentStatus: "VERIFIED",
    ...overrides,
  }) as PurchaseReceptionItem;

describe("FASE 5 PURCHASE-UX — refresco de estados de Recepción", () => {
  it("parte los ids en lotes de 200", () => {
    const ids = Array.from({ length: 450 }, (_, i) => `doc-${i}`);
    expect(chunkIds(ids).map((c) => c.length)).toEqual([200, 200, 50]);
    expect(chunkIds([])).toEqual([]);
  });

  it("fusiona por documentId: actualiza estado e ids, conserva orden, cantidad y nombre comercial", () => {
    const current = [
      item("a", { supplierTradeName: "DINADEC", purchaseState: "NONE" }),
      item("b"),
      item("c", { purchaseState: "DRAFT", purchaseId: "draft-1" }),
    ];
    const refreshed = [
      item("c", { purchaseState: "DISCARDED", cancelledPurchaseId: "draft-1", documentStatus: "VERIFIED" }),
      item("a", { purchaseState: "CONFIRMED", purchaseId: "p-1", documentStatus: "PROCESSED" }),
    ];

    const merged = mergeRefreshedReceptionItems(current, refreshed);

    expect(merged.map((i) => i.documentId)).toEqual(["a", "b", "c"]);
    expect(merged[0]).toMatchObject({
      purchaseState: "CONFIRMED",
      purchaseId: "p-1",
      documentStatus: "PROCESSED",
      supplierTradeName: "DINADEC", // solo llega con la descarga del XML: se conserva
    });
    expect(merged[1]).toBe(current[1]); // no devuelta → intacta
    expect(merged[2]).toMatchObject({
      purchaseState: "DISCARDED",
      purchaseId: null,
      cancelledPurchaseId: "draft-1",
    });
  });
});
