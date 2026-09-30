import { describe, expect, it, vi } from "vitest";

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: vi.fn(),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
  apiPatch: vi.fn(),
}));

import { emissionPointsService } from "./emissionPointsService";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — el lookup de establecimientos es del owner establishments
 * (establishmentLookupFacade); emissionPoints ya no mantiene un segundo cliente del endpoint.
 */
describe("emissionPointsService", () => {
  it("no expone un cliente propio de /settings/establishments/lookups", () => {
    expect("establishmentLookups" in emissionPointsService).toBe(false);
  });
});
