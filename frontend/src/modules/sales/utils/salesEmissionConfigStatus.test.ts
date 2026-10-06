import { describe, it, expect } from "vitest";
import {
  computeSalesConfigStatus,
  type SalesConfigStatusInput,
} from "./salesEmissionConfigStatus";

// POS-CONFIG-STATUS-SEVERITY-01: computeSalesConfigStatus es la única fuente de verdad del estado
// de "Configuración de venta". Solo `error` bloquea (y se integra a canEmit en useSalesPage);
// cliente/caja/cobro/stock tienen su propio bloqueante en el hook y ya no viven aquí.

const MAPPED = { id: "pm-cash", isCreditAllowed: false, sriPaymentMethodCode: "01" };
const UNMAPPED = { id: "pm-card", isCreditAllowed: false, sriPaymentMethodCode: null };
const CREDIT = { id: "pm-credit", isCreditAllowed: true, sriPaymentMethodCode: null };

function input(overrides: Partial<SalesConfigStatusInput> = {}): SalesConfigStatusInput {
  return {
    hasCashSession: true,
    emissionType: "Electronic",
    docTypeCode: "01",
    lines: [],
    defaultSriPaymentCode: "01",
    payments: [],
    paymentMethods: [MAPPED, UNMAPPED, CREDIT],
    ...overrides,
  };
}

describe("computeSalesConfigStatus", () => {
  it("ready cuando todo está configurado", () => {
    const s = computeSalesConfigStatus(input());
    expect(s.level).toBe("ready");
    expect(s.missing).toEqual([]);
  });

  it("ya no reporta Cliente ni Caja abierta (tienen su propio bloqueante en el hook)", () => {
    const s = computeSalesConfigStatus(input({ hasCashSession: false, emissionType: null }));
    expect(s.missing).toEqual([]);
  });

  it("error bloqueante si hay caja abierta pero el tipo de emisión no se resolvió", () => {
    const s = computeSalesConfigStatus(input({ emissionType: null }));
    expect(s.level).toBe("incomplete");
    expect(s.missing[0]).toMatch(/Tipo de emisión/);
  });

  it("error bloqueante si una línea de stock no tiene bodega (mismo criterio que el schema)", () => {
    const s = computeSalesConfigStatus(
      input({ lines: [{ _participatesInInventory: true, warehouseId: null }] }),
    );
    expect(s.missing).toContain("Bodega");
  });

  it("línea de stock CON bodega no bloquea", () => {
    const s = computeSalesConfigStatus(
      input({ lines: [{ _participatesInInventory: true, warehouseId: "wh-1" }] }),
    );
    expect(s.missing).toEqual([]);
  });

  it("tipo de documento vacío es solo informativo (el backend usa Factura 01)", () => {
    const s = computeSalesConfigStatus(input({ docTypeCode: "" }));
    expect(s.level).toBe("ready");
    expect(s.issues.some((i) => i.severity === "info")).toBe(true);
  });

  describe("Forma de pago SRI — solo electrónica", () => {
    it("física: nunca reporta nada de Forma de pago SRI, aunque falte el default", () => {
      const s = computeSalesConfigStatus(
        input({
          emissionType: "Physical",
          defaultSriPaymentCode: "",
          payments: [{ paymentMethodId: UNMAPPED.id, amount: 10 }],
        }),
      );
      expect(s.issues).toEqual([]);
      expect(s.level).toBe("ready");
    });

    it("electrónica sin default + cobro sin mapeo propio → error bloqueante", () => {
      const s = computeSalesConfigStatus(
        input({
          defaultSriPaymentCode: "",
          payments: [{ paymentMethodId: UNMAPPED.id, amount: 10 }],
        }),
      );
      expect(s.missing).toContain("Forma de pago SRI por defecto");
    });

    it("electrónica sin default + único método mapeado no-crédito → no bloquea (el backend sincroniza la cabecera)", () => {
      const s = computeSalesConfigStatus(
        input({
          defaultSriPaymentCode: "",
          payments: [{ paymentMethodId: MAPPED.id, amount: 10 }],
        }),
      );
      expect(s.missing).toEqual([]);
    });

    it("electrónica sin default + multipago (aunque todos mapeados) → bloquea: la cabecera no se sincroniza", () => {
      const twoMapped = { id: "pm-transfer", isCreditAllowed: false, sriPaymentMethodCode: "20" };
      const s = computeSalesConfigStatus(
        input({
          defaultSriPaymentCode: "",
          paymentMethods: [MAPPED, twoMapped],
          payments: [
            { paymentMethodId: MAPPED.id, amount: 5 },
            { paymentMethodId: twoMapped.id, amount: 5 },
          ],
        }),
      );
      expect(s.missing).toContain("Forma de pago SRI por defecto");
    });

    it("electrónica sin default y sin cobros todavía → warning (no bloquea)", () => {
      const s = computeSalesConfigStatus(input({ defaultSriPaymentCode: "" }));
      expect(s.level).toBe("review");
      expect(s.missing).toEqual([]);
    });

    it("electrónica con default + cobro sin mapeo propio → solo info (usa el default)", () => {
      const s = computeSalesConfigStatus(
        input({ payments: [{ paymentMethodId: UNMAPPED.id, amount: 10 }] }),
      );
      expect(s.level).toBe("ready");
      expect(s.issues.map((i) => i.severity)).toEqual(["info"]);
    });
  });
});
