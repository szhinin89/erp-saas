// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { useState } from "react";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
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

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04C — Cantidad → `precision="quantity"`, Valor unitario →
 * `precision="purchaseUnitPrice"`, Descuento (MONTO: DiscountAmount numeric(18,2), redondeado por
 * el dominio a FiscalPrecision.TaxAmount) → `precision="money"`. onChange (recálculo en vivo) y
 * payload intactos.
 */
describe("ExpenseDocumentLinesEditor — precisión semántica (04C)", () => {
  function Stateful({ spy }: { spy: (lines: ExpenseDraftLineState[]) => void }) {
    const [lines, setLines] = useState([makeLine()]);
    return (
      <ExpenseDocumentLinesEditor
        lines={lines}
        tree={[]}
        accountsById={new Map()}
        vatRates={VAT_RATES}
        vatRateByCode={new Map(VAT_RATES.map((r) => [r.code, r.percentage]))}
        onChange={(next) => {
          spy(next);
          setLines(next);
        }}
      />
    );
  }

  /** ¿Admite un dígito más tras `digits` decimales? (usa el estado real del editor). */
  function allowsDecimal(input: HTMLInputElement, digits: number) {
    fireEvent.change(input, { target: { value: `1.${"1".repeat(digits)}` } });
    input.setSelectionRange(input.value.length, input.value.length);
    return fireEvent.keyDown(input, { key: "9" });
  }

  const POLICY = { ...TEST_PRECISION_POLICY, quantityDecimals: 3, purchaseUnitPriceDecimals: 5, moneyDecimals: 2 };

  it("cantidad/valor unitario/descuento toman quantity (3), purchaseUnitPrice (5) y money (2)", () => {
    setPrecisionPolicyForTests(POLICY);
    render(<Stateful spy={() => {}} />);
    const qty = screen.getByLabelText(/^Cantidad/) as HTMLInputElement;
    const price = screen.getByLabelText(/^Valor unitario/) as HTMLInputElement;
    const discount = screen.getByLabelText(/^Descuento/) as HTMLInputElement;
    expect([allowsDecimal(qty, 2), allowsDecimal(qty, 3)]).toEqual([true, false]);
    expect([allowsDecimal(price, 4), allowsDecimal(price, 5)]).toEqual([true, false]);
    expect([allowsDecimal(discount, 1), allowsDecimal(discount, 2)]).toEqual([true, false]);
  });

  it("policy A → B sin remount: la cantidad sigue la nueva escala en la siguiente edición", () => {
    setPrecisionPolicyForTests(POLICY);
    render(<Stateful spy={() => {}} />);
    const qty = screen.getByLabelText(/^Cantidad/) as HTMLInputElement;
    act(() => setPrecisionPolicyForTests({ ...POLICY, quantityDecimals: 1 }));
    expect(screen.getByLabelText(/^Cantidad/)).toBe(qty);
    expect(allowsDecimal(qty, 1)).toBe(false);
  });

  it("coma/paste → valor canónico y el onChange (recálculo) recibe el mismo shape de línea", () => {
    setPrecisionPolicyForTests(POLICY);
    const spy = vi.fn();
    render(<Stateful spy={spy} />);
    const discount = screen.getByLabelText(/^Descuento/) as HTMLInputElement;
    fireEvent.paste(discount, { clipboardData: { getData: () => "1.234,5" } });
    expect(discount.value).toBe("1234.5");
    expect(spy).toHaveBeenLastCalledWith([makeLine({ discountValue: "1234.5" })]);
  });
});
