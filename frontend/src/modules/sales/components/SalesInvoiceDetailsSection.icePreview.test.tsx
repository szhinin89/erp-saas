// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import type { SalesInvoiceDetailDto } from "../api/salesService";
import * as salesCalc from "../utils/salesCalc";

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
});

function renderLine(unitPrice: number, iceCode?: string, backendLine?: SalesInvoiceDetailDto) {
  return render(
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={[{ _key: 1, description: "Producto", quantity: 1, unitPrice,
          discountPct: 0, vatCode: "15", iceCode }]}
        backendLines={backendLine ? [backendLine] : undefined}
        readOnly={false}
        disabled={false}
        onRemoveLine={vi.fn()}
        onUpdateLine={vi.fn()}
        onAddItemLine={vi.fn()}
        onUpdateLineWarehouse={vi.fn()}
        warehouses={[]}
        selectedWarehouseId=""
        onWarehouseChange={vi.fn()}
        vatRates={{ "15": 15 }}
        iceRates={{ ICE10: 10 }}
      />
    </MemoryRouter>,
  );
}

function expectAmounts(container: HTMLElement, base: string, vat: string, total: string) {
  const amounts = container.querySelectorAll(".sf-product__subtotal-value");
  expect(Array.from(amounts, (el) => el.textContent)).toEqual([base, vat]);
  expect(container.querySelector(".sf-product__total-amount")?.textContent).toBe(total);
}

describe("SALES-LINE-ICE-PREVIEW-PARITY-04", () => {
  it("passes ICE through section and grid to the real fiscal helper", () => {
    // Call-through spy: the existing row has no separate ICE amount display.
    const fiscal = vi.spyOn(salesCalc, "calcFiscalLine");
    const { container } = renderLine(100, "ICE10");
    expect(fiscal).toHaveBeenCalledWith(
      expect.objectContaining({ unitPrice: 100, quantity: 1, iceCode: "ICE10" }),
      { "15": 15 }, { ICE10: 10 },
    );
    expect(fiscal.mock.results[0].value).toMatchObject({
      taxableBase: 100, ice: 10, vat: 16.5, total: 126.5,
    });
    expectAmounts(container, "$100.00", "$16.50", "$126.50");
  });

  it("preserves fiscal rounding without ICE", () => {
    const { container } = renderLine(0.3);
    expectAmounts(container, "$0.30", "$0.05", "$0.35");
  });

  it("prioritizes backend values even when the local ICE preview differs", () => {
    const backendLine = {
      taxableBase: 200, iceAmount: 20, vatAmount: 33, taxInclusiveTotal: 253,
    } as SalesInvoiceDetailDto;
    const { container } = renderLine(100, "ICE10", backendLine);
    expectAmounts(container, "$200.00", "$33.00", "$253.00");
  });
});
