import { describe, expect, it } from "vitest";
import {
  salesReturnDraftSchema,
  buildAuthorizeSalesReturnSchema,
} from "./salesReturnSchema";

describe("salesReturnDraftSchema", () => {
  const base = {
    salesInvoiceId: "inv-1",
    reason: "Producto en mal estado",
    lines: [{ invoiceDetailId: "line-1", quantity: 2 }],
  };

  it("acepta un borrador válido con al menos una línea", () => {
    expect(salesReturnDraftSchema.safeParse(base).success).toBe(true);
  });

  it("rechaza sin factura origen", () => {
    const result = salesReturnDraftSchema.safeParse({ ...base, salesInvoiceId: "" });
    expect(result.success).toBe(false);
  });

  it("rechaza motivo vacío", () => {
    const result = salesReturnDraftSchema.safeParse({ ...base, reason: "" });
    expect(result.success).toBe(false);
  });

  it("rechaza motivo mayor a 500 caracteres", () => {
    const result = salesReturnDraftSchema.safeParse({
      ...base,
      reason: "a".repeat(501),
    });
    expect(result.success).toBe(false);
  });

  it("rechaza sin líneas", () => {
    const result = salesReturnDraftSchema.safeParse({ ...base, lines: [] });
    expect(result.success).toBe(false);
  });

  it("rechaza cantidad <= 0", () => {
    const result = salesReturnDraftSchema.safeParse({
      ...base,
      lines: [{ invoiceDetailId: "line-1", quantity: 0 }],
    });
    expect(result.success).toBe(false);
  });
});

describe("buildAuthorizeSalesReturnSchema", () => {
  it("acepta una única asignación en efectivo que coincide con el total", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({
      refundAllocations: [{ method: "Cash", amount: 23 }],
    });
    expect(result.success).toBe(true);
  });

  it("acepta una asignación mixta cuya suma coincide con el total", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({
      refundAllocations: [
        { method: "Cash", amount: 15 },
        { method: "ReceivableCredit", amount: 8 },
      ],
    });
    expect(result.success).toBe(true);
  });

  it("rechaza cuando la suma de asignaciones no coincide con el total", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({
      refundAllocations: [{ method: "Cash", amount: 10 }],
    });
    expect(result.success).toBe(false);
  });

  it("rechaza sin ninguna asignación", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({ refundAllocations: [] });
    expect(result.success).toBe(false);
  });

  it("rechaza un monto de asignación <= 0", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({
      refundAllocations: [{ method: "Cash", amount: 0 }],
    });
    expect(result.success).toBe(false);
  });

  it("rechaza una forma de reembolso inválida", () => {
    const schema = buildAuthorizeSalesReturnSchema(23);
    const result = schema.safeParse({
      refundAllocations: [{ method: "Bitcoin", amount: 23 }],
    });
    expect(result.success).toBe(false);
  });
});
