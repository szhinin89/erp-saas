import { describe, it, expect } from "vitest";
import {
  basePaymentsForAdditionalMethod,
  computeSalesCollectionStatus,
  parseCashReceivedInput,
  quickTenderAmounts,
  syncCashOnlyAppliedAmount,
  type SalesCollectionInput,
} from "./salesCollectionStatus";

// POS-COLLECTION-SSOT-01 — única fuente de verdad del estado del cobro POS.

const CASH = "pm-cash";
const CARD = "pm-card";
const isCash = (id: string) => id === CASH;

function status(overrides: Partial<SalesCollectionInput> = {}) {
  return computeSalesCollectionStatus({
    total: 14.66,
    payments: [{ paymentMethodId: CASH, amount: 14.66 }],
    isCashMethod: isCash,
    cashReceived: null,
    tolerance: 0.01,
    moneyDecimals: 2,
    ...overrides,
  });
}

describe("computeSalesCollectionStatus — efectivo único (flujo B)", () => {
  it("vacío → estado neutro, nunca 'Insuficiente' ni 'Cobro completo', no permite emitir", () => {
    const s = status();
    expect(s.state).toBe("cashPending");
    expect(s.tone).toBe("neutral");
    expect(s.label).toBe("Esperando monto");
    expect(s.isComplete).toBe(false);
  });

  it("menor al debido → Falta por cobrar $X (warning), no permite emitir", () => {
    const s = status({ cashReceived: 10 });
    expect(s.state).toBe("cashShort");
    expect(s.tone).toBe("warning");
    expect(s.label).toBe("Falta por cobrar");
    expect(s.amount).toBe(4.66);
    expect(s.cashShortfall).toBe(4.66);
    expect(s.isComplete).toBe(false);
  });

  it("exacto → Pago exacto, permite emitir, vuelto 0", () => {
    const s = status({ cashReceived: 14.66 });
    expect(s.state).toBe("exact");
    expect(s.label).toBe("Pago exacto");
    expect(s.cashChange).toBe(0);
    expect(s.isComplete).toBe(true);
  });

  it("mayor → Vuelto $X y cobro completo, permite emitir", () => {
    const s = status({ cashReceived: 20 });
    expect(s.state).toBe("change");
    expect(s.label).toBe("Vuelto");
    expect(s.cashChange).toBe(5.34);
    expect(s.amount).toBe(5.34);
    expect(s.isComplete).toBe(true);
  });

  it("0 o texto vacío en el input equivale a 'sin ingresar' (neutral)", () => {
    expect(parseCashReceivedInput("")).toBeNull();
    expect(parseCashReceivedInput("0")).toBeNull();
    expect(parseCashReceivedInput("0.00")).toBeNull();
    expect(parseCashReceivedInput("abc")).toBeNull();
    expect(parseCashReceivedInput("20.5")).toBe(20.5);
  });

  it("estado único: un solo state/label en cada caso (no hay mensajes simultáneos)", () => {
    for (const received of [null, 5, 14.66, 30]) {
      const s = status({ cashReceived: received });
      expect(typeof s.state).toBe("string");
      expect(typeof s.label).toBe("string");
    }
  });
});

describe("computeSalesCollectionStatus — tolerancia de settlement (la de la empresa, nunca de UI)", () => {
  it("diferencia aplicada de 0.01 con tolerancia 0.01 → cuadra (resumen y appliedOk coinciden)", () => {
    const s = status({
      payments: [{ paymentMethodId: CARD, amount: 14.65 }],
    });
    expect(s.appliedOk).toBe(true);
    expect(s.state).toBe("complete");
    expect(s.isComplete).toBe(true);
  });

  it("diferencia de 0.02 con tolerancia 0.01 → Falta por cobrar (no hay estado 'verde' con Emitir bloqueado)", () => {
    const s = status({
      payments: [{ paymentMethodId: CARD, amount: 14.64 }],
    });
    expect(s.appliedOk).toBe(false);
    expect(s.state).toBe("pending");
    expect(s.amount).toBe(0.02);
    expect(s.isComplete).toBe(false);
  });

  it("la misma diferencia con tolerancia 0.02 sí cuadra — la tolerancia viene del llamador", () => {
    const s = status({
      payments: [{ paymentMethodId: CARD, amount: 14.64 }],
      tolerance: 0.02,
    });
    expect(s.appliedOk).toBe(true);
    expect(s.isComplete).toBe(true);
  });

  it("efectivo recibido 0.01 menos que lo aplicado (dentro de tolerancia) → Pago exacto", () => {
    const s = status({ cashReceived: 14.65 });
    expect(s.state).toBe("exact");
    expect(s.isComplete).toBe(true);
  });
});

