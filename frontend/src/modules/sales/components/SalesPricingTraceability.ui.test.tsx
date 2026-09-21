// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { SalesInvoiceDetailsSection } from "./SalesInvoiceDetailsSection";
import { SalesPriceListContext } from "./SalesPriceListContext";
import { I18nProvider } from "../../../i18n/i18n";
import { dictionaries } from "../../../i18n/dictionaries";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";
import type { WarehouseDto } from "../../inventory/types";

// SALES-PRICING-UX-TRACEABILITY-07C — presentación del origen del pricing (cabecera + línea).
// La lógica de decisión vive en utils/pricingTraceability (testeada aparte); aquí se fija lo
// renderizado: textos i18n, y que nada se recalcula en el frontend.

afterEach(() => cleanup());

const WAREHOUSES: WarehouseDto[] = [{ id: "wh-1", name: "Bodega Principal" } as WarehouseDto];

function line(overrides: Partial<SalesLineFormValues> = {}): SalesLineFormValues {
  return {
    _key: 1,
    itemId: "item-1",
    warehouseId: "wh-1",
    description: "MANJAR",
    quantity: 1,
    unitPrice: 0.55,
    vatCode: "10",
    discountPct: 0,
    _sku: "M1",
    _name: "MANJAR",
    _pvp: 0.55,
    _basePrice: 0.61,
    ...overrides,
  };
}

