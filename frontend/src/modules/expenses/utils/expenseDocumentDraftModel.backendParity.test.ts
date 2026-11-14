import { describe, expect, it } from "vitest";
import {
  calculateExpenseDocumentTotals,
  calculateExpenseLineTotals,
} from "./expenseDocumentDraftModel";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";

/**
 * EXPENSE-UX-01 (U7) — el cálculo de pantalla debe coincidir con el del backend
 * (`ExpenseLine`: decimal exacto, `Math.Round(..., 2, MidpointRounding.AwayFromZero)` en subtotal,
 * descuento, base, IVA y total; `ExpenseDocument`: suma de los importes ya redondeados por línea).
 *
 * Los valores esperados salen de aplicar esas mismas reglas con aritmética decimal exacta. Con
 * floats (`Math.round(x * 100) / 100`) los casos de empate binario (1.005, 1.015, 10.025…)
 * divergían en un centavo del valor que persistía el servidor.
 */

const VAT_BY_CODE = new Map<string, number>([
  ["0", 0],
  ["2", 15],
]);

function line(
  quantity: string,
  unitPrice: string,
  discountValue: string,
  vatCode: string,
): ExpenseDraftLineState {
  return {
    key: `${quantity}-${unitPrice}-${discountValue}-${vatCode}`,
    expenseSubcategoryId: "sub-1",
    description: "",
    quantity,
    unitPrice,
    discountValue,
    vatCode,
    notes: "",
  };
}

describe("calculateExpenseLineTotals — paridad con ExpenseLine (backend)", () => {
  it("empate binario en subtotal: 1 x 1.005 → 1.01 (antes 1.00)", () => {
    expect(calculateExpenseLineTotals(line("1", "1.005", "0.00", "2"), VAT_BY_CODE)).toEqual({
      subtotal: 1.01,
      discount: 0,
      taxableBase: 1.01,
      vat: 0.15,
      total: 1.16,
    });
  });

  it("empate binario sin IVA: 1 x 1.015 → 1.02 (antes 1.01)", () => {
    expect(calculateExpenseLineTotals(line("1", "1.015", "0.00", "0"), VAT_BY_CODE)).toEqual({
      subtotal: 1.02,
      discount: 0,
      taxableBase: 1.02,
      vat: 0,
      total: 1.02,
    });
  });

  it("cantidad x precio con 4 decimales: 3 x 0.3350 → 1.01", () => {
    expect(calculateExpenseLineTotals(line("3", "0.3350", "0.00", "2"), VAT_BY_CODE).subtotal).toBe(1.01);
  });

  it("descuento + IVA: redondea subtotal, descuento, base e IVA por separado (AwayFromZero)", () => {
    expect(calculateExpenseLineTotals(line("1", "10.0250", "0.015", "2"), VAT_BY_CODE)).toEqual({
      subtotal: 10.03,
      discount: 0.02,
      taxableBase: 10.01,
      vat: 1.5,
      total: 11.51,
    });
  });

  it("el descuento nunca supera el subtotal ni deja base negativa", () => {
    const totals = calculateExpenseLineTotals(line("1", "5.00", "9.00", "2"), VAT_BY_CODE);
    expect(totals.discount).toBe(5);
    expect(totals.taxableBase).toBe(0);
    expect(totals.vat).toBe(0);
    expect(totals.total).toBe(0);
  });

  it("valores no numéricos cuentan como cero (mismo criterio que parseExpenseNumber)", () => {
    const totals = calculateExpenseLineTotals(line("", "abc", "", "2"), VAT_BY_CODE);
    expect(totals).toEqual({ subtotal: 0, discount: 0, taxableBase: 0, vat: 0, total: 0 });
  });

  it("código IVA desconocido → tarifa 0 (el servidor lo rechaza al guardar)", () => {
    expect(calculateExpenseLineTotals(line("1", "10.00", "0.00", "99"), VAT_BY_CODE).vat).toBe(0);
  });
});

describe("calculateExpenseDocumentTotals — suma de importes de línea ya redondeados", () => {
  it("coincide con ExpenseDocument: subtotal 5.01 + 1.01, IVA 0.75 + 0.15", () => {
    const totals = calculateExpenseDocumentTotals(
      [line("2", "2.5050", "0.00", "2"), line("1", "1.005", "0.00", "2")],
      VAT_BY_CODE,
    );
    expect(totals).toEqual({
      subtotal: 6.02,
      totalDiscount: 0,
      totalTax: 0.9,
      grandTotal: 6.92,
    });
  });

  it("sin líneas devuelve ceros", () => {
    expect(calculateExpenseDocumentTotals([], VAT_BY_CODE)).toEqual({
      subtotal: 0,
      totalDiscount: 0,
      totalTax: 0,
      grandTotal: 0,
    });
  });
});
