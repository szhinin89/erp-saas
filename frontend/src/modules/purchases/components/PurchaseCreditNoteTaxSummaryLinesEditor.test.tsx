// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, cleanup, fireEvent, screen } from "@testing-library/react";
import { useFieldArray, useForm } from "react-hook-form";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseCreditNoteTaxSummaryLinesEditor } from "./PurchaseCreditNoteTaxSummaryLinesEditor";
import type { PurchaseInvoiceTaxSummaryDto } from "../api/purchaseService";
import type { PurchaseCreditNoteDraftFormValues } from "../schemas/purchaseCreditNoteSchema";

// Las columnas de solo lectura "Base compra" / "Ya acreditado" / "Disponible" / "ICE
// crédito" / "IVA crédito" / "Total NC" migraron de formatMoney a ZHMoneyValue
// (sin símbolo de moneda — el formatMoney original nunca mostró "$").

function buildSummary(
  overrides: Partial<PurchaseInvoiceTaxSummaryDto> = {},
): PurchaseInvoiceTaxSummaryDto {
  return {
    id: "ts-1",
    vatCode: "2",
    vatRate: 15,
    vatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceName: null,
    irbpnrCode: null,
    irbpnrRate: 0,
    irbpnrName: null,
    taxableBase: 100,
    iceAmount: 0,
    vatAmount: 15,
    irbpnrAmount: 0,
    totalAmount: 115,
    creditedTaxableBase: 20,
    availableTaxableBase: 80,
    ...overrides,
  };
}

function Wrapper({ taxSummaries }: { taxSummaries: PurchaseInvoiceTaxSummaryDto[] }) {
  const { control } = useForm<PurchaseCreditNoteDraftFormValues>({
    defaultValues: { lines: [], taxSummaryLines: [] } as never,
  });
  const { fields, append, remove } = useFieldArray({
    control,
    name: "taxSummaryLines",
  });
  return (
    <I18nProvider>
      <PurchaseCreditNoteTaxSummaryLinesEditor
        taxSummaries={taxSummaries}
        selected={fields}
        append={append}
        remove={remove}
      />
    </I18nProvider>
  );
}

afterEach(() => {
  cleanup();
});

describe("PurchaseCreditNoteTaxSummaryLinesEditor — columnas de solo lectura (ZHMoneyValue)", () => {
  it("Base compra / Ya acreditado / Disponible usan ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = render(
      <Wrapper taxSummaries={[buildSummary()]} />,
    );

    const cells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value",
    );
    expect(cells.length).toBeGreaterThanOrEqual(3);

    const texts = Array.from(cells).map((el) => el.textContent);
    expect(texts).toContain("100.00");
    expect(texts).toContain("20.00");
    expect(texts).toContain("80.00");
    cells.forEach((el) => {
      expect(el.textContent).not.toMatch(/\$/);
      expect(el.getAttribute("style")).toBeNull();
    });
  });

  it("ICE crédito / IVA crédito / Total NC muestran 0.00 sin capturar base (preview inicial)", () => {
    const { container } = render(
      <Wrapper taxSummaries={[buildSummary()]} />,
    );

    const cells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value",
    );
    const texts = Array.from(cells).map((el) => el.textContent);
    expect(texts.filter((t) => t === "0.00").length).toBeGreaterThanOrEqual(3);
  });
});

// PURCHASE-CREDIT-NOTE-DISCOUNT-DECIMAL-INPUT-01 — el input estaba controlado por un valor ya
// parseado a número (`String(selected[idx].taxableBase)`), que en cada tecla descartaba el punto
// decimal recién tecleado (y cualquier "0" final tras el punto). Estos tests simulan tecla por
// tecla (fireEvent.change secuencial, releyendo el value real del DOM tras cada re-render) para
// detectar exactamente esa regresión, no solo el resultado final.
describe("PurchaseCreditNoteTaxSummaryLinesEditor — Base descuento a aplicar admite decimales (PURCHASE-CREDIT-NOTE-DISCOUNT-DECIMAL-INPUT-01)", () => {
  function getInput(label = "IVA 15%") {
    return screen.getByLabelText(
      new RegExp(`Base descuento a aplicar: ${label}`),
    ) as HTMLInputElement;
  }

  it("permite teclear 3.50 sin perder el punto decimal en ninguna tecla", () => {
    render(<Wrapper taxSummaries={[buildSummary({ availableTaxableBase: 80 })]} />);
    const input = getInput();

    fireEvent.change(input, { target: { value: "3" } });
    expect(input.value).toBe("3");

    fireEvent.change(input, { target: { value: input.value + "." } });
    expect(input.value).toBe("3.");

    fireEvent.change(input, { target: { value: input.value + "5" } });
    expect(input.value).toBe("3.5");

    fireEvent.change(input, { target: { value: input.value + "0" } });
    expect(input.value).toBe("3.50");
  });

  it("permite teclear 0.25 sin perder el punto decimal", () => {
    render(<Wrapper taxSummaries={[buildSummary({ availableTaxableBase: 80 })]} />);
    const input = getInput();

    fireEvent.change(input, { target: { value: "0" } });
    expect(input.value).toBe("0");

    fireEvent.change(input, { target: { value: input.value + "." } });
    expect(input.value).toBe("0.");

    fireEvent.change(input, { target: { value: input.value + "2" } });
    expect(input.value).toBe("0.2");

    fireEvent.change(input, { target: { value: input.value + "5" } });
    expect(input.value).toBe("0.25");
  });

  it("Total NC calcula correctamente con una base decimal (12.57)", () => {
    const { container } = render(
      <Wrapper
        taxSummaries={[
          buildSummary({ vatRate: 15, iceRate: 0, availableTaxableBase: 80 }),
        ]}
      />,
    );
    const input = getInput();

    fireEvent.change(input, { target: { value: "12.57" } });

    const cells = container.querySelectorAll("td.zh-table-cell--num .zh-money-value");
    const texts = Array.from(cells).map((el) => el.textContent);
    // IVA 15% de 12.57 = 1.8855 → redondeado 1.89; Total = 12.57 + 1.89 = 14.46
    expect(texts).toContain("1.89");
    expect(texts).toContain("14.46");
  });

  it("no permite exceder la base disponible: muestra el error y no bloquea seguir editando", () => {
    render(
      <Wrapper taxSummaries={[buildSummary({ availableTaxableBase: 10 })]} />,
    );
    const input = getInput();

    fireEvent.change(input, { target: { value: "15" } });

    expect(input.value).toBe("15");
    expect(screen.getByText(/Excede la base disponible/)).toBeTruthy();
    expect(input.getAttribute("aria-invalid")).toBe("true");
  });

  it("no permite valores negativos (positiveOnly bloquea el guion en la tecla)", () => {
    render(<Wrapper taxSummaries={[buildSummary({ availableTaxableBase: 80 })]} />);
    const input = getInput();

    const prevented = !fireEvent.keyDown(input, { key: "-" });
    expect(prevented).toBe(true);
  });
});
