// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { FormProvider, useForm } from "react-hook-form";
import { PricingTab } from "./PricingTab";
import type { CreateItemFormValues } from "../../schemas/createItemSchema";

vi.mock("../../../pricing/facades/priceListLookupFacade", () => ({
  priceListLookupFacade: {
    list: vi.fn().mockResolvedValue([
      {
        id: "default",
        name: "Default",
        isDefault: true,
        currencyCode: "USD",
      },
    ]),
  },
}));

vi.mock(
  "../../../configuracion/empresa/facades/companyProfileLookupFacade",
  () => ({
    companyProfileLookupFacade: {
      getProfile: vi.fn().mockResolvedValue({ currencyCode: "USD" }),
    },
  }),
);

vi.mock("../../api/itemService", () => ({
  itemService: {
    getProfitability: vi.fn().mockResolvedValue({
      averageCost: 80,
      currencyCode: "USD",
    }),
    getPricingSimulation: vi.fn().mockResolvedValue([]),
    previewPricingSimulation: vi.fn().mockResolvedValue([]),
    setPriceLists: vi.fn().mockResolvedValue([]),
  },
}));

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;
const vatRateOptions = [{ code: "IVA15", name: "IVA 15%", percentage: 15 }];

function PricingHarness({
  baseSalePrice = 100,
  saleVatCode = "IVA15",
  itemId,
}: {
  baseSalePrice?: number | null;
  saleVatCode?: string;
  itemId?: string;
}) {
  const methods = useForm<CreateItemFormValues>({
    defaultValues: {
      baseSalePrice,
      taxConfig: {
        saleVatCode,
        purchaseVatCode: "",
        exciseTaxCode: "",
      },
      saleConfig: {
        isForSale: true,
        maxDiscountPercent: null,
        isAvailableOnWeb: false,
        isAvailableOnPOS: false,
        isAvailableOnMobile: false,
        isEcommerceActive: false,
      },
    } as CreateItemFormValues,
  });
  const watchedBasePrice = methods.watch("baseSalePrice");

  return (
    <FormProvider {...methods}>
      <PricingTab
        t={t}
        disabled={false}
        itemId={itemId}
        vatRateOptions={vatRateOptions}
      />
      <output data-testid="base-sale-price">{watchedBasePrice ?? ""}</output>
    </FormProvider>
  );
}

describe("PricingTab", () => {
  it("calcula IVA desde precio sin IVA sin persistir campos derivados", async () => {
    render(<PricingHarness baseSalePrice={100} />);

    const netMetric = screen
      .getByText("Precio sin IVA que se guardará")
      .closest(".items-metric-card");
    const vatMetric = screen
      .getByText("IVA calculado")
      .closest(".items-metric-card");
    const grossMetric = screen
      .getByText("Precio final con IVA")
      .closest(".items-metric-card");

    expect(netMetric?.textContent).toContain("100.00");
    expect(vatMetric?.textContent).toContain("15.00");
    expect(grossMetric?.textContent).toContain("115.00");
    expect(screen.getByTestId("base-sale-price").textContent).toBe("100");
  });

  it("convierte precio con IVA a BaseSalePrice sin IVA", async () => {
    render(<PricingHarness baseSalePrice={100} />);

    const priceInput = screen.getByLabelText(
      "Precio ingresado",
    ) as HTMLInputElement;
    const modeSelect = screen.getByLabelText(
      "El precio ingresado es",
    ) as HTMLSelectElement;

    fireEvent.change(modeSelect, { target: { value: "gross" } });
    expect(priceInput.value).toBe("115");

    fireEvent.change(priceInput, { target: { value: "230" } });

    await waitFor(() => {
      expect(Number(screen.getByTestId("base-sale-price").textContent)).toBe(
        200,
      );
    });
  });

  it("muestra alerta cuando el precio queda por debajo del costo", async () => {
    render(<PricingHarness baseSalePrice={50} itemId="item-1" />);

    await waitFor(() => {
      expect(
        screen.getByText(
          "El precio está por debajo del costo. Esta venta generaría pérdida.",
        ),
      ).toBeTruthy();
      expect(screen.getByText("Pérdida")).toBeTruthy();
    });
  });

  it("calcula utilidad, margen, markup y estado cuando hay costo", async () => {
    render(<PricingHarness baseSalePrice={100} itemId="item-1" />);

    await waitFor(() => {
      const profitMetric = screen
        .getByText("Utilidad estimada por unidad")
        .closest(".items-metric-card");
      const marginMetric = screen
        .getByText("Margen sobre venta")
        .closest(".items-metric-card");
      const markupMetric = screen
        .getByText("Markup sobre costo")
        .closest(".items-metric-card");
      const statusMetric = screen
        .getByText("Estado")
        .closest(".items-metric-card");

      expect(profitMetric?.textContent).toContain("20.00");
      expect(marginMetric?.textContent).toContain("20.00%");
      expect(markupMetric?.textContent).toContain("25.00%");
      expect(statusMetric?.textContent).toContain("Saludable");
    });
  });

  it("muestra estado sin costo para ítems nuevos", () => {
    render(<PricingHarness baseSalePrice={100} />);

    expect(
      screen.getByText(
        "No hay costo disponible. El margen se calculará cuando exista una compra confirmada.",
      ),
    ).toBeTruthy();
    expect(screen.getByText("Sin costo disponible")).toBeTruthy();
  });

  it("mantiene una sola sección principal y un solo input editable de precio", () => {
    render(<PricingHarness baseSalePrice={100} />);

    expect(
      screen.getAllByText("Precio de venta, costo y rentabilidad"),
    ).toHaveLength(1);

    const pricingSection = screen
      .getByText("Precio de venta, costo y rentabilidad")
      .closest(".zh-form-section");
    expect(pricingSection).toBeTruthy();

    const priceInputs = within(pricingSection as HTMLElement).getAllByLabelText(
      "Precio ingresado",
    );
    expect(priceInputs).toHaveLength(1);
    expect(
      (pricingSection as HTMLElement).querySelector(".items-metric-grid"),
    ).toBeTruthy();
  });

  it("no muestra el simulador viejo de Nuevo PVP ni botón Simular", () => {
    render(<PricingHarness baseSalePrice={100} itemId="item-1" />);

    expect(screen.queryByText("Nuevo PVP")).toBeNull();
    expect(screen.queryByRole("button", { name: "Simular" })).toBeNull();
    expect(screen.queryByText("Simulador de Precio")).toBeNull();
  });
});
