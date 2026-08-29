// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { PriceListExceptionsTab } from "./PriceListExceptionsTab";
import {
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
  vi.mocked(priceListService.getAssignedItems).mockResolvedValue([ASSIGNED_ITEM]);
  vi.mocked(pricingRuleService.list).mockResolvedValue([RULE]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("PriceListExceptionsTab — eliminar excepción: sin window.confirm", () => {
  it("usa message.confirm en vez de window.confirm antes de eliminar", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    vi.mocked(pricingRuleService.remove).mockResolvedValue(true);

    render(<PriceListExceptionsTab priceList={PRICE_LIST} />);
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

    render(<PriceListExceptionsTab priceList={PRICE_LIST} />);
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByTitle("Eliminar excepción"));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(pricingRuleService.remove).not.toHaveBeenCalled();
  });
});
