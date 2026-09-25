import { describe, expect, it, vi } from "vitest";
import {
  formatDecimalDisplay,
  formatMoney,
  formatMoneyWithSymbol,
  normalizeOptionalCode,
  roundToDecimals,
  sanitizeDecimal,
} from "./sanitizers";

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

/** ZH-DESIGN-SYSTEM-PRECISION-02A — motor único de formato de presentación. */
describe("formatDecimalDisplay — motor único (02A)", () => {
  it.each([
    [0.075, 2, "0.08"], [0.305, 2, "0.31"], [1.005, 2, "1.01"], [-0.075, 2, "-0.08"],
    [0, 2, "0.00"], [0, 6, "0.000000"],
    [0.12345, 4, "0.1235"], [0.2261, 6, "0.226100"],
    [1.123456785, 8, "1.12345679"], [0.0045783210, 10, "0.0045783210"],
  ])("%s a %s decimales → %s (Decimal ROUND_HALF_UP, punto decimal, sin agrupación)", (value, decimals, expected) => {
    expect(formatDecimalDisplay(value, decimals)).toBe(expected);
  });

  it.each([0.075, 0.305, 1.005, -0.075, 0, 1234567.5])("formatMoney delega en el motor único (%s)", (value) => {
    expect(formatMoney(value, 2)).toBe(formatDecimalDisplay(value, 2));
    expect(formatMoney(value)).toBe(formatDecimalDisplay(value, 2));
  });

  it("con locale, Intl recibe la cadena YA redondeada (nunca un Number): no hay segundo redondeo", () => {
    // `format` es un getter del prototipo: se envuelve el constructor para registrar lo que recibe.
    const Original = Intl.NumberFormat;
    const received: unknown[] = [];
    const wrapper = vi.fn(function (locale?: string, options?: Intl.NumberFormatOptions) {
      const nf = new Original(locale, options);
      return { format: (v: Intl.StringNumericLiteral | number) => (received.push(v), nf.format(v)) };
    });
    Intl.NumberFormat = wrapper as unknown as typeof Intl.NumberFormat;
    try {
      expect(formatDecimalDisplay(1.005, 2, "en-US")).toBe("1.01");
      expect(received).toEqual(["1.01"]);
      expect(wrapper).toHaveBeenCalledWith("en-US", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    } finally {
      Intl.NumberFormat = Original;
    }
  });

  it("con locale solo cambia la representación (separadores/agrupación), no los dígitos", () => {
    expect(formatDecimalDisplay(1234567.5, 2, "en-US")).toBe("1,234,567.50");
    expect(formatDecimalDisplay(-0.075, 2, "en-US")).toBe("-0.08");
    expect(formatDecimalDisplay(0.0045783210, 10, "en-US")).toBe("0.0045783210");
    const es = formatDecimalDisplay(12345.675, 2, "es-EC");
    expect(es.replace(/\D/g, "")).toBe("1234568"); // mismos dígitos que "12345.68"
    expect(es).toBe(new Intl.NumberFormat("es-EC", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format("12345.68"));
  });
});

/** ZH-DESIGN-SYSTEM-PRECISION-03C0 — regla léxica única del separador decimal en entrada (paste). */
describe("sanitizeDecimal — separador decimal (03C0)", () => {
  it.each([
    ["1,5", 2, false, "1.5"],
    ["0,075", 3, false, "0.075"],
    ["-1,5", 2, false, "-1.5"],
    ["-1,5", 2, true, "1.5"],
    ["12,3499", 2, false, "12.34"], // sigue TRUNCANDO, no redondea
    ["1,5", 0, false, "1"], // decimals=0 descarta la parte decimal (igual que con punto)
  ])("coma decimal: %s (decimals %s, positiveOnly %s) → %s", (raw, decimals, positiveOnly, expected) => {
    expect(sanitizeDecimal(raw, decimals, positiveOnly)).toBe(expected);
  });

  it.each([
    ["1,234.56", 2, "1234.56"], // con punto presente, la coma es agrupación (contrato 01A)
    ["$ 1,234.56 USD", 2, "1234.56"],
    ["12.3499", 2, "12.34"],
    ["abc", 2, ""],
    ["", 2, ""],
    ["1.2.3", 4, "1.23"],
  ])("contratos existentes intactos: %s → %s", (raw, decimals, expected) => {
    expect(sanitizeDecimal(raw, decimals)).toBe(expected);
  });
});

/**
 * ZH-DESIGN-SYSTEM-PRECISION-03C01 — política oficial de separadores en paste (normalización léxica,
 * sin locale/Intl/Decimal): una sola aparición = decimal; mixtos → el separador MÁS A LA DERECHA es
 * decimal y el otro agrupación; un solo tipo repetido en grupos de 3 = agrupación. Luego trunca.
 */
describe("sanitizeDecimal — política de separadores (03C01)", () => {
  it.each([
    // una sola aparición → decimal (sin inferir miles por 3 dígitos)
    ["1,5", 2, "1.5"],
    ["0,075", 3, "0.075"],
    ["1,234", 3, "1.234"],
    ["1.234", 3, "1.234"],
    // mixtos → el separador de la derecha es decimal
    ["1.234,56", 2, "1234.56"],
    ["1,234.56", 2, "1234.56"],
    ["12.345,6789", 4, "12345.6789"],
    ["1.234.567,89", 2, "1234567.89"],
    ["1,234,567.89", 2, "1234567.89"],
    // un solo tipo repetido con grupos de 3 → agrupación
    ["1,234,567", 2, "1234567"],
    ["1.234.567", 2, "1234567"],
    // truncado posterior (no redondea)
    ["1.234,5678", 2, "1234.56"],
    ["1,234.5678", 2, "1234.56"],
    // malformado: contratos previos intactos
    ["1.2.3", 4, "1.23"],
    ["1,2,3", 2, "123"],
  ])("%s (decimals %s) → %s", (raw, decimals, expected) => {
    expect(sanitizeDecimal(raw, decimals)).toBe(expected);
  });

  it("signo: '-1.234,56' → '-1234.56'; con positiveOnly → '1234.56'", () => {
    expect(sanitizeDecimal("-1.234,56", 2)).toBe("-1234.56");
    expect(sanitizeDecimal("-1.234,56", 2, true)).toBe("1234.56");
  });
});
