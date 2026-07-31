import { describe, it, expect, vi, beforeEach } from "vitest";

const apiGetMock = vi.fn();
const apiPostMock = vi.fn();
const apiPutMock = vi.fn();
const apiDeleteMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: (...args: unknown[]) => apiPostMock(...args),
  apiPut: (...args: unknown[]) => apiPutMock(...args),
  apiDelete: (...args: unknown[]) => apiDeleteMock(...args),
}));

import { salesReturnService } from "./salesReturnService";

describe("salesReturnService", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
    apiPostMock.mockReset();
    apiPutMock.mockReset();
    apiDeleteMock.mockReset();
  });

  it("list calls GET /api/v1/sales/returns with search/status/pagination params", async () => {
    apiGetMock.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });

    await salesReturnService.list("DEV-001", "Draft", 2, 10);

    expect(apiGetMock).toHaveBeenCalledWith(
      "/api/v1/sales/returns?search=DEV-001&status=Draft&pageNumber=2&pageSize=10",
    );
  });

  it("list omits empty search/status filters", async () => {
    apiGetMock.mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });

    await salesReturnService.list();

    expect(apiGetMock).toHaveBeenCalledWith(
      "/api/v1/sales/returns?pageNumber=1&pageSize=25",
    );
  });

  it("getById calls GET /api/v1/sales/returns/{id}", async () => {
    apiGetMock.mockResolvedValue({ id: "r1" });

    await salesReturnService.getById("r1");

    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/sales/returns/r1");
  });

  it("getReturnableLines calls GET /api/v1/sales/invoices/{invoiceId}/returnable-lines", async () => {
    apiGetMock.mockResolvedValue([]);

    await salesReturnService.getReturnableLines("inv-1");

    expect(apiGetMock).toHaveBeenCalledWith(
      "/api/v1/sales/invoices/inv-1/returnable-lines",
    );
  });

  it("createDraft calls POST /api/v1/sales/returns with the payload", async () => {
    apiPostMock.mockResolvedValue({ id: "r1" });
    const payload = {
      salesInvoiceId: "inv-1",
      reason: "Producto dañado",
      lines: [{ invoiceDetailId: "line-1", quantity: 1 }],
    };

    await salesReturnService.createDraft(payload);

    expect(apiPostMock).toHaveBeenCalledWith("/api/v1/sales/returns", payload);
  });

  it("updateDraft calls PUT /api/v1/sales/returns/{id} with the payload", async () => {
    apiPutMock.mockResolvedValue({ id: "r1" });
    const payload = { id: "r1", lines: [{ invoiceDetailId: "line-1", quantity: 2 }] };

    await salesReturnService.updateDraft("r1", payload);

    expect(apiPutMock).toHaveBeenCalledWith("/api/v1/sales/returns/r1", payload);
  });

  it("cancelDraft calls DELETE /api/v1/sales/returns/{id}", async () => {
    apiDeleteMock.mockResolvedValue({ id: "r1", status: "Cancelled" });

    await salesReturnService.cancelDraft("r1");

    expect(apiDeleteMock).toHaveBeenCalledWith("/api/v1/sales/returns/r1");
  });

  it("authorize calls POST /api/v1/sales/returns/{id}/authorize with refund allocations", async () => {
    apiPostMock.mockResolvedValue({ id: "r1", status: "Authorized" });
    const payload = { refundAllocations: [{ method: "Cash" as const, amount: 23 }] };

    await salesReturnService.authorize("r1", payload);

    expect(apiPostMock).toHaveBeenCalledWith(
      "/api/v1/sales/returns/r1/authorize",
      payload,
    );
  });
});
