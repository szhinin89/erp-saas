import { beforeEach, describe, expect, it, vi } from "vitest";

const apiGetMock = vi.fn();

vi.mock("../../lib/apiEnvelope", () => ({
  apiGet: (...args: unknown[]) => apiGetMock(...args),
  apiPost: vi.fn(),
  apiPatch: vi.fn(),
}));

import { accountLookupFacade } from "./accountLookupFacade";
import { accountingApi } from "../api/accountingApi";

/**
 * ZH-FRONTEND-HTTP-CLIENT-SSOT-01 — GET /accounting/accounts tiene un solo cliente
 * (accountingApi); cashRegisters, finance, expenses y sales lo consumen por esta facade.
 */
describe("accountLookupFacade", () => {
  beforeEach(() => {
    apiGetMock.mockReset();
  });

  it("listAccounts → GET /api/v1/accounting/accounts (el mismo request que cashRegisters/finance hacían inline)", async () => {
    apiGetMock.mockResolvedValue([]);
    await accountLookupFacade.listAccounts();
    expect(apiGetMock).toHaveBeenCalledWith("/api/v1/accounting/accounts");
  });

  it("delega en accountingApi, sin wrapper", () => {
    expect(accountLookupFacade.listAccounts).toBe(accountingApi.listAccounts);
  });

  it("un error HTTP se propaga (los consumidores muestran la lista vacía)", async () => {
    const error = new Error("403");
    apiGetMock.mockRejectedValue(error);
    await expect(accountLookupFacade.listAccounts()).rejects.toBe(error);
  });
});
