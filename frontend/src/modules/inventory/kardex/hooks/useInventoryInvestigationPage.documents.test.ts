// @vitest-environment jsdom
import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { useInventoryInvestigationPage } from "./useInventoryInvestigationPage";

const api = vi.hoisted(() => ({ list: vi.fn(), sales: vi.fn(), getByDocument: vi.fn() }));
vi.mock("../../../purchases/facades/purchaseLookupFacade", () => ({ purchaseLookupFacade: { list: api.list } }));
vi.mock("../../../sales/facades/salesLookupFacade", () => ({ salesLookupFacade: { list: api.sales } }));
vi.mock("../../stock/api/kardexService", () => ({ kardexService: { getByDocument: api.getByDocument } }));
vi.mock("../../../items/facades/itemPickerFacade", () => ({
  useItemLookupSearch: () => ({ query: "", results: [], loading: false, setQuery: vi.fn(), reset: vi.fn() }),
}));

afterEach(() => { cleanup(); vi.clearAllMocks(); });

it.each(["000005545", "001-001-000005545"])("busca %s y consulta cada documento duplicado por su propio ID", async query => {
  api.list.mockImplementation(async (_query, status) => ({ items: [
    { id: "confirmed-id", invoiceNumber: "001-001-000005545", status: "Confirmed" },
    { id: "cancelled-id", invoiceNumber: "001-001-000005545", status: "Cancelled" },
  ].filter(i => i.status === status) }));
  api.getByDocument.mockResolvedValue([]);
  const { result } = renderHook(() => useInventoryInvestigationPage());
  act(() => {
    result.current.setSearchMode("document");
    result.current.setDocNumberQuery(query);
  });
  await act(async () => { await result.current.searchDocuments(); });
  expect(api.list).toHaveBeenCalledWith(query, "Confirmed", 1, 10);
  expect(api.list).toHaveBeenCalledWith(query, "Cancelled", 1, 10);
  expect(api.list).toHaveBeenCalledTimes(2);
  expect(api.getByDocument).not.toHaveBeenCalled();
  const matches = result.current.docMatches;
  expect(matches.map(d => [d.id, d.status])).toEqual([
    ["confirmed-id", "Confirmed"], ["cancelled-id", "Cancelled"],
  ]);
  for (const doc of matches) {
    act(() => { result.current.selectDocument(doc); });
    expect(result.current.selectedDoc).toEqual(doc);
    expect(result.current.docMatches).toEqual([]);
    await act(async () => { await result.current.runSearch(); });
    expect(api.getByDocument).toHaveBeenLastCalledWith(doc.id, "PurchaseInvoice");
  }
});

it("ventas consulta estados reales sin b?squeda autom?tica ni Kardex", async () => {
  api.sales.mockImplementation(async (_query, status) => ({ items: [{ id: status, invoiceNumber: "123", status, customerName: "Cliente", issueDate: "2026-10-29" }] }));
  const { result } = renderHook(() => useInventoryInvestigationPage());
  act(() => { result.current.setDocSubType("SalesInvoice"); result.current.setDocNumberQuery("123"); });
  expect(api.sales).not.toHaveBeenCalled();
  await act(async () => { await result.current.searchDocuments(); });
  expect(api.sales.mock.calls).toEqual([["123", "Authorized", 1, 10], ["123", "Cancelled", 1, 10]]);
  expect(result.current.docMatches[0]).toMatchObject({ partnerName: "Cliente", issueDate: "2026-10-29" });
  expect(api.getByDocument).not.toHaveBeenCalled();
});
it("descarta respuestas anteriores a la ?ltima b?squeda", async () => {
  let release!: (value: { items: unknown[] }) => void;
  const old = new Promise<{ items: unknown[] }>(resolve => { release = resolve; });
  api.list.mockImplementation((query, status) => query === "vieja" ? old : Promise.resolve({ items: [{ id: status, invoiceNumber: "nueva", status }] }));
  const { result } = renderHook(() => useInventoryInvestigationPage());
  act(() => { result.current.setDocNumberQuery("vieja"); });
  let pending!: Promise<void>;
  act(() => { pending = result.current.searchDocuments(); });
  act(() => { result.current.setDocNumberQuery("nueva"); });
  await act(async () => { await result.current.searchDocuments(); });
  await act(async () => { release({ items: [] }); await pending; });
  expect(result.current.docMatches.map(i => i.number)).toEqual(["nueva", "nueva"]);
  expect(result.current.docSearching).toBe(false);
});
