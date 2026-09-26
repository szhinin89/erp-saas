import { describe, expect, it } from "vitest";
import { supplierCreditSourceLabel, supplierCreditSourceRoute } from "./supplierCreditOrigin";

describe("origen de un saldo a favor (02D-E)", () => {
  it("etiqueta de negocio: anticipo / pago mayor y devolución de compra", () => {
    expect(supplierCreditSourceLabel("SupplierPayment")).toBe("Anticipo / pago mayor");
    expect(supplierCreditSourceLabel("PurchaseReturn")).toBe("Devolución de compra");
  });

  it("navega al pago a proveedor o a la devolución de compra de origen", () => {
    expect(supplierCreditSourceRoute({ sourceType: "SupplierPayment", sourceDocumentId: "pay-1" })).toBe(
      "/supplier-payments/pay-1",
    );
    expect(supplierCreditSourceRoute({ sourceType: "PurchaseReturn", sourceDocumentId: "ret-1" })).toBe(
      "/purchases/returns/ret-1",
    );
  });
});
