import { describe, expect, it } from "vitest";
import { purchaseReturnLineSchema } from "./purchaseReturnSchema";

describe("líneas de NC por devolución", () => {
  it("admite referencias de factura con cantidades positivas", () => {
    expect(purchaseReturnLineSchema.safeParse({ originalInvoiceDetailId: "line-1", quantity: 2 }).success).toBe(true);
  });
  it.each([0, -1])("rechaza cantidades no positivas: %s", (quantity) => {
    expect(purchaseReturnLineSchema.safeParse({ originalInvoiceDetailId: "line-1", quantity }).success).toBe(false);
  });
  it("exige referencia a la línea original", () => {
    expect(purchaseReturnLineSchema.safeParse({ originalInvoiceDetailId: "", quantity: 1 }).success).toBe(false);
  });
});
