// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { apiGet } from "../../lib/apiEnvelope";
import { salesItemPricingService } from "./salesItemPricingService";

/**
 * SALES-CONTEXTUAL-PRICING-READ-06A — cubre exclusivamente el wiring HTTP (URL/query param):
 * Sales solo informa el customerId actualmente seleccionado, nunca decide qué lista de precios
 * corresponde — esa resolución (Customer → CompanyDefault → PVP) vive enteramente en
 * IPricingResolver, ya cubierto en el backend (GetSalesItemPricingQueryHandlerTests).
 */

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
}));

describe("salesItemPricingService", () => {
  it("sin customerId, llama GET .../pricing sin params", () => {
    salesItemPricingService.get("item-1");
    expect(apiGet).toHaveBeenCalledWith("/api/v1/sales/items/item-1/pricing", {
      params: undefined,
    });
  });

  it("con customerId, lo envía como query param", () => {
    salesItemPricingService.get("item-1", "cust-1");
    expect(apiGet).toHaveBeenCalledWith("/api/v1/sales/items/item-1/pricing", {
      params: { customerId: "cust-1" },
    });
  });
});
