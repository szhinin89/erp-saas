import { describe, expect, it } from "vitest";
import { formatMoney, formatMoneyWithSymbol, normalizeOptionalCode, roundToDecimals } from "./sanitizers";

describe("money presentation HALF_UP / AwayFromZero", () => {
  it.each([
    [0.075, 2, "0.08"], [0.305, 2, "0.31"],
    [-0.075, 2, "-0.08"], [-0.305, 2, "-0.31"],
    [0.03, 2, "0.03"], [0.00345, 2, "0.00"],
    [0.074999, 2, "0.07"], [0.3000, 4, "0.3000"],
    [0.00345, 6, "0.003450"],
  ])("formats %s at %s decimals as %s", (value, decimals, expected) => {
    expect(formatMoney(value, decimals)).toBe(expected);
    expect(formatMoneyWithSymbol(value, decimals)).toBe(`$${expected}`);
  });
});

describe("normalizeOptionalCode", () => {
  it("convierte null a null", () => {
    expect(normalizeOptionalCode(null)).toBeNull();
  });

  it("convierte undefined a null", () => {
    expect(normalizeOptionalCode(undefined)).toBeNull();
  });

  it('convierte "" a null', () => {
    expect(normalizeOptionalCode("")).toBeNull();
  });

  it('convierte "   " (solo espacios) a null', () => {
    expect(normalizeOptionalCode("   ")).toBeNull();
  });

  it("recorta espacios de un código con contenido real", () => {
    expect(normalizeOptionalCode(" ICE01 ")).toBe("ICE01");
  });

  it("deja intacto un código sin espacios", () => {
    expect(normalizeOptionalCode("ICE01")).toBe("ICE01");
  });
});


describe("roundToDecimals ? operational precision", () => {
  it.each([
    [0.0045783210, 2, 0], [0.0045783210, 6, 0.004578],
    [0.0045783210, 10, 0.0045783210],
    [1234567.123456, 2, 1234567.12], [1234567.123456, 6, 1234567.123456],
    [1234567.123456, 10, 1234567.123456],
    [0.495, 2, 0.5], [1.4849999999999999, 2, 1.49],
    [0.00000000015, 10, 0.0000000002], [1.2345675, 6, 1.234568],
    [1.234567499, 6, 1.234567], [1e-10, 10, 1e-10],
  ])("rounds %s at scale %s to %s, symmetrically", (value, decimals, expected) => {
    expect(roundToDecimals(value, decimals)).toBe(expected);
    expect(roundToDecimals(-value, decimals)).toBe(-expected);
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-01A — characterization de formatMoney / formatMoneyWithSymbol.
 * "legacy:" = default decimals=2 y símbolo antes del signo; se congelan temporalmente.
 */
describe("formatMoney / formatMoneyWithSymbol — characterization 01A", () => {
  it("legacy: default decimals=2 en ambos formatters", () => {
    expect(formatMoney(5)).toBe("5.00");
    expect(formatMoneyWithSymbol(5)).toBe("$5.00");
  });

  it.each([
    [0, 0, "0"], [0, 2, "0.00"], [-0, 2, "0.00"], [0, 6, "0.000000"],
  ])("contract: cero %s a %s decimales → %s", (value, decimals, expected) => {
    expect(formatMoney(value, decimals)).toBe(expected);
  });

  it.each([
    [12.345678, 0, "12"], [12.345678, 2, "12.35"],
    [12.345678, 4, "12.3457"], [12.345678, 6, "12.345678"],
    [1234567.5, 2, "1234567.50"], [1.005, 2, "1.01"],
  ])("contract: %s a %s decimales → %s (sin separador de miles, HALF_UP)", (value, decimals, expected) => {
    expect(formatMoney(value, decimals)).toBe(expected);
  });

  it("legacy: símbolo va antes del signo en negativos ('$-5.00')", () => {
    expect(formatMoneyWithSymbol(-5, 2)).toBe("$-5.00");
  });

  it("contract: símbolo personalizado y vacío", () => {
    expect(formatMoneyWithSymbol(5, 2, "USD ")).toBe("USD 5.00");
    expect(formatMoneyWithSymbol(5, 2, "")).toBe("5.00");
  });
});
