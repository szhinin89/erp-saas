import { describe, it, expect } from "vitest";
import { resolveSalesEmissionType } from "./salesEmissionType";

// POS-EMISSION-TYPE-SNAPSHOT-01 — venta nueva: caja abierta; venta existente: snapshot de la factura.

const ELECTRONIC_SESSION = { emissionType: "Electronic" };
const PHYSICAL_SESSION = { emissionType: "Physical" };

describe("resolveSalesEmissionType", () => {
  it("nueva venta electrónica: toma el tipo del punto de emisión de la caja", () => {
    expect(resolveSalesEmissionType(null, ELECTRONIC_SESSION)).toBe("Electronic");
  });

  it("nueva venta física: toma el tipo del punto de emisión de la caja", () => {
    expect(resolveSalesEmissionType(null, PHYSICAL_SESSION)).toBe("Physical");
  });

  it("factura electrónica histórica abierta desde una caja FÍSICA → Electronic (manda el snapshot)", () => {
    expect(
      resolveSalesEmissionType({ emissionType: "Electronic" }, PHYSICAL_SESSION),
    ).toBe("Electronic");
  });

  it("factura física histórica abierta desde una caja ELECTRÓNICA → Physical (manda el snapshot)", () => {
    expect(
      resolveSalesEmissionType({ emissionType: "Physical" }, ELECTRONIC_SESSION),
    ).toBe("Physical");
  });

  it("factura existente sin caja abierta → snapshot", () => {
    expect(resolveSalesEmissionType({ emissionType: "Physical" }, null)).toBe("Physical");
  });

  it("sin factura ni caja (o caja sin tipo resuelto) → null, nunca un default inventado", () => {
    expect(resolveSalesEmissionType(null, null)).toBeNull();
    expect(resolveSalesEmissionType(null, { emissionType: null })).toBeNull();
  });
});
