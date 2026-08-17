// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { render, cleanup } from "@testing-library/react";
import { ReturnableLinesEditor } from "./ReturnableLinesEditor";
import type { ReturnableLineDto } from "../api/salesReturnService";

// SALES-DS-MONEY-12 — la columna "P. unitario" migró de formatMoney a
// ZHMoneyValue (decimals=dc.salesUnitPrice, sin símbolo de moneda — nunca
// mostró "$"). La cantidad a devolver sigue siendo un input editable.

function buildLine(overrides: Partial<ReturnableLineDto> = {}): ReturnableLineDto {
  return {
    invoiceDetailId: "od-1",
    itemId: "item-1",
    description: "Producto X",
    snapshotSku: "SKU-1",
    warehouseId: "wh-1",
    uomCode: "UND",
    originalQuantity: 5,
    returnedQuantity: 0,
    remainingQuantity: 5,
    unitPrice: 12.5,
    discountPct: 0,
    vatCode: "10",
    vatRate: 15,
    iceCode: null,
    iceRate: 0,
    ...overrides,
  };
}

afterEach(() => {
  cleanup();
});

describe("ReturnableLinesEditor — P. unitario migrado a ZHMoneyValue (SALES-DS-MONEY-12)", () => {
  it("la columna P. unitario usa ZHMoneyValue sin símbolo de moneda", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine({ unitPrice: 12.5 })]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    const cell = container.querySelector(
      "td.zh-table-cell--num .zh-money-value",
    );
    expect(cell).toBeTruthy();
    expect(cell?.textContent).toBe("12.50");
  });

  it("la cantidad a devolver sigue siendo un input editable, no ZHMoneyValue", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine()]}
        quantities={{ "od-1": "2" }}
        onChangeQuantity={vi.fn()}
      />,
    );

    const input = container.querySelector("input");
    expect(input).toBeTruthy();
    expect(input?.value).toBe("2");
  });

  it("no hay estilos inline en el valor migrado", () => {
    const { container } = render(
      <ReturnableLinesEditor
        lines={[buildLine()]}
        quantities={{}}
        onChangeQuantity={vi.fn()}
      />,
    );

    container.querySelectorAll(".zh-money-value").forEach((el) => {
      expect(el.getAttribute("style")).toBeNull();
    });
  });
});
