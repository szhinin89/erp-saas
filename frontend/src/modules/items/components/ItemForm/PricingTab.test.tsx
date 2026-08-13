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
    setPriceLists: vi.fn().mockResolvedValue([]),
  },
}));

afterEach(() => cleanup());

const t = (_key: string, fallback?: string) => fallback ?? _key;
const vatRateOptions = [
  { code: "IVA15", name: "IVA 15%", percentage: 15 },
  { code: "IVA0", name: "IVA 0%", percentage: 0 },
  { code: "EXENTO", name: "Exento de IVA", percentage: 0 },
];
const oldPvpLabel = ["Nuevo", "PVP"].join(" ");
const oldSimulateLabel = ["Sim", "ular"].join("");

function PricingHarness({
  baseSalePrice = 100,
  saleVatCode = "IVA15",
  itemId,
  rates = vatRateOptions,
}: {
  baseSalePrice?: number | null;
  saleVatCode?: string;
  itemId?: string;
  rates?: typeof vatRateOptions;
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
      <select
        aria-label="External sale VAT"
        {...methods.register("taxConfig.saleVatCode")}
      >
        {rates.map((rate) => (
          <option key={rate.code} value={rate.code}>
            {rate.name}
          </option>
        ))}
      </select>
      <PricingTab
        t={t}
        disabled={false}
        itemId={itemId}
        vatRateOptions={rates}
      />
      <output data-testid="base-sale-price">{watchedBasePrice ?? ""}</output>
    </FormProvider>
  );
}

function getPricingSection() {
  return screen
    .getByText("Precio de venta, costo y rentabilidad")
    .closest(".zh-form-section") as HTMLElement;
}

function getPriceInput() {
  return screen.getByLabelText("Precio ingresado") as HTMLInputElement;
}

function getPriceModeSelect() {
  return screen.getByLabelText("El precio ingresado es") as HTMLSelectElement;
}

function getMetric(label: string) {
  return screen.getByText(label).closest(".items-metric-card") as HTMLElement;
}

function getMetricValue(label: string) {
  return getMetric(label).querySelector(".items-metric-card__value")
    ?.textContent;
}

