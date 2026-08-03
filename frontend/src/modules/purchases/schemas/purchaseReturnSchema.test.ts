import { describe, expect, it } from "vitest";
import {
  purchaseReturnDraftSchema,
  cancelPurchaseReturnSchema,
  linkSupplierCreditNoteSchema,
} from "./purchaseReturnSchema";

describe("purchaseReturnDraftSchema", () => {
  const base = {
    reason: "Producto en mal estado",
    lines: [{ originalInvoiceDetailId: "line-1", quantity: 2 }],
  };

  it("acepta un borrador válido con al menos una línea", () => {
    expect(purchaseReturnDraftSchema.safeParse(base).success).toBe(true);
  });

  it("rechaza motivo vacío", () => {
    const result = purchaseReturnDraftSchema.safeParse({ ...base, reason: "" });
    expect(result.success).toBe(false);
  });

  it("rechaza motivo mayor a 500 caracteres", () => {
    const result = purchaseReturnDraftSchema.safeParse({
      ...base,
      reason: "a".repeat(501),
    });
    expect(result.success).toBe(false);
  });

  it("rechaza sin líneas", () => {
    const result = purchaseReturnDraftSchema.safeParse({ ...base, lines: [] });
    expect(result.success).toBe(false);
  });

  it("rechaza cantidad <= 0", () => {
    const result = purchaseReturnDraftSchema.safeParse({
      ...base,
      lines: [{ originalInvoiceDetailId: "line-1", quantity: 0 }],
    });
    expect(result.success).toBe(false);
  });
});

describe("cancelPurchaseReturnSchema", () => {
  it("acepta un motivo válido", () => {
    expect(
      cancelPurchaseReturnSchema.safeParse({ reason: "Ya no aplica" }).success,
    ).toBe(true);
  });

  it("rechaza motivo vacío", () => {
    expect(cancelPurchaseReturnSchema.safeParse({ reason: "" }).success).toBe(
      false,
    );
  });

  it("rechaza motivo mayor a 500 caracteres", () => {
    expect(
      cancelPurchaseReturnSchema.safeParse({ reason: "a".repeat(501) }).success,
    ).toBe(false);
  });
});

describe("linkSupplierCreditNoteSchema", () => {
  const base = {
    accessKey: "AK-1",
    supplierRuc: "1791352688001",
    supplierName: "Proveedor Test",
    invoiceNumber: "001-001-000000099",
    issueDate: "2026-06-15",
    subtotal: 100,
    vatAmount: 12,
    totalAmount: 112,
    currencyCode: "USD",
  };

  it("acepta un vínculo de NC válido", () => {
    expect(linkSupplierCreditNoteSchema.safeParse(base).success).toBe(true);
  });

  it("rechaza sin clave de acceso", () => {
    expect(
      linkSupplierCreditNoteSchema.safeParse({ ...base, accessKey: "" }).success,
    ).toBe(false);
  });

  it("rechaza total <= 0", () => {
    expect(
      linkSupplierCreditNoteSchema.safeParse({ ...base, totalAmount: 0 }).success,
    ).toBe(false);
  });

  it("rechaza subtotal negativo", () => {
    expect(
      linkSupplierCreditNoteSchema.safeParse({ ...base, subtotal: -1 }).success,
    ).toBe(false);
  });
});
