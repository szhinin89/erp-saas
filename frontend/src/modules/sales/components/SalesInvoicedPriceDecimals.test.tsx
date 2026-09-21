// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach, beforeEach } from "vitest";
import { render, cleanup, fireEvent } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import { SalesRepricingTable } from "./SalesRepricingTable";
import { roundToDecimals } from "../../../lib/sanitizers";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import { buildRepricingPlan, mapResolvedPricingToLineFields } from "../hooks/useSalesCustomerRepricing";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto } from "../../inventory/types";
import type { SalesRepricingPreviewItemDto } from "../api/salesRepricingPreviewService";

// SALES-INVOICED-PRICE-CONFIGURED-DECIMALS-07C3 — "Precio facturado" (UnitPrice) y el resto de
// precios de venta siempre con salesUnitPriceDecimals, sin hardcode 2 ni toFixed sobre binario.

let salesDecimals = 2;
vi.mock("../../../lib/config/precisionPolicy.config", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../../../lib/config/precisionPolicy.config")>();
  return {
    ...actual,
    getPrecisionPolicy: () => ({
      ...TEST_PRECISION_POLICY,
      salesUnitPriceDecimals: salesDecimals,
    }),
  };
});

beforeEach(() => {
  salesDecimals = 2;
});
afterEach(() => cleanup());

const WAREHOUSES: WarehouseDto[] = [{ id: "wh-1", name: "Bodega" } as WarehouseDto];

function line(overrides: Partial<SalesLineFormValues> = {}): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "MANJAR",
    quantity: 1,
    unitPrice: 0.495, // 0.55 -10%
    vatCode: "10",
    discountPct: 0,
    _sku: "M1",
    _name: "MANJAR",
    _pvp: 0.495,
    _basePrice: 0.55,
    _isManualPrice: false,
    ...overrides,
  };
}

function renderLines(
  lines: SalesLineFormValues[],
  opts: { readOnly?: boolean; onUpdateLine?: (k: number, f: string, v: unknown) => void } = {},
) {
  return render(
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={lines}
        readOnly={opts.readOnly ?? false}
        disabled={opts.readOnly ?? false}
        onRemoveLine={vi.fn()}
        onUpdateLine={opts.onUpdateLine ?? vi.fn()}
        onAddItemLine={vi.fn()}
        onUpdateLineWarehouse={vi.fn()}
        warehouses={WAREHOUSES}
        selectedWarehouseId="wh-1"
        onWarehouseChange={vi.fn()}
        vatRates={{ "10": 15 }}
      />
    </MemoryRouter>,
  );
}

const priceInput = (c: HTMLElement) =>
  c.querySelector<HTMLInputElement>(".sf-product__price-input")!;
const listPriceText = (c: HTMLElement) =>
  c.querySelector(".sf-product__pricelist-value")?.textContent ?? "";

describe("roundToDecimals — mitad hacia arriba como el backend", () => {
  it("0.495 → 0.50 / 0.495 / 0.495 con 2 / 3 / 4 decimales (toFixed daría 0.49)", () => {
    expect((0.495).toFixed(2)).toBe("0.49"); // el bug de fondo que se evita
    expect(roundToDecimals(0.495, 2)).toBe(0.5);
    expect(roundToDecimals(0.495, 3)).toBe(0.495);
    expect(roundToDecimals(0.495, 4)).toBe(0.495);
  });

  it("absorbe ruido binario de una multiplicación por conversionFactor (0.495 × 3)", () => {
    expect(0.495 * 3).not.toBe(1.485);
    expect(roundToDecimals(0.495 * 3, 2)).toBe(1.49);
    expect(roundToDecimals(0.495 * 3, 3)).toBe(1.485);
  });

  it("negativos, cero y no finitos", () => {
    expect(roundToDecimals(-0.495, 2)).toBe(-0.5);
    expect(roundToDecimals(0, 2)).toBe(0);
    expect(Number.isNaN(roundToDecimals(NaN, 2))).toBe(true);
  });
});

describe("línea de venta nueva — Precio facturado y Precio lista con decimales configurados", () => {
  it.each([
    [2, "0.50", "0.55"],
    [3, "0.495", "0.550"],
    [4, "0.4950", "0.5500"],
  ])("config %i decimales → precio facturado %s y precio lista %s", (decimals, invoiced, list) => {
    salesDecimals = decimals;
    const { container } = renderLines([line()]);
    expect(priceInput(container).value).toBe(invoiced);
    expect(listPriceText(container)).toContain(list); // antes: siempre 2 decimales
  });
});