describe("computeSalesCollectionStatus — multipago", () => {
  it("sin cobros → neutral 'Seleccione una forma de cobro'", () => {
    const s = status({ payments: [] });
    expect(s.state).toBe("empty");
    expect(s.tone).toBe("neutral");
  });

  it("sin total (sin productos) → noTotal", () => {
    expect(status({ total: 0, payments: [] }).state).toBe("noTotal");
  });

  it("aplicado < total → Falta por cobrar con la diferencia", () => {
    const s = status({
      payments: [
        { paymentMethodId: CASH, amount: 5 },
        { paymentMethodId: CARD, amount: 5 },
      ],
      cashReceived: 5,
    });
    expect(s.state).toBe("pending");
    expect(s.amount).toBe(4.66);
  });

  it("aplicado > total → El cobro excede el total (error)", () => {
    const s = status({
      payments: [
        { paymentMethodId: CASH, amount: 10 },
        { paymentMethodId: CARD, amount: 10 },
      ],
    });
    expect(s.state).toBe("exceeds");
    expect(s.tone).toBe("error");
    expect(s.amount).toBe(5.34);
  });

  it("aplicado = total con efectivo → exige efectivo recibido; vuelto sobre lo aplicado en efectivo", () => {
    const payments = [
      { paymentMethodId: CASH, amount: 4.66 },
      { paymentMethodId: CARD, amount: 10 },
    ];
    expect(status({ payments }).state).toBe("cashPending");
    const s = status({ payments, cashReceived: 5 });
    expect(s.state).toBe("change");
    expect(s.cashChange).toBe(0.34);
    expect(s.isCashOnly).toBe(false);
  });

  it("aplicado = total sin efectivo → Cobro completo", () => {
    const s = status({ payments: [{ paymentMethodId: CARD, amount: 14.66 }] });
    expect(s.state).toBe("complete");
    expect(s.isComplete).toBe(true);
  });
});

describe("syncCashOnlyAppliedAmount — cambio de total después de cobrar", () => {
  it("pago único en efectivo: lo aplicado sigue al nuevo total", () => {
    const payments = [{ _key: 1, paymentMethodId: CASH, amount: 10 }];
    expect(syncCashOnlyAppliedAmount(payments, isCash, 12.5)).toEqual([
      { _key: 1, paymentMethodId: CASH, amount: 12.5 },
    ]);
  });

  it("ya alineado → null (sin escrituras redundantes)", () => {
    const payments = [{ _key: 1, paymentMethodId: CASH, amount: 12.5 }];
    expect(syncCashOnlyAppliedAmount(payments, isCash, 12.5)).toBeNull();
  });

  it("multipago: NUNCA redistribuye — montos explícitos intactos", () => {
    const payments = [
      { _key: 1, paymentMethodId: CASH, amount: 5 },
      { _key: 2, paymentMethodId: CARD, amount: 5 },
    ];
    expect(syncCashOnlyAppliedAmount(payments, isCash, 20)).toBeNull();
  });

  it("pago único NO efectivo: se respeta (muestra Falta/Excede)", () => {
    const payments = [{ _key: 1, paymentMethodId: CARD, amount: 10 }];
    expect(syncCashOnlyAppliedAmount(payments, isCash, 20)).toBeNull();
  });

  it("efectivo recibido se conserva: el estado se recalcula contra el nuevo total", () => {
    const synced = syncCashOnlyAppliedAmount(
      [{ _key: 1, paymentMethodId: CASH, amount: 14.66 }],
      isCash,
      25,
    )!;
    const s = status({ total: 25, payments: synced, cashReceived: 20 });
    expect(s.state).toBe("cashShort");
    expect(s.amount).toBe(5);
  });
});

describe("basePaymentsForAdditionalMethod — de efectivo único a multipago", () => {
  const cashOnly = [{ _key: 1, paymentMethodId: CASH, amount: 14.66 }];

  it("fija lo aplicado en efectivo a lo recibido para que la nueva forma cubra solo el resto", () => {
    expect(basePaymentsForAdditionalMethod(cashOnly, CARD, isCash, 10, 14.66)).toEqual([
      { _key: 1, paymentMethodId: CASH, amount: 10 },
    ]);
  });

  it("si aún no se ingresó efectivo recibido, quita el efectivo (la nueva forma cubre todo)", () => {
    expect(basePaymentsForAdditionalMethod(cashOnly, CARD, isCash, null, 14.66)).toEqual([]);
  });

  it("recibido mayor al total: lo aplicado nunca supera el total", () => {
    expect(basePaymentsForAdditionalMethod(cashOnly, CARD, isCash, 50, 14.66)).toEqual([
      { _key: 1, paymentMethodId: CASH, amount: 14.66 },
    ]);
  });

  it("en multipago devuelve los pagos tal cual", () => {
    const multi = [
      { _key: 1, paymentMethodId: CASH, amount: 4 },
      { _key: 2, paymentMethodId: CARD, amount: 10.66 },
    ];
    expect(basePaymentsForAdditionalMethod(multi, "pm-x", isCash, 4, 14.66)).toEqual(multi);
  });
});

describe("quickTenderAmounts — montos rápidos de efectivo recibido", () => {
  it("14.66 → 15 · 20 · 50", () => {
    expect(quickTenderAmounts(14.66)).toEqual([15, 20, 50]);
  });
  it("monto redondo exacto se incluye (15 → 15 · 20 · 50)", () => {
    expect(quickTenderAmounts(15)).toEqual([15, 20, 50]);
  });
  it("4.66 → 5 · 10 · 20", () => {
    expect(quickTenderAmounts(4.66)).toEqual([5, 10, 20]);
  });
  it("sin efectivo aplicado → ninguno", () => {
    expect(quickTenderAmounts(0)).toEqual([]);
  });
});
