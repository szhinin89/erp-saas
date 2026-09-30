import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGetMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
}));

import { establishmentLookupFacade } from "./establishmentLookupFacade";
import { establishmentService } from "../api/establishmentService";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — GET /settings/establishments/lookups tiene un solo cliente
 * (establishmentService, owner) y una facade pública para emissionPoints.
 */
describe("establishmentLookupFacade", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
  });

  it("lookups → GET /api/v1/settings/establishments/lookups (mismo endpoint que usaba emissionPoints)", async () => {
    const rows = [{ id: "e1", code: "001", name: "Matriz" }];
    apiGetMock.mockResolvedValue(rows);
    await expect(establishmentLookupFacade.lookups()).resolves.toBe(rows);
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/settings/establishments/lookups");
  });

  it("delega en el service del owner, sin wrapper", () => {
    expect(establishmentLookupFacade.lookups).toBe(establishmentService.lookups);
  });

  it("un error HTTP se propaga (el hook de puntos de emisión decide mostrar vacío)", async () => {
    const error = new Error("403");
    apiGetMock.mockRejectedValue(error);
    await expect(establishmentLookupFacade.lookups()).rejects.toBe(error);
  });
});
