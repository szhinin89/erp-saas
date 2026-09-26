import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGet = vi.fn();
vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...a: unknown[]) => apiGet(...a),
  apiPost: vi.fn(),
}));

import { supplierCreditService } from "./supplierCreditService";

/** ZH-SUPPLIER-CREDIT-READ-MODEL-02D-D — filtros server-side del listado de saldos a favor. */
describe("supplierCreditService.list", () => {
  beforeEach(() => apiGet.mockReset().mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 }));

  it("sin filtros solo envía paginación", async () => {
    await supplierCreditService.list(2, 10);
    expect(apiGet).toHaveBeenCalledWith("/api/v1/finance/supplier-credits?page=2&pageSize=10");
  });

  it("envía supplierId, sourceType e isOpen (incluido isOpen=false)", async () => {
    await supplierCreditService.list(1, 25, { supplierId: "sup-1", sourceType: "SupplierPayment", isOpen: false });
    const url = new URL(apiGet.mock.calls[0]![0] as string, "http://x");
    expect(url.searchParams.get("supplierId")).toBe("sup-1");
    expect(url.searchParams.get("sourceType")).toBe("SupplierPayment");
    expect(url.searchParams.get("isOpen")).toBe("false");
  });
});
