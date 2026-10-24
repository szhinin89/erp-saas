// @vitest-environment jsdom
import { act, cleanup, renderHook } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { itemLookupFacade } from "../../items/facades/itemLookupFacade";
import { usePurchaseProductSearch } from "./usePurchaseProductSearch";

vi.mock("../../items/facades/itemLookupFacade", () => ({ itemLookupFacade: { search: vi.fn() } }));
afterEach(() => { cleanup(); vi.useRealTimers(); vi.clearAllMocks(); });
it("discards obsolete responses and retains the global search's filter on retry", async () => {
  vi.useFakeTimers();
  let resolveOld!: (value: never) => void;
  vi.mocked(itemLookupFacade.search).mockImplementationOnce(() => new Promise(resolve => { resolveOld = resolve; }))
    .mockRejectedValueOnce(new Error("network"))
    .mockResolvedValueOnce({ items: [], total: 0 } as never);
  const { result, rerender } = renderHook(({ query }) => usePurchaseProductSearch(query, true, 10, "type-1"), { initialProps: { query: "old" } });
  await act(async () => { await vi.advanceTimersByTimeAsync(300); });
  rerender({ query: "new" });
  expect(result.current.status).toBe("loading");
  await act(async () => { await vi.advanceTimersByTimeAsync(300); });
  expect(result.current.status).toBe("error");
  await act(async () => { resolveOld({ items: [{ id: "obsolete" }] } as never); });
  expect(result.current.status).toBe("error");
  expect(result.current.results).toEqual([]);
  act(() => result.current.retry());
  await act(async () => { await vi.advanceTimersByTimeAsync(300); });
  expect(result.current.status).toBe("ready");
  expect(itemLookupFacade.search).toHaveBeenLastCalledWith({ search: "new", itemTypeId: "type-1", isActive: true, pageSize: 10 });
});
