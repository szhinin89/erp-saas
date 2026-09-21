// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { mapInvoiceLinesToFormValues } from "./salesInvoiceHydration";
import { resolveLinePriceListLabel, resolvePriceListHeader } from "./pricingTraceability";
import { SalesInvoiceDetailsSection } from "../components/SalesInvoiceDetailsSection";
import { SalesPriceListContext } from "../components/SalesPriceListContext";
import type { SalesInvoiceDetailDto, SalesInvoiceDto } from "../api/salesService";

// SALES-PRICING-UX-TRACEABILITY-07C1 — hidratación directa de loadForEdit (mapInvoiceLinesToFormValues)
// y su lectura en modo solo lectura: solo snapshots persistidos, nunca configuración actual.

afterEach(() => cleanup());

function detail(overrides: Partial<SalesInvoiceDetailDto>): SalesInvoiceDetailDto {
  return {
    id: "d",
    itemId: "item",
    warehouseId: null,
    description: "PRODUCTO",
    snapshotSku: "SKU",
    snapshotItemName: "PRODUCTO",
    uomCode: "UNIT",
    conversionFactor: 1,
    quantityInBaseUom: 1,
    quantity: 1,
    unitPrice: 1,
    discountPct: 0,
    discountAmount: 0,
    taxableBase: 1,
    vatCode: "10",
    vatRate: 15,
    vatAmount: 0.15,
    snapshotVatName: "IVA 15%",
    iceCode: null,
    iceRate: 0,
    iceAmount: 0,
    snapshotIceName: null,
    taxInclusiveTotal: 1.15,
    notes: null,
    sortOrder: 1,
    packagingLevelId: null,
    baseUomCode: "UNIT",
    warehouseName: null,
    unitCostAtSale: null,
    totalCostAtSale: null,
    listPriceAtSale: 1,
    priceListId: null,
    priceListName: null,
    pricingSource: null,
    discountSource: null,
    discountDescription: null,
    selectionSource: null,
    ...overrides,
  } as SalesInvoiceDetailDto;
}

function invoice(
  version: number | null | undefined,
  preferred: { id: string | null; name: string | null },
  lines: SalesInvoiceDetailDto[],
): SalesInvoiceDto {
  return {
    pricingTraceabilityVersion: version,
    customerPreferredPriceListId: preferred.id,
    customerPreferredPriceListName: preferred.name,
    lines,
  } as unknown as SalesInvoiceDto;
}

const V1_LINES = [
  detail({
    id: "a",
    snapshotItemName: "MANJAR",
    description: "MANJAR",
    priceListId: "l-may",
    priceListName: "MAYORISTA001",
    selectionSource: "Customer",
  }),
  detail({
    id: "b",
    snapshotItemName: "DURAZNO",
    description: "DURAZNO",
    priceListId: "l-gen",
    priceListName: "Lista General",
    selectionSource: "CompanyDefault",
  }),
  detail({
    id: "c",
    snapshotItemName: "FRUTILLA",
    description: "FRUTILLA",
    priceListId: null,
    priceListName: "Precio base", // sentinel real de Pricing cuando ninguna lista aplica
    selectionSource: null,
  }),
];

