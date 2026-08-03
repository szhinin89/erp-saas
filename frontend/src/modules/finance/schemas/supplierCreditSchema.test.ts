import { describe, expect, it } from "vitest";
import {
  buildApplySupplierCreditSchema,
  buildRegisterSupplierCreditRefundSchema,
  reverseSupplierCreditRefundSchema,
} from "./supplierCreditSchema";

describe("buildApplySupplierCreditSchema", () => {
  it("acepta un monto dentro del saldo disponible", () => {
    const schema = buildApplySupplierCreditSchema(100);
    expect(
      schema.safeParse({ targetPurchasePayableId: "p-1", amount: 40 }).success,
    ).toBe(true);
  });

  it("rechaza un monto que excede el saldo disponible", () => {
    const schema = buildApplySupplierCreditSchema(100);
    expect(
      schema.safeParse({ targetPurchasePayableId: "p-1", amount: 150 }).success,
    ).toBe(false);
  });

  it("rechaza sin cuenta por pagar destino", () => {
    const schema = buildApplySupplierCreditSchema(100);
    expect(schema.safeParse({ targetPurchasePayableId: "", amount: 40 }).success).toBe(
      false,
    );
  });
});

describe("buildRegisterSupplierCreditRefundSchema", () => {
  const base = {
    financialDestinationId: "fd-1",
    paymentMethodCode: "TRANSFER",
    amount: 50,
    effectiveDate: "2026-07-01",
    externalReference: "",
  };

  it("acepta sin referencia cuando el método no la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, false);
    expect(schema.safeParse(base).success).toBe(true);
  });

  it("rechaza sin referencia cuando el método la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, true);
    expect(schema.safeParse(base).success).toBe(false);
  });

  it("acepta con referencia cuando el método la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, true);
    expect(
      schema.safeParse({ ...base, externalReference: "TRX-001" }).success,
    ).toBe(true);
  });

  it("rechaza un monto que excede el saldo disponible", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(30, false);
    expect(schema.safeParse(base).success).toBe(false);
  });
});

describe("reverseSupplierCreditRefundSchema", () => {
  it("acepta un motivo y fecha válidos", () => {
    expect(
      reverseSupplierCreditRefundSchema.safeParse({
        reason: "Reembolso rechazado por el banco",
        effectiveDate: "2026-07-02",
      }).success,
    ).toBe(true);
  });

  it("rechaza sin motivo", () => {
    expect(
      reverseSupplierCreditRefundSchema.safeParse({
        reason: "",
        effectiveDate: "2026-07-02",
      }).success,
    ).toBe(false);
  });
});
