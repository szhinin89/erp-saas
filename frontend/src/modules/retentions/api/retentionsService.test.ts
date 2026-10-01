// @vitest-environment jsdom
import { describe, it, expect, vi } from "vitest";
import { apiGet, apiPost } from "../../lib/apiEnvelope";
import { api } from "../../lib/api";
import { retentionsService } from "./retentionsService";

/**
 * PURCHASES-RETENTIONS-UI-MIGRATION-05C — cliente transversal de Retentions consumido desde
 * Compras. Cubre exclusivamente el wiring HTTP (URL/payload). ZH-PURCHASE-RETENTION-CONFIRM-01:
 * la emisión ya no tiene endpoint propio (viaja en la confirmación de la compra).
 */

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
}));

vi.mock("../../lib/api", () => ({
  api: { get: vi.fn() },
}));

describe("retentionsService", () => {
  it("getForPurchase llama GET /api/v1/purchases/{id}/retention", () => {
    retentionsService.getForPurchase("purchase-1");
    expect(apiGet).toHaveBeenCalledWith("/api/v1/purchases/purchase-1/retention");
  });

  it("no expone una emisión posterior de retención (se emite al confirmar la compra)", () => {
    expect(retentionsService).not.toHaveProperty("issueForPurchase");
  });

  it("no expone anulación aislada de la retención (solo se anula anulando la compra)", () => {
    expect(retentionsService).not.toHaveProperty("cancelForPurchase");
  });

  it("registerElectronic llama POST /api/v1/retentions/{id}/electronic/register", () => {
    retentionsService.registerElectronic("ret-1");
    expect(apiPost).toHaveBeenCalledWith("/api/v1/retentions/ret-1/electronic/register", {});
  });

  it("getElectronicXmlBlob llama GET /api/v1/retentions/{id}/electronic/xml como blob", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: new Blob(["<xml/>"]) });

    await retentionsService.getElectronicXmlBlob("ret-1");

    expect(api.get).toHaveBeenCalledWith("/api/v1/retentions/ret-1/electronic/xml", {
      responseType: "blob",
    });
  });

  it("getRidePdfBlob llama GET /api/v1/retentions/{id}/ride/pdf como blob", async () => {
    vi.mocked(api.get).mockResolvedValue({ data: new Blob(["%PDF"]) });

    await retentionsService.getRidePdfBlob("ret-1");

    expect(api.get).toHaveBeenCalledWith("/api/v1/retentions/ret-1/ride/pdf", {
      responseType: "blob",
    });
  });
});
