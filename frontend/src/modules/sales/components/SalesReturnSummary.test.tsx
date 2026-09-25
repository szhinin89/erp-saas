// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import { act, render, cleanup } from "@testing-library/react";
import { SalesReturnSummary } from "./SalesReturnSummary";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
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
      <SalesReturnSummary salesReturn={buildSalesReturn()} />,
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
      <SalesReturnSummary salesReturn={buildSalesReturn({ grandTotal: 110 })} />,
    );

    const grandRow = container.querySelector(".sr-totals-grid__grand");
    const moneyValue = grandRow?.querySelector(".zh-money-value");
    expect(moneyValue).toBeTruthy();
    expect(moneyValue?.textContent).toBe("110.00");
  });

  it("el descuento se muestra con el signo - seguido de ZHMoneyValue", () => {
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn({ totalDiscount: 5 })} />,
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
      <SalesReturnSummary salesReturn={buildSalesReturn()} />,
    );

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});

describe("SalesReturnSummary — precisión de línea (ERP-PRECISION-FRONTEND-06B)", () => {
  it("cantidad usa quantityDecimals y P. unitario usa salesUnitPriceDecimals; IVA/total siguen en moneyDecimals", () => {
    setPrecisionPolicyForTests({
      ...TEST_PRECISION_POLICY,
      quantityDecimals: 6,
      salesUnitPriceDecimals: 4,
    });
    const { container } = render(
      <SalesReturnSummary
        salesReturn={buildSalesReturn({
          lines: [buildLine({ quantity: 1.234567, unitPrice: 12.3457, vatAmount: 3, taxInclusiveTotal: 23 })],
        })}
      />,
    );

    const row = container.querySelector(".sr-lines-table tbody tr");
    const texts = Array.from(row?.querySelectorAll("td") ?? []).map((td) => td.textContent);
    expect(texts).toContain("1.234567");
    expect(texts).toContain("12.3457");
    expect(texts).toContain("3.00");
    expect(texts).toContain("23.00");
  });

  it("con quantityDecimals=0 la cantidad se muestra sin decimales", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 0 });
    const { container } = render(
      <SalesReturnSummary
        salesReturn={buildSalesReturn({ lines: [buildLine({ quantity: 3 })] })}
      />,
    );

    const row = container.querySelector(".sr-lines-table tbody tr");
    const texts = Array.from(row?.querySelectorAll("td") ?? []).map((td) => td.textContent);
    expect(texts).toContain("3");
  });
});

describe("SalesReturnSummary — cantidad reactiva sin remount (ZH-DESIGN-SYSTEM-PRECISION-04E)", () => {
  it("la cantidad (texto compuesto con usePrecisionDecimals) reacciona A → B; sin símbolo $", () => {
    setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 2 });
    const { container } = render(
      <SalesReturnSummary salesReturn={buildSalesReturn({ lines: [buildLine({ quantity: 1.5 })] })} />,
    );
    const cells = () =>
      Array.from(container.querySelectorAll(".sr-lines-table tbody tr td")).map((td) => td.textContent);
    expect(cells()).toContain("1.50");

    act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, quantityDecimals: 4 }));
    expect(cells()).toContain("1.5000");
    expect(container.querySelector(".sr-lines-table")?.textContent).not.toContain("$");
  });
});