describe("draft reabierto y factura autorizada (read-only)", () => {
  it.each([
    [2, "0.50"],
    [3, "0.495"],
    [4, "0.4950"],
  ])("draft reabierto (editable) con config %i → %s", (decimals, expected) => {
    salesDecimals = decimals;
    const { container } = renderLines([
      line({ _priceListIdAtSale: "l1", _priceListNameAtSale: "MAYORISTA001", _traceabilityVersionAtSale: 1 }),
    ]);
    expect(priceInput(container).value).toBe(expected);
  });

  it.each([
    [2, "0.50", "0.55"],
    [3, "0.495", "0.550"],
    [4, "0.4950", "0.5500"],
  ])("factura autorizada con config %i → facturado %s, lista %s", (decimals, invoiced, list) => {
    salesDecimals = decimals;
    const { container } = renderLines(
      [
        line({
          _listPriceAtSale: 0.55,
          _priceListIdAtSale: "l1",
          _priceListNameAtSale: "MAYORISTA001",
          _traceabilityVersionAtSale: 1,
        }),
      ],
      { readOnly: true },
    );
    expect(priceInput(container).value).toBe(invoiced);
    expect(listPriceText(container)).toContain(list);
  });
});

describe("conversionFactor (venta por presentación)", () => {
  it.each([
    [2, "1.49"],
    [3, "1.485"],
    [4, "1.4850"],
  ])("0.495 × caja de 3, config %i → %s", (decimals, expected) => {
    salesDecimals = decimals;
    const fields = mapResolvedPricingToLineFields(0.495, 0.55, "MAYORISTA001", null, 3, "l1");
    const { container } = renderLines([line({ unitPrice: fields.unitPrice, conversionFactor: 3 })]);
    expect(priceInput(container).value).toBe(expected);
  });
});

describe("precio manual — foco/blur no lo altera", () => {
  it("blur sin cambios NO marca manual ni redondea el precio resuelto (config 2, resuelto 0.495)", () => {
    const onUpdateLine = vi.fn();
    const { container } = renderLines([line()], { onUpdateLine });
    const input = priceInput(container);
    fireEvent.focus(input);
    fireEvent.blur(input);
    expect(onUpdateLine).not.toHaveBeenCalled();
  });

  it("editar a un valor distinto sí se envía, ya con los decimales configurados", () => {
    salesDecimals = 3;
    const onUpdateLine = vi.fn();
    const { container } = renderLines([line()], { onUpdateLine });
    const input = priceInput(container);
    fireEvent.change(input, { target: { value: "0.480" } });
    fireEvent.blur(input);
    expect(onUpdateLine).toHaveBeenCalledWith(1, "unitPrice", 0.48);
  });

  it("línea manual (0.50 tecleado) se muestra con los decimales configurados", () => {
    salesDecimals = 4;
    const { container } = renderLines([line({ unitPrice: 0.5, _isManualPrice: true })]);
    expect(priceInput(container).value).toBe("0.5000");
  });
});

describe("modal repricing — precios actual y nuevo con decimales configurados", () => {
  const rows = (current: number, next: number) => [
    {
      key: 1,
      itemId: "i",
      description: "MANJAR",
      currentUnitPrice: current,
      fields: mapResolvedPricingToLineFields(next, next, "L", null, 1, "l1"),
    },
  ];

  it.each([
    [2, "0.60", "0.50"],
    [3, "0.600", "0.495"],
    [4, "0.6000", "0.4950"],
  ])("config %i → actual %s / nuevo %s", (decimals, current, next) => {
    const { container } = render(<SalesRepricingTable rows={rows(0.6, 0.495)} decimals={decimals} />);
    const amounts = Array.from(container.querySelectorAll(".zh-money-value__amount")).map(
      (n) => n.textContent,
    );
    expect(amounts).toEqual([current, next]);
  });

  it("buildRepricingPlan compara con los mismos decimales configurados (0.495 vs 0.50 a 2 = sin cambio)", () => {
    const preview: SalesRepricingPreviewItemDto[] = [
      {
        itemId: "i",
        oldResolvedPrice: 0,
        newResolvedPrice: 0.495,
        newBasePrice: 0.55,
        changed: true,
        oldPriceListId: null,
        oldPriceListName: "PVP",
        newPriceListId: "l1",
        newPriceListName: "MAYORISTA001",
        oldSelectionSource: null,
        newSelectionSource: "Customer",
        newDiscountDescription: null,
      },
    ];
    const lines = [
      { key: 1, itemId: "i", description: "MANJAR", unitPrice: 0.5, _priceListId: "l1" },
    ];
    expect(buildRepricingPlan(lines, preview, 2).priceChangedRows).toHaveLength(0); // 0.50 == 0.50
    expect(buildRepricingPlan(lines, preview, 3).priceChangedRows).toHaveLength(1); // 0.500 != 0.495
  });
});
