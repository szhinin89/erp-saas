import { describe, expect, it } from "vitest";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import {
  buildReceptionRowKeys,
  countReceptionDocuments,
  hasValidReceptionDocumentId,
  normalizeReceptionItems,
  resolveReceptionDocumentBuckets,
  summarizeReceptionDocuments,
} from "./receptionNormalize";
import { summarizeReceptionItems } from "./receptionErpRecord";
import { buildReceptionRow as row } from "./reception.testFixtures";

describe("normalizeReceptionItems (B2)", () => {
  it("unifica filas idénticas del mismo documentId y conserva el orden del TXT", () => {
    const a = row({ documentId: "a" });
    const result = normalizeReceptionItems([a, row({ documentId: "b" }), { ...a }, { ...a }]);
    expect(result.items.map((r) => r.documentId)).toEqual(["a", "b"]);
    expect(result.duplicatesRemoved).toBe(2);
    expect(result.conflictingDocumentIds).toEqual([]);
  });

  it("la firma no depende del orden de las propiedades", () => {
    const a = row({ documentId: "a" });
    const reordered = Object.fromEntries(Object.entries(a).reverse()) as PurchaseReceptionItem;
    expect(normalizeReceptionItems([a, reordered]).items).toHaveLength(1);
  });

  it("mismo documentId con datos distintos: no fusiona ni descarta, lo informa para revisión", () => {
    const result = normalizeReceptionItems([
      row({ documentId: "a", total: 115 }),
      row({ documentId: "a", total: 999 }),
      row({ documentId: "a", total: 115 }),
    ]);
    expect(result.items.map((r) => r.total)).toEqual([115, 999]);
    expect(result.duplicatesRemoved).toBe(1);
    expect(result.conflictingDocumentIds).toEqual(["a"]);
  });

  it("no agrupa filas sin identificador válido (podrían ser documentos distintos)", () => {
    const missing = row({ documentId: "" });
    const blank = row({ documentId: "   " });
    const nullish = row({ documentId: null as unknown as string });
    const result = normalizeReceptionItems([missing, { ...missing }, blank, nullish]);
    expect(result.items).toHaveLength(4);
    expect(result.invalidIdCount).toBe(4);
    expect(result.duplicatesRemoved).toBe(0);
    expect(hasValidReceptionDocumentId(nullish)).toBe(false);
  });

  it("los KPI dejan de contar dos veces el mismo comprobante y suman el lote normalizado", () => {
    const registered = row({ documentId: "r", purchaseState: "CONFIRMED", status: "IMPORTED" });
    const raw = [registered, { ...registered }, row({ documentId: "p" })];
    expect(summarizeReceptionItems(raw).registered).toBe(2);
    const { items } = normalizeReceptionItems(raw);
    const counts = summarizeReceptionItems(items);
    expect(counts.registered).toBe(1);
    expect(counts.registered + counts.toProcess + counts.pendingSupplier).toBe(items.length);
  });
});

describe("buildReceptionRowKeys", () => {
  it("claves únicas y estables: documentId, id#n para conflictos, sin-id#pos sin identificador", () => {
    const items = [
      row({ documentId: "a" }),
      row({ documentId: "a", total: 1 }),
      row({ documentId: "" }),
      row({ documentId: "b" }),
    ];
    const keys = buildReceptionRowKeys(items);
    expect(items.map((r) => keys.get(r))).toEqual(["a", "a#2", "sin-id#2", "b"]);
    expect(new Set(keys.values()).size).toBe(items.length);
  });
});

describe("summarizeReceptionDocuments / countReceptionDocuments (A)", () => {
  it("cuenta comprobantes únicos; repetidos con datos distintos quedan En revisión, fuera de las tarjetas", () => {
    const rows = [
      row({ documentId: "a", total: 1 }),
      row({ documentId: "a", total: 2 }),
      row({ documentId: "b", purchaseState: "CONFIRMED", status: "IMPORTED" }),
      row({ documentId: "" }),
      row({ documentId: "" }),
    ];
    const summary = summarizeReceptionDocuments(rows);
    // "a" (datos distintos, aunque se clasificaría igual) va a revisión; las filas sin id cuentan solas.
    expect(summary).toMatchObject({ registered: 1, purchases: 1, toProcess: 2, inReview: 1, documents: 4 });
    expect(countReceptionDocuments(rows)).toBe(4);
    expect(summary.registered + summary.toProcess + summary.pendingSupplier + summary.inReview).toBe(
      summary.documents,
    );
  });

  it("repetidos idénticos no son contradicción: cuentan una vez en su tarjeta", () => {
    const a = row({ documentId: "a" });
    expect(summarizeReceptionDocuments([a, { ...a }])).toMatchObject({ toProcess: 1, inReview: 0, documents: 1 });
    expect(resolveReceptionDocumentBuckets([a, { ...a }])(a)).toBe("toProcess");
  });

  it("repetidos con clasificación distinta: ninguna tarjeta, quedan en revisión (sin elegir fila)", () => {
    const registered = row({ documentId: "c", expenseExists: true });
    const pending = row({ documentId: "c" });
    const bucketOf = resolveReceptionDocumentBuckets([registered, pending]);
    expect(bucketOf(registered)).toBe("review");
    expect(bucketOf(pending)).toBe("review");
    expect(summarizeReceptionDocuments([registered, pending])).toMatchObject({
      registered: 0,
      expenses: 0,
      toProcess: 0,
      inReview: 1,
      documents: 1,
    });
    // El orden de las filas no cambia el resultado.
    expect(summarizeReceptionDocuments([pending, registered]).inReview).toBe(1);
  });
});
