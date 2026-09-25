import { describe, expect, it } from "vitest";
import {
  buildApplySupplierCreditSchema,
  buildRegisterSupplierCreditRefundSchema,
  reverseSupplierCreditRefundSchema,
} from "./supplierCreditSchema";

describe("buildApplySupplierCreditSchema", () => {
  it("acepta un monto dentro del saldo disponible", () => {
    const schema = buildApplySupplierCreditSchema(100, 2);
    expect(
      schema.safeParse({ targetPurchasePayableId: "p-1", amount: 40 }).success,
    ).toBe(true);
  });

  it("rechaza un monto que excede el saldo disponible", () => {
    const schema = buildApplySupplierCreditSchema(100, 2);
    expect(
      schema.safeParse({ targetPurchasePayableId: "p-1", amount: 150 }).success,
    ).toBe(false);
  });

  it("rechaza sin cuenta por pagar destino", () => {
    const schema = buildApplySupplierCreditSchema(100, 2);
    expect(schema.safeParse({ targetPurchasePayableId: "", amount: 40 }).success).toBe(
      false,
    );
  });
});

describe("buildRegisterSupplierCreditRefundSchema", () => {
  const base = {
    destination: "bank:fd-1",
    paymentMethodCode: "TRANSFER",
    amount: 50,
    effectiveDate: "2026-07-01",
    externalReference: "",
  };

  it("acepta sin referencia cuando el método no la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, false, 2);
    expect(schema.safeParse(base).success).toBe(true);
  });

  it("rechaza sin referencia cuando el método la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, true, 2);
    expect(schema.safeParse(base).success).toBe(false);
  });

  it("acepta con referencia cuando el método la requiere", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(100, true, 2);
    expect(
      schema.safeParse({ ...base, externalReference: "TRX-001" }).success,
    ).toBe(true);
  });

  it("rechaza un monto que excede el saldo disponible", () => {
    const schema = buildRegisterSupplierCreditRefundSchema(30, false, 2);
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

// ZH-DESIGN-SYSTEM-PRECISION-04G — el saldo del mensaje usa la escala money recibida (sin toFixed(2));
// la regla (monto ≤ saldo) es la misma con cualquier escala.
describe("supplierCreditSchema — representación del saldo en el mensaje (04G)", () => {
  function maxMessage(schema: { safeParse: (v: unknown) => { success: boolean; error?: { issues: { message: string }[] } } }, value: object) {
    const r = schema.safeParse(value);
    expect(r.success).toBe(false);
    return r.error!.issues.map((i) => i.message);
  }

  it("aplicar crédito: moneyDecimals 2 vs 3 → mismo rechazo, solo cambia la representación", () => {
    const value = { targetPurchasePayableId: "p-1", amount: 150 };
    expect(maxMessage(buildApplySupplierCreditSchema(100.5, 2), value)).toContain(
      "El monto no puede exceder el saldo disponible (100.50).",
    );
    expect(maxMessage(buildApplySupplierCreditSchema(100.5, 3), value)).toContain(
      "El monto no puede exceder el saldo disponible (100.500).",
    );
    expect(buildApplySupplierCreditSchema(100.5, 3).safeParse({ ...value, amount: 100.5 }).success).toBe(true);
  });

  it("reembolso: mismo contrato", () => {
    const value = { destination: "bank:fd-1", paymentMethodCode: "TRANSFER", amount: 80, effectiveDate: "2026-07-01", externalReference: "" };
    expect(maxMessage(buildRegisterSupplierCreditRefundSchema(30, false, 4), value)).toContain(
      "El monto no puede exceder el saldo disponible (30.0000).",
    );
  });
});
