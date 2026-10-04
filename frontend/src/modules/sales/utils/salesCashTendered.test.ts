import { describe, it, expect } from "vitest";
import { persistedCashTendered, tenderedForPayment } from "./salesCashTendered";

// POS-CASH-TENDERED-01 — el efectivo entregado se persiste en el pago en efectivo y Recibido/Vuelto
// se leen siempre de lo persistido (modal final, tirilla, reimpresión).

const pay = (amount: number, tendered: number | null) => ({
  tenderedAmount: tendered,
  changeAmount: tendered === null ? null : Math.round((tendered - amount) * 100) / 100,
});

describe("tenderedForPayment — qué se envía al backend", () => {
  it("efectivo con recibido mayor → envía lo recibido (14.66 / 20)", () => {
    expect(tenderedForPayment(true, 14.66, 20)).toBe(20);
  });

  it("pago exacto → envía el mismo monto", () => {
    expect(tenderedForPayment(true, 14.66, 14.66)).toBe(14.66);
  });

  it("forma de cobro que no es efectivo → nunca envía efectivo entregado", () => {
    expect(tenderedForPayment(false, 10, 20)).toBeNull();
  });

  it("sin recibido ingresado → null", () => {
    expect(tenderedForPayment(true, 14.66, null)).toBeNull();
  });

  it("recibido 1 centavo menor (dentro de tolerancia) → no se falsea: null", () => {
    expect(tenderedForPayment(true, 14.66, 14.65)).toBeNull();
  });
});

describe("persistedCashTendered — Recibido/Vuelto desde los pagos persistidos", () => {
  it("14.66 con 20 entregado → recibido 20, vuelto 5.34", () => {
    expect(persistedCashTendered([pay(14.66, 20)])).toEqual({ cashReceived: 20, cashChange: 5.34 });
  });

  it("pago exacto → recibido 14.66, vuelto 0", () => {
    expect(persistedCashTendered([pay(14.66, 14.66)])).toEqual({ cashReceived: 14.66, cashChange: 0 });
  });

  it("multipago tarjeta 10 + efectivo 4.66 con 10 entregado → vuelto 5.34", () => {
    expect(persistedCashTendered([pay(10, null), pay(4.66, 10)])).toEqual({
      cashReceived: 10,
      cashChange: 5.34,
    });
  });

  it("sin efectivo entregado (o venta histórica sin el dato) → null, no se muestra", () => {
    expect(persistedCashTendered([pay(14.66, null)])).toBeNull();
    expect(persistedCashTendered(undefined)).toBeNull();
  });
});
