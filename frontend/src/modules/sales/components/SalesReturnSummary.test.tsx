// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { render, cleanup } from "@testing-library/react";
import { SalesReturnSummary } from "./SalesReturnSummary";
import type {
  SalesReturnDetailDto,
  SalesReturnDto,
} from "../api/salesReturnService";

// SALES-DS-MONEY-12 — todas las columnas/filas de este resumen migraron de
// formatMoney a ZHMoneyValue con currencySymbol="" (el resumen nunca mostró
// "$" — se preserva el texto visible exacto).

function buildLine(overrides: Partial<SalesReturnDetailDto> = {}): SalesReturnDetailDto {
  return {
    id: "line-1",
    originalInvoiceDetailId: "od-1",
    itemId: "item-1",
    description: "Producto X",
    snapshotSku: "SKU-1",
    snapshotItemName: "Producto X",
    warehouseId: "wh-1",
    uomCode: "UND",
    quantity: 2,
    unitPrice: 10,
    discountPct: 0,
    discountAmount: 0,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 3,
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    lineSubtotal: 20,
    taxableBase: 20,
    taxInclusiveTotal: 23,
    isFrozen: true,
    ...overrides,
  };
}

function buildSalesReturn(
  overrides: Partial<SalesReturnDto> = {},
): SalesReturnDto {
  return {
    id: "sr-1",
    salesInvoiceId: "inv-1",
    customerId: "cust-1",
    returnNumber: "DEV-001",
    reason: "Producto defectuoso",
    status: "Authorized",
    subtotal: 100,
    totalVat: 15,
    totalIce: 0,
    totalDiscount: 5,
    grandTotal: 110,
    lines: [buildLine()],
    refundAllocations: [{ id: "ra-1", method: "Cash", amount: 110 }],
    createdAt: "2026-07-01T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
});

describe("SalesReturnSummary — totales migrados a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it("la fila de línea (P. unitario/IVA/ICE/Total línea) usa ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn()} decimals={2} />,
    );

    const cells = container.querySelectorAll(
      ".sr-lines-table .zh-table-cell--num .zh-money-value",
    );
    expect(cells.length).toBeGreaterThan(0);
    const texts = Array.from(cells).map((c) => c.textContent);
    expect(texts).toContain("10.00");
    expect(texts).toContain("3.00");
    expect(texts).toContain("23.00");
  });

  it('"Total a reembolsar" usa ZHMoneyValue con el valor del grandTotal', () => {
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn({ grandTotal: 110 })} decimals={2} />,
    );

    const grandRow = container.querySelector(".sr-totals-grid__grand");
    const moneyValue = grandRow?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("110.00");
  });

  it("el descuento se muestra con el signo - seguido de ZHMoneyValue", () => {
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn({ totalDiscount: 5 })} decimals={2} />,
    );

    const rows = container.querySelectorAll(".sr-general-grid__value");
    const discountRow = Array.from(rows).find((r) =>
      r.textContent?.startsWith("-"),
    );
    expect(discountRow).toBeTruthy();
    expect(discountRow?.querySelector(".zh-money-value")?.textContent).toBe(
      "5.00",
    );
  });

  it("la asignación de reembolso usa ZHMoneyValue", () => {
    const { container } = render(
      <SalesReturnSummary
        salesReturn={buildSalesReturn({
          refundAllocations: [{ id: "ra-1", method: "Cash", amount: 110 }],
        })}
        decimals={2}
      />,
    );

    const table = Array.from(
      container.querySelectorAll("table.table--neutral"),
    ).find((t) => t.textContent?.includes("Efectivo (Caja)"));
    const moneyValue = table?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("110.00");
  });

  it("no hay estilos inline en ningún valor monetario del resumen", () => {
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn()} decimals={2} />,
    );

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