function renderLines(lines: SalesLineFormValues[], readOnly = false) {
  return render(
    <MemoryRouter>
      <SalesInvoiceDetailsSection
        lines={lines}
        readOnly={readOnly}
        disabled={false}
        onRemoveLine={vi.fn()}
        onUpdateLine={vi.fn()}
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

const secondary = (c: HTMLElement) =>
  Array.from(c.querySelectorAll(".sf-product__pricelist-name")).map((n) => n.textContent);

describe("línea — texto secundario de lista / PVP (usa solo metadata existente)", () => {
  it("línea con lista del cliente → nombre de la lista (y sigue la insignia de descuento)", () => {
    const { container } = renderLines([
      line({ _priceListId: "l1", _priceListName: "MAYORISTA001", _discountDescription: "Descuento 10% (regla general)" }),
    ]);
    expect(secondary(container)).toEqual(["MAYORISTA001"]);
    expect(container.querySelector(".sf-product__discount")?.textContent).toContain("-10%");
  });

  it("línea con lista default → nombre de esa lista", () => {
    const { container } = renderLines([
      line({ _priceListId: "l2", _priceListName: "Lista General" }),
    ]);
    expect(secondary(container)).toEqual(["Lista General"]);
  });

  it("línea PVP → 'PVP' (no el sentinel 'Precio base')", () => {
    const { container } = renderLines([line({ _priceListId: null, _priceListName: "Precio base" })]);
    expect(secondary(container)).toEqual(["PVP"]);
  });

  it("factura v1 usa el snapshot aunque la línea traiga otros valores en vivo", () => {
    const { container } = renderLines(
      [
        line({
          _priceListId: "live-id",
          _priceListName: "Lista viva (config actual)",
          _listPriceAtSale: 0.61,
          _priceListIdAtSale: "snap-id",
          _priceListNameAtSale: "MAYORISTA001",
          _selectionSourceAtSale: "Customer",
          _traceabilityVersionAtSale: 1,
        }),
      ],
      true,
    );
    expect(secondary(container)).toEqual(["MAYORISTA001"]);
  });

  it("factura v1 sin lista ni origen → PVP", () => {
    const { container } = renderLines(
      [
        line({
          _listPriceAtSale: 0.55,
          _priceListIdAtSale: null,
          _priceListNameAtSale: "Precio base",
          _selectionSourceAtSale: null,
          _traceabilityVersionAtSale: 1,
        }),
      ],
      true,
    );
    expect(secondary(container)).toEqual(["PVP"]);
  });

  it("legacy (version null) sin datos: no se fabrica PVP ni ninguna lista", () => {
    const { container } = renderLines(
      [
        line({
          _listPriceAtSale: 0.55,
          _priceListIdAtSale: null,
          _priceListNameAtSale: null,
          _selectionSourceAtSale: null,
          _traceabilityVersionAtSale: null,
        }),
      ],
      true,
    );
    expect(secondary(container)).toEqual([]);
  });

  it("no recalcula precios: el unitPrice mostrado es exactamente el de la línea", () => {
    const { container } = renderLines([line({ unitPrice: 0.55, _priceListId: "l1", _priceListName: "MAYORISTA001" })]);
    expect(container.querySelector(".sf-product__pricelist-value")?.textContent).toContain("0.61");
  });
});

describe("cabecera — SalesPriceListContext (i18n)", () => {
  const withI18n = (ui: React.ReactElement) => render(<I18nProvider>{ui}</I18nProvider>);

  it("cliente con lista propia → 'Lista preferente: X' y la default solo como secundaria", () => {
    withI18n(
      <SalesPriceListContext state={{ kind: "preferred", name: "MAYORISTA001", defaultName: "Lista General" }} />,
    );
    const box = screen.getByTestId("sales-price-list-context");
    expect(box.textContent).toContain("Lista preferente: MAYORISTA001");
    expect(box.textContent).toContain("Predeterminada: Lista General");
  });

  it("sin lista propia → 'Sin lista preferente' (la default nunca se llama preferente)", () => {
    withI18n(<SalesPriceListContext state={{ kind: "none", defaultName: "Lista General" }} />);
    const box = screen.getByTestId("sales-price-list-context");
    expect(box.textContent).toContain("Sin lista preferente");
    expect(box.textContent).not.toContain("Lista preferente: Lista General");
  });

  it("legacy → 'Trazabilidad de precios no disponible'", () => {
    withI18n(<SalesPriceListContext state={{ kind: "legacy" }} />);
    expect(screen.getByTestId("sales-price-list-context").textContent).toContain(
      "Trazabilidad de precios no disponible",
    );
  });

  it("error del query → estado neutro, no lanza", () => {
    withI18n(<SalesPriceListContext state={{ kind: "contextError" }} />);
    expect(screen.getByTestId("sales-price-list-context").textContent).toContain(
      "Lista preferente no disponible",
    );
  });

  it("hidden / sin estado → no renderiza nada", () => {
    const { container, rerender } = withI18n(<SalesPriceListContext state={{ kind: "hidden" }} />);
    expect(container.innerHTML).toBe("");
    rerender(
      <I18nProvider>
        <SalesPriceListContext />
      </I18nProvider>,
    );
    expect(container.innerHTML).toBe("");
  });

  it("funciona sin I18nProvider (cae al idioma por defecto)", () => {
    render(<SalesPriceListContext state={{ kind: "none", defaultName: null }} />);
    expect(screen.getByTestId("sales-price-list-context").textContent).toContain("Sin lista preferente");
  });
});

describe("i18n — claves sales.pricing.* en ES/EN/QU sin hardcodes", () => {
  const KEYS = [
    "sales.pricing.preferredList",
    "sales.pricing.noPreferredList",
    "sales.pricing.defaultList",
    "sales.pricing.traceabilityUnavailable",
    "sales.pricing.contextUnavailable",
    "sales.pricing.pvp",
  ];
  it.each(["es", "en", "qu"] as const)("locale %s tiene todas las claves y conserva {{name}}", (locale) => {
    for (const key of KEYS) {
      const text = dictionaries[locale][key];
      expect(text, `${locale}:${key}`).toBeTruthy();
      if (key.endsWith("preferredList") && !key.includes("no")) expect(text).toContain("{{name}}");
      if (key.endsWith("defaultList")) expect(text).toContain("{{name}}");
    }
  });
});