describe("PricingTab", () => {
  it("calcula IVA desde precio sin IVA sin persistir campos derivados", async () => {
    render(<PricingHarness baseSalePrice={100} />);

    await waitFor(() => {
      expect(getMetricValue("Tarifa IVA venta")).toContain("IVA 15%");
      expect(getMetricValue("Precio sin IVA que se guardará")).toContain(
        "100.00",
      );
      expect(getMetricValue("IVA calculado")).toContain("15.00");
      expect(getMetricValue("Precio final con IVA")).toContain("115.00");
    });
    expect(screen.getByTestId("base-sale-price").textContent).toBe("100");
  });

  it("mantiene 2.99 visible y calcula BaseSalePrice sin IVA con tarifa dinámica 15%", async () => {
    render(<PricingHarness baseSalePrice={null} />);

    const priceInput = getPriceInput();
    const modeSelect = getPriceModeSelect();

    fireEvent.change(priceInput, { target: { value: "2.99" } });
    fireEvent.change(modeSelect, { target: { value: "gross" } });

    await waitFor(() => {
      expect(priceInput.value).toBe("2.99");
      expect(getMetricValue("Precio sin IVA que se guardará")).toContain(
        "2.60",
      );
      expect(getMetricValue("IVA calculado")).toContain("0.39");
      expect(screen.getByTestId("base-sale-price").textContent).toBe("2.6");
    });
  });

  it("mantiene 2.60 visible y calcula precio final con IVA con tarifa dinámica 15%", async () => {
    render(<PricingHarness baseSalePrice={null} />);

    const priceInput = getPriceInput();
    fireEvent.change(priceInput, { target: { value: "2.60" } });

    await waitFor(() => {
      expect(priceInput.value).toBe("2.60");
      expect(getMetricValue("Precio sin IVA que se guardará")).toContain(
        "2.60",
      );
      expect(getMetricValue("Precio final con IVA")).toContain("2.99");
      expect(screen.getByTestId("base-sale-price").textContent).toBe("2.6");
    });
  });

  it("cambiar entre Con IVA y Sin IVA no modifica el valor digitado", async () => {
    render(<PricingHarness baseSalePrice={null} />);

    const priceInput = getPriceInput();
    const modeSelect = getPriceModeSelect();

    fireEvent.change(priceInput, { target: { value: "2.99" } });
    fireEvent.change(modeSelect, { target: { value: "gross" } });
    fireEvent.change(modeSelect, { target: { value: "net" } });

    await waitFor(() => {
      expect(priceInput.value).toBe("2.99");
    });
  });

  it("cambiar la tarifa IVA venta no modifica el valor digitado", async () => {
    render(<PricingHarness baseSalePrice={null} />);

    const priceInput = getPriceInput();
    fireEvent.change(priceInput, { target: { value: "2.99" } });
    fireEvent.change(getPriceModeSelect(), { target: { value: "gross" } });
    fireEvent.change(screen.getByLabelText("External sale VAT"), {
      target: { value: "IVA0" },
    });

    await waitFor(() => {
      expect(priceInput.value).toBe("2.99");
      expect(getMetricValue("IVA calculado")).toContain("0.00");
      expect(screen.getByTestId("base-sale-price").textContent).toBe("2.99");
    });
  });

  it("con tarifa 0%, IVA calculado es 0 y precio sin IVA es el ingresado", async () => {
    render(<PricingHarness baseSalePrice={null} saleVatCode="IVA0" />);

    const priceInput = getPriceInput();
    fireEvent.change(priceInput, { target: { value: "2.99" } });
    fireEvent.change(getPriceModeSelect(), { target: { value: "gross" } });

    await waitFor(() => {
      expect(priceInput.value).toBe("2.99");
      expect(getMetricValue("Precio sin IVA que se guardará")).toContain(
        "2.99",
      );
      expect(getMetricValue("IVA calculado")).toContain("0.00");
      expect(getMetricValue("Precio final con IVA")).toContain("2.99");
    });
  });

  it("con tarifa exenta rate 0, IVA calculado es 0 y precio final es el ingresado", async () => {
    render(<PricingHarness baseSalePrice={null} saleVatCode="EXENTO" />);

    const priceInput = getPriceInput();
    fireEvent.change(priceInput, { target: { value: "2.99" } });
    fireEvent.change(getPriceModeSelect(), { target: { value: "gross" } });

    await waitFor(() => {
      expect(getMetricValue("Tarifa IVA venta")).toContain("Exento de IVA");
      expect(getMetricValue("IVA calculado")).toContain("0.00");
      expect(getMetricValue("Precio final con IVA")).toContain("2.99");
      expect(screen.getByTestId("base-sale-price").textContent).toBe("2.99");
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

    const pricingSection = getPricingSection();
    expect(pricingSection).toBeTruthy();

    const priceInputs = within(pricingSection as HTMLElement).getAllByLabelText(
      "Precio ingresado",
    );
    expect(priceInputs).toHaveLength(1);
    expect(
      (pricingSection as HTMLElement).querySelector(".items-metric-grid"),
    ).toBeTruthy();
  });

  it("no renderiza selector de IVA duplicado dentro del bloque de precio", () => {
    render(<PricingHarness baseSalePrice={100} />);

    const pricingSection = getPricingSection();

    expect(within(pricingSection).queryByLabelText("Tarifa IVA venta")).toBe(
      null,
    );
    expect(pricingSection.querySelectorAll("select")).toHaveLength(1);
  });

  it("renderiza métricas con label y value separados y sin texto montado", () => {
    render(<PricingHarness baseSalePrice={100} />);

    const vatMetric = getMetric("IVA calculado");
    const label = vatMetric.querySelector(".items-metric-card__label");
    const value = vatMetric.querySelector(".items-metric-card__value");

    expect(label).toBeTruthy();
    expect(value).toBeTruthy();
    expect(label).not.toBe(value);
    expect(vatMetric.textContent).not.toContain("IVA calculadoUSD");
  });

  it("no usa estilos inline en el bloque de precio", () => {
    render(<PricingHarness baseSalePrice={100} />);

    expect(getPricingSection().querySelectorAll("[style]")).toHaveLength(0);
  });

  it("no muestra el simulador viejo ni su botón de ejecución", () => {
    render(<PricingHarness baseSalePrice={100} itemId="item-1" />);

    expect(screen.queryByText(oldPvpLabel)).toBeNull();
    expect(screen.queryByRole("button", { name: oldSimulateLabel })).toBeNull();
    expect(screen.queryByText("Simulador de Precio")).toBeNull();
  });
});
