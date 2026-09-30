import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGetMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: vi.fn(),
  apiPut: vi.fn(),
}));

import { paymentMethodService } from "./paymentMethodService";
import { paymentMethodLookupFacade } from "../facades/paymentMethodLookupFacade";
import { salesService } from "./salesService";

const BASE = "/api/v1/payment-methods";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — GET /payment-methods tiene un solo cliente
 * (paymentMethodService); la venta y los subscribers (facade) lo reutilizan.
 */
describe("paymentMethodService — cliente único de /payment-methods", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
  });

  it("list(true) → mismo request que el antiguo salesService.listPaymentMethods()", async () => {
    apiGetMock.mockResolvedValue([]);
    await paymentMethodService.list(true);
    expect(apiGetMock).toHaveBeenCalledWith(`${BASE}?onlyActive=true`);
  });

  it("list() sin argumento conserva su default (todos, para la pantalla de configuración)", async () => {
    apiGetMock.mockResolvedValue([]);
    await paymentMethodService.list();
    expect(apiGetMock).toHaveBeenCalledWith(`${BASE}?onlyActive=false`);
  });

  it("la facade pública delega en el mismo service", () => {
    expect(paymentMethodLookupFacade.list).toBe(paymentMethodService.list);
  });

  it("salesService ya no tiene un segundo cliente de /payment-methods", () => {
    expect("listPaymentMethods" in salesService).toBe(false);
  });

  it("un error HTTP se propaga sin transformar", async () => {
    const error = new Error("500");
    apiGetMock.mockRejectedValue(error);
    await expect(paymentMethodService.list(true)).rejects.toBe(error);
  });
});
