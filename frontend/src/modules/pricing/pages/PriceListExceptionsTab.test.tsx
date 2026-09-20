// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { PriceListExceptionsTab } from "./PriceListExceptionsTab";
import {
  pricingSimulationService,
  pricingRuleService,
  priceListService,
  type PriceListDto,
  type PriceListAssignedItemDto,
  type PricingRuleDto,
} from "../api/pricingService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: "Eliminar excepción"
 * usaba window.confirm (diálogo nativo prohibido). Se reemplaza por message.confirm y se agrega
 * message.success tras la eliminación real. No cambia el pricing engine ni el payload de
 * pricingRuleService.remove.
 */

vi.mock("../api/pricingService", async () => {
  const actual = await vi.importActual<typeof import("../api/pricingService")>(
    "../api/pricingService",
  );
  return {
    ...actual,
    pricingSimulationService: { simulate: vi.fn() },
    priceListService: {
      getAssignedItems: vi.fn(),
    },
    pricingRuleService: {
      list: vi.fn(),
      set: vi.fn(),
      enable: vi.fn(),
      remove: vi.fn(),
    },
  };
});

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const PRICE_LIST: PriceListDto = {
  id: "pl-1",
  code: "DEFAULT",
  name: "Lista general",
  currencyCode: "USD",
  isDefault: true,
  validFrom: null,
  validUntil: null,
  ruleType: null,
  ruleValue: null,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

const ASSIGNED_ITEM: PriceListAssignedItemDto = {
  itemId: "item-1",
  sku: "SKU-1",
  itemName: "Producto Uno",
  baseSalePrice: 10,
};

const RULE: PricingRuleDto = {
  id: "rule-1",
  priceListId: "pl-1",
  itemId: "item-1",
  ruleType: "PercentDiscount",
  ruleValue: 10,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
  lastModifiedAt: null,
  lastModifiedByName: null,
};

afterEach(() => cleanup());

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(pricingSimulationService.simulate).mockResolvedValue([{ priceListId: "pl-1", netPrice: 9, ruleSummary: { source: "Exception", type: "PercentDiscount", value: 10, description: "" } }]);
  vi.mocked(priceListService.getAssignedItems).mockResolvedValue([ASSIGNED_ITEM]);
  vi.mocked(pricingRuleService.list).mockResolvedValue([RULE]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PriceListExceptionsTab — eliminar excepción: sin window.confirm", () => {
  it("usa message.confirm en vez de window.confirm antes de eliminar", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(pricingRuleService.remove).mockResolvedValue(true);

    render(
      <I18nProvider>
        <PriceListExceptionsTab priceList={PRICE_LIST} />
      </I18nProvider>,
    );
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Eliminar excepción"));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(pricingRuleService.remove).toHaveBeenCalledWith("rule-1");
    });
    expect(confirmSpy).not.toHaveBeenCalled();
    expect(message.success).toHaveBeenCalledWith("Excepción eliminada correctamente.");

    confirmSpy.mockRestore();
  });

  it("si se cancela, no llama a pricingRuleService.remove", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);

    render(
      <I18nProvider>
        <PriceListExceptionsTab priceList={PRICE_LIST} />
      </I18nProvider>,
    );
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Eliminar excepción"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(pricingRuleService.remove).not.toHaveBeenCalled();
  });
});


describe("PRICING-LIST-UX-01", () => {
  const show = (mode: "products" | "exceptions" = "exceptions") => render(
    <I18nProvider><PriceListExceptionsTab priceList={PRICE_LIST} mode={mode} /></I18nProvider>,
  );

  it("blocks exceptions for an empty list", async () => {
    vi.mocked(priceListService.getAssignedItems).mockResolvedValue([]);
    show();
    await waitFor(() => expect(screen.getAllByText("Primero asigna productos a esta lista.").length).toBeGreaterThan(0));
    expect((screen.getByRole("button", { name: /Nueva excepción/ }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("offers only assigned products, never the global catalog or orphan rules", async () => {
    vi.mocked(pricingRuleService.list).mockResolvedValue([RULE, { ...RULE, id: "orphan", itemId: "not-assigned" }]);
    show();
    await screen.findByText("Producto Uno");
    fireEvent.click(screen.getByRole("button", { name: /Nueva excepción/ }));
    const selector = screen.getByLabelText("Producto de la lista") as HTMLSelectElement;
    expect(Array.from(selector.options).map((option) => option.value)).toEqual(["", "item-1"]);
    expect(screen.queryByPlaceholderText("Buscar por SKU o nombre...")).toBeNull();
  });

  it("saves a valid exception and displays the server-calculated preview", async () => {
    vi.mocked(pricingRuleService.set).mockResolvedValue({ status: "Created", pricingRuleId: "rule-1", existingRuleType: null, existingRuleValue: null });
    show();
    await screen.findByText("Producto Uno");
    fireEvent.click(screen.getByTitle("Editar excepción"));
    await waitFor(() => expect(pricingSimulationService.simulate).toHaveBeenCalledWith("item-1", {
      priceListId: "pl-1", itemId: "item-1", ruleType: "PercentDiscount", ruleValue: 10,
    }));
    expect(screen.getAllByText(/USD 9/).length).toBeGreaterThan(0);
    fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));
    await waitFor(() => expect(pricingRuleService.set).toHaveBeenCalledWith({ priceListId: "pl-1", itemId: "item-1", ruleType: "PercentDiscount", ruleValue: 10 }));
    await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
  });

  it("shows a business validation message for 422, not Axios status text", async () => {
    vi.mocked(pricingRuleService.set).mockRejectedValue({ isAxiosError: true, message: "Request failed with status code 422", response: { status: 422, data: { message: { user: "Validación fallida" }, data: { errors: ["El ítem no está asignado a esta lista."] } } } });
    show();
    await screen.findByText("Producto Uno");
    fireEvent.click(screen.getByTitle("Editar excepción"));
    fireEvent.click(screen.getByRole("button", { name: /Guardar/ }));
    expect(await screen.findByText("El ítem no está asignado a esta lista.")).toBeTruthy();
    expect(screen.queryByText(/Request failed/)).toBeNull();
  });

  it("renders calculated prices from the backend without applying rules again", async () => {
    vi.mocked(pricingSimulationService.simulate).mockResolvedValue([{ priceListId: "pl-1", netPrice: 7.123456, ruleSummary: { source: "Exception", type: "FixedPrice", value: 7.123456, description: "" } }]);
    show("products");
    expect(await screen.findByText(/USD 7/)).toBeTruthy();
    expect(screen.getByText("Precio calculado")).toBeTruthy();
  });
});
