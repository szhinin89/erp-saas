import { describe, expect, it } from "vitest";
import { buildRegisterCollectionSchema } from "./registerCollectionSchema";

// ZH-DESIGN-SYSTEM-PRECISION-05 — el saldo del mensaje usa la escala money recibida (sin toFixed(2));
// la regla (monto ≤ saldo) es la misma con cualquier escala.
describe("buildRegisterCollectionSchema — representación del saldo (05)", () => {
  function messageFor(decimals: number) {
    const r = buildRegisterCollectionSchema(43.5, decimals).safeParse({ amount: 50 });
    expect(r.success).toBe(false);
    return r.error!.issues[0]!.message;
  }

  it("money 2 vs 3: mismo rechazo, solo cambia la representación", () => {
    expect(messageFor(2)).toBe("El monto no puede superar el saldo pendiente (43.50).");
    expect(messageFor(3)).toBe("El monto no puede superar el saldo pendiente (43.500).");
    expect(buildRegisterCollectionSchema(43.5, 3).safeParse({ amount: 43.5 }).success).toBe(true);
  });
});
