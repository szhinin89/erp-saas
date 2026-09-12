// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { ExpenseDocumentLinesEditor } from "./ExpenseDocumentLinesEditor";
import type { ExpenseDraftLineState } from "./ExpenseDocumentLinesEditor";
import type { SriVatRateLookup } from "../../items/facades/sriLookupFacade";

afterEach(cleanup);

const VAT_RATES: SriVatRateLookup[] = [
  { code: "0", name: "0% IVA", percentage: 0 },
  { code: "4", name: "15% IVA (tarifa general vigente)", percentage: 15 },
  { code: "5", name: "5% IVA", percentage: 5 },
  { code: "6", name: "No objeto de Impuesto", percentage: 0 },
  { code: "7", name: "Exento de IVA", percentage: 0 },
];

function makeLine(overrides: Partial<ExpenseDraftLineState> = {}): ExpenseDraftLineState {
  return {
    key: "line-1",
    expenseSubcategoryId: "",
    description: "",
    quantity: "1",
    unitPrice: "90.00",
    discountValue: "0.00",
    vatCode: "4",
    notes: "",
    ...overrides,
  };
}

describe("ExpenseDocumentLinesEditor — Codigo IVA selector", () => {
  it("shows the real effective percentage for every VAT code, never an ambiguous 'IVA vigente' label", () => {
    render(
      <ExpenseDocumentLinesEditor
        lines={[makeLine()]}
        tree={[]}
        accountsById={new Map()}
        vatRates={VAT_RATES}
        vatRateByCode={new Map(VAT_RATES.map((r) => [r.code, r.percentage]))}
        onChange={vi.fn()}
      />,
    );

    const options = screen.getAllByRole("option") as HTMLOptionElement[];
    const vatOptions = options.filter((option) =>
      VAT_RATES.some((rate) => rate.code === option.value),
    );

    expect(vatOptions).toHaveLength(VAT_RATES.length);
    // Ningun texto debe ser un "IVA vigente" ambiguo sin porcentaje ni informacion real.
    expect(vatOptions.some((option) => /^\d+\s*-\s*IVA vigente$/.test(option.textContent ?? ""))).toBe(false);
    expect(screen.getByText("4 - 15% IVA (tarifa general vigente)")).toBeDefined();
    expect(screen.getByText("5 - 5% IVA")).toBeDefined();
    expect(screen.getByText("6 - No objeto de Impuesto")).toBeDefined();
    expect(screen.getByText("7 - Exento de IVA")).toBeDefined();
  });

  it("computes the line VAT/total from the real catalog percentage, not a hardcoded table", () => {
    render(
      <ExpenseDocumentLinesEditor
        lines={[makeLine({ vatCode: "4" })]}
        tree={[]}
        accountsById={new Map()}
        vatRates={VAT_RATES}
        vatRateByCode={new Map(VAT_RATES.map((r) => [r.code, r.percentage]))}
        onChange={vi.fn()}
      />,
    );

    // subtotal 90 * 15% = 13.50 IVA, total 103.50 — igual al XML del bug reportado.
    expect(screen.getByText("13.50")).toBeDefined();
    expect(screen.getByText("103.50")).toBeDefined();
  });

  it("keeps a stale/unknown code visible with a warning instead of silently dropping the value", () => {
    render(
      <ExpenseDocumentLinesEditor
        lines={[makeLine({ vatCode: "20" })]}
        tree={[]}
        accountsById={new Map()}
        vatRates={VAT_RATES}
        vatRateByCode={new Map(VAT_RATES.map((r) => [r.code, r.percentage]))}
        onChange={vi.fn()}
      />,
    );

    expect(screen.getByText(/20 - Codigo IVA no vigente/)).toBeDefined();
  });
});
