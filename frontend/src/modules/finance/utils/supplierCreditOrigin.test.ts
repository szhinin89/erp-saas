import { describe, expect, it } from "vitest";
import { formatSupplierCreditOrigin } from "./supplierCreditOrigin";

describe("formatSupplierCreditOrigin (02C)", () => {
  it("identifica un anticipo originado por un pago a proveedor", () => {
    expect(
      formatSupplierCreditOrigin({ sourceType: "SupplierPayment", sourceDocumentNumber: "00000012" }),
    ).toBe("Pago a proveedor 00000012");
  });

  it("mantiene el origen devolución de compra", () => {
    expect(
      formatSupplierCreditOrigin({ sourceType: "PurchaseReturn", sourceDocumentNumber: "00000003" }),
    ).toBe("Devolución de compra 00000003");
  });

  it("sin número resuelto muestra solo el tipo de origen", () => {
    expect(formatSupplierCreditOrigin({ sourceType: "SupplierPayment", sourceDocumentNumber: null })).toBe(
      "Pago a proveedor",
    );
  });
});
