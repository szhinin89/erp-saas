// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, cleanup } from "@testing-library/react";
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
