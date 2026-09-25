import { describe, expect, it } from "vitest";
import { buildRegisterSupplierPaymentSchema } from "./registerSupplierPaymentSchema";

// ZH-DESIGN-SYSTEM-PRECISION-05 — las sumas del mensaje usan la escala money recibida (sin
// toFixed(2)); la tolerancia 0.005 de la regla no cambia.
describe("buildRegisterSupplierPaymentSchema — representación de las sumas (05)", () => {
  const base = {
    supplierId: "sup-1",
    paymentDate: "2026-09-25",
    receiptNumber: null,
    methodLines: [{ paymentMethodId: "pm-1", destination: "bank:fd-1", amount: 15 }],
    applicationLines: [{ accountsPayableInstallmentId: "inst-1", amountApplied: 20.5 }],
  };

  function sumMessage(decimals: number, value: unknown = base) {
    const r = buildRegisterSupplierPaymentSchema(decimals).safeParse(value);
    return r.success ? null : r.error.issues.find((i) => i.path[0] === "methodLines")?.message;
  }

  it("money 2 vs 3: mismo rechazo, solo cambia la representación", () => {
    expect(sumMessage(2)).toBe(
      "La suma de los medios de pago (15.00) debe ser igual a la suma de las cuotas aplicadas (20.50).",
    );
    expect(sumMessage(3)).toBe(
      "La suma de los medios de pago (15.000) debe ser igual a la suma de las cuotas aplicadas (20.500).",
    );
  });

  it("dentro de la tolerancia no hay error de sumas (regla intacta)", () => {
    const ok = { ...base, methodLines: [{ paymentMethodId: "pm-1", destination: "bank:fd-1", amount: 20.504 }] };
    expect(sumMessage(3, ok)).toBeNull();
  });
});
