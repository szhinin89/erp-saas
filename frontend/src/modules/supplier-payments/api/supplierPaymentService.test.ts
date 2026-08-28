import { describe, it, expect, vi, beforeEach } from "vitest";

const apiGetMock = vi.fn();
const apiPostMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: (...args: unknown[]) => apiPostMock(...args),
}));

import { supplierPaymentService } from "./supplierPaymentService";

describe("supplierPaymentService", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
    apiPostMock.mockReset();
  });

  it("reverse calls POST /api/v1/supplier-payments/{id}/reverse with { reason }", async () => {
    apiPostMock.mockResolvedValue({ id: "sp-1", status: "Reversed" });

    await supplierPaymentService.reverse("sp-1", "Error de digitación");

    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/supplier-payments/sp-1/reverse", {
      reason: "Error de digitación",
    });
  });

  it("getById calls GET /api/v1/supplier-payments/{id}", async () => {
    apiGetMock.mockResolvedValue({ id: "sp-1" });

    await supplierPaymentService.getById("sp-1");

    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/supplier-payments/sp-1");
  });

  it("register calls POST /api/v1/supplier-payments with the payload", async () => {
    apiPostMock.mockResolvedValue({ id: "sp-1" });
    const payload = {
      supplierId: "sup-1",
      paymentDate: "2026-08-28",
      totalAmount: 300,
      methodLines: [],
      applicationLines: [],
      allocations: [],
    };

    await supplierPaymentService.register(payload);

    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/supplier-payments", payload);
  });
});
