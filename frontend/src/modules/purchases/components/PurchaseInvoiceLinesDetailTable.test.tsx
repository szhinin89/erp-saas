// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, cleanup } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseInvoiceLinesDetailTable } from "./PurchaseInvoiceLinesDetailTable";
import type { PurchaseLineDto } from "../api/purchaseService";

// Las columnas de solo lectura (Precio, Descuento, Base, IVA, ICE, IRBPNR, Total)
// migraron de formatMoney a ZHMoneyValue (sin símbolo de moneda — formatMoney nunca
// mostró "$"). "Cantidad" no es dinero, sigue siendo texto plano.

function buildLine(overrides: Partial<PurchaseLineDto> = {}): PurchaseLineDto {
  return {
    id: "line-1",
    itemId: "item-1",
    description: "Producto Uno",
    snapshotSku: "SKU-1",
    snapshotItemName: "Producto Uno",
    snapshotSupplierCode: null,
    packagingLevelId: null,
    uomCode: "UND",
    baseUomCode: "UND",
    conversionFactor: 1,
    quantityInBaseUom: 10,
    quantity: 10,
    unitPrice: 5,
    discountPct: 0,
    discountAmount: 2,
    freightAllocated: 0,
    otherCostsAllocated: 0,
    totalLineCost: 48,
    landedUnitCost: 4.8,
    taxableBase: 48,
    vatCode: "2",
    vatRate: 15,
    vatAmount: 7.2,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    irbpnrCode: null,
    irbpnrRate: 0,
    irbpnrAmount: 0.5,
    snapshotIrbpnrName: null,
    taxes: [],
    taxInclusiveTotal: 55.2,
    snapshotItemPvp: 0,
    snapshotWarehouseCode: null,
    ...overrides,
  } as never;
}

function renderTable(lines: PurchaseLineDto[]) {
  return render(
    <I18nProvider>
      <PurchaseInvoiceLinesDetailTable lines={lines} />
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
});

describe("PurchaseInvoiceLinesDetailTable — columnas de solo lectura (ZHMoneyValue)", () => {
  it("Precio/Descuento/Base/IVA/ICE/IRBPNR/Total usan ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = renderTable([buildLine()]);

    const cells = container.querySelectorAll(
      "td.zh-table-cell--num .zh-money-value",
    );
    expect(cells.length).toBe(7);

    const texts = Array.from(cells).map((el) => el.textContent);
    expect(texts).toEqual([
      "5.00",
      "2.00",
      "48.00",
      "7.20",
      "0.00",
      "0.50",
      "55.20",
    ]);
    cells.forEach((el) => {
      expect(el.textContent).not.toMatch(/\$/);
      expect(el.getAttribute("style")).toBeNull();
    });
  });

  it("la cantidad no se envuelve en ZHMoneyValue (no es dinero)", () => {
    const { getByText } = renderTable([buildLine({ quantity: 10, uomCode: "UND" })]);

    expect(getByText("10 UND")).toBeTruthy();
  });
});
