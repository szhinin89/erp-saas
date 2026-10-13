import { describe, expect, it } from "vitest";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { receptionErpRecordView, summarizeReceptionItems } from "./receptionErpRecord";

type Row = Parameters<typeof summarizeReceptionItems>[0][number];

const invoice = (overrides: Partial<PurchaseReceptionItem> = {}): Row => ({
  sourceDocType: "INVOICE",
  purchaseState: "NONE",
  expenseExists: false,
  cancelledExpenseId: null,
  creditNoteExists: false,
  cancelledCreditNoteId: null,
  status: "PENDING",
  ...overrides,
});
const creditNote = (overrides: Partial<PurchaseReceptionItem> = {}): Row =>
  invoice({ sourceDocType: "CREDIT_NOTE", ...overrides });

describe("FASE 5 RECEPTION-UX — receptionErpRecordView (columna Registro ERP)", () => {
  it.each([
    ["compra DRAFT", invoice({ purchaseState: "DRAFT", status: "IMPORTED" }), "purchase", "Compra en borrador"],
    ["compra CONFIRMED", invoice({ purchaseState: "CONFIRMED", status: "IMPORTED" }), "purchase", "Compra confirmada"],
    ["compra CANCELLED", invoice({ purchaseState: "CANCELLED" }), null, "Compra anulada"],
    ["borrador DISCARDED", invoice({ purchaseState: "DISCARDED" }), null, "Borrador descartado"],
    ["gasto activo", invoice({ expenseExists: true }), "expense", "Gasto registrado"],
    ["gasto activo tras compra anulada", invoice({ expenseExists: true, purchaseState: "CANCELLED" }), "expense", "Gasto registrado"],
    ["gasto anulado", invoice({ cancelledExpenseId: "e-1" }), null, "Gasto anulado"],
    ["factura sin registro", invoice(), null, "Sin procesar"],
    ["NC activa", creditNote({ creditNoteExists: true }), "creditNote", "NC procesada"],
    ["NC anulada", creditNote({ cancelledCreditNoteId: "cn-1" }), null, "NC anulada"],
    ["NC sin registro", creditNote(), null, "Sin procesar"],
  ] as const)("%s → %s / %s", (_label, row, active, badge) => {
    const view = receptionErpRecordView(row);
    expect(view.active).toBe(active);
    expect(view.badge.fallback).toBe(badge);
  });
});

describe("FASE 5 RECEPTION-UX — summarizeReceptionItems (tarjetas)", () => {
  it("cuenta Compra/Gasto/NC activos, reprocesables como Por procesar y comprobantes sin proveedor", () => {
    const counts = summarizeReceptionItems([
      invoice({ purchaseState: "DRAFT", status: "IMPORTED" }),
      invoice({ purchaseState: "CONFIRMED", status: "IMPORTED" }),
      invoice({ expenseExists: true }),
      creditNote({ creditNoteExists: true }),
      invoice({ purchaseState: "CANCELLED" }),
      invoice({ purchaseState: "DISCARDED" }),
      creditNote({ cancelledCreditNoteId: "cn-1" }),
      invoice(),
      // Mismo proveedor inexistente en 2 comprobantes: cuenta comprobantes, no proveedores.
      invoice({ status: "NEW_SUPPLIER" }),
      creditNote({ status: "NEW_SUPPLIER" }),
    ]);

    expect(counts).toEqual({
      registered: 4,
      purchases: 2,
      expenses: 1,
      creditNotes: 1,
      toProcess: 4,
      pendingSupplier: 2,
    });
  });
});