describe("loadForEdit — hidratación de factura con trazabilidad v1", () => {
  const inv = invoice(1, { id: "l-may", name: "MAYORISTA001" }, V1_LINES);
  const lines = mapInvoiceLinesToFormValues(inv);

  it("hidrata PriceListId/Name + SelectionSource + versión en cada línea", () => {
    expect(lines.map((l) => l._priceListIdAtSale)).toEqual(["l-may", "l-gen", null]);
    expect(lines.map((l) => l._priceListNameAtSale)).toEqual([
      "MAYORISTA001",
      "Lista General",
      "Precio base",
    ]);
    expect(lines.map((l) => l._selectionSourceAtSale)).toEqual(["Customer", "CompanyDefault", null]);
    expect(lines.every((l) => l._traceabilityVersionAtSale === 1)).toBe(true);
  });

  it("no hidrata captura en vivo (queda para el snapshot, nunca mezcla configuración actual)", () => {
    for (const l of lines) {
      expect(l._priceListId).toBeUndefined();
      expect(l._priceListName).toBeUndefined();
    }
  });

  it("Customer / CompanyDefault / PVP se resuelven correctamente a partir de lo hidratado", () => {
    expect(lines.map((l) => resolveLinePriceListLabel(l, true))).toEqual([
      { kind: "list", name: "MAYORISTA001" },
      { kind: "list", name: "Lista General" },
      { kind: "pvp" },
    ]);
  });

  it("cabecera: snapshot de lista preferente de la factura (no una consulta viva)", () => {
    expect(
      resolvePriceListHeader({
        hasCustomer: true,
        readOnly: true,
        saved: {
          pricingTraceabilityVersion: inv.pricingTraceabilityVersion ?? null,
          customerPreferredPriceListName: inv.customerPreferredPriceListName ?? null,
        },
        live: { status: "error", data: null },
      }),
    ).toEqual({ kind: "preferred", name: "MAYORISTA001", defaultName: null });
  });

  it("render solo lectura: MAYORISTA001 / Lista General / PVP", () => {
    const { container } = render(
      <MemoryRouter>
        <SalesInvoiceDetailsSection
          lines={lines}
          readOnly
          disabled={false}
          onRemoveLine={vi.fn()}
          onUpdateLine={vi.fn()}
          onAddItemLine={vi.fn()}
          onUpdateLineWarehouse={vi.fn()}
          warehouses={[]}
          selectedWarehouseId=""
          onWarehouseChange={vi.fn()}
          vatRates={{ "10": 15 }}
        />
      </MemoryRouter>,
    );
    expect(
      Array.from(container.querySelectorAll(".sf-product__pricelist-name")).map((n) => n.textContent),
    ).toEqual(["MAYORISTA001", "Lista General", "PVP"]);
  });
});

describe("loadForEdit — factura legacy (version null)", () => {
  const legacy = invoice(
    null,
    { id: null, name: null },
    [
      detail({ id: "x", priceListId: null, priceListName: null, selectionSource: null }),
      detail({ id: "y", priceListId: null, priceListName: null, selectionSource: null }),
    ],
  );
  const lines = mapInvoiceLinesToFormValues(legacy);

  it("hidrata version null sin fabricar valores", () => {
    expect(lines.every((l) => l._traceabilityVersionAtSale === null)).toBe(true);
    expect(lines.every((l) => l._selectionSourceAtSale === null)).toBe(true);
  });

  it("no infiere PVP ni lista en las líneas", () => {
    expect(lines.map((l) => resolveLinePriceListLabel(l, true))).toEqual([
      { kind: "none" },
      { kind: "none" },
    ]);
  });

  it("payload sin el campo (undefined) se trata igual que null", () => {
    const missing = mapInvoiceLinesToFormValues(invoice(undefined, { id: null, name: null }, [detail({})]));
    expect(missing[0]._traceabilityVersionAtSale).toBeNull();
    expect(resolveLinePriceListLabel(missing[0], true)).toEqual({ kind: "none" });
  });

  it("cabecera: 'Trazabilidad de precios no disponible', jamás 'Sin lista preferente'", () => {
    const state = resolvePriceListHeader({
      hasCustomer: true,
      readOnly: true,
      saved: { pricingTraceabilityVersion: null, customerPreferredPriceListName: null },
      live: { status: "ready", data: null },
    });
    expect(state).toEqual({ kind: "legacy" });
    render(<SalesPriceListContext state={state} />);
    const text = screen.getByTestId("sales-price-list-context").textContent;
    expect(text).toContain("Trazabilidad de precios no disponible");
    expect(text).not.toContain("Sin lista preferente");
  });
});
