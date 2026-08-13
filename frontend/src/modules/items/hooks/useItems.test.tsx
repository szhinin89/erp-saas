// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, renderHook, waitFor } from "@testing-library/react";
import { useItems } from "./useItems";
import { itemService } from "../api/itemService";

vi.mock("../api/itemService", () => ({
  itemService: {
    getAll: vi.fn().mockResolvedValue({
      items: [],
      totalCount: 0,
      pageNumber: 1,
      pageSize: 50,
    }),
    create: vi.fn(),
    update: vi.fn(),
    enable: vi.fn(),
    disable: vi.fn(),
  },
}));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("useItems", () => {
  it("reejecuta itemService.getAll cuando cambia isActive", async () => {
    const { rerender } = renderHook(({ isActive }) => useItems({ isActive }), {
      initialProps: { isActive: true as boolean | undefined },
    });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenCalledWith({ isActive: true });
    });

    rerender({ isActive: false });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith({
        isActive: false,
      });
    });
  });

  it("reejecuta itemService.getAll cuando cambia itemTypeId o search", async () => {
    const { rerender } = renderHook(
      ({ itemTypeId, search }) => useItems({ itemTypeId, search }),
      {
        initialProps: {
          itemTypeId: undefined as string | undefined,
          search: undefined as string | undefined,
        },
      },
    );

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenCalledWith({
        itemTypeId: undefined,
        search: undefined,
      });
    });

    rerender({ itemTypeId: "type-1", search: undefined });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith({
        itemTypeId: "type-1",
        search: undefined,
      });
    });

    rerender({ itemTypeId: "type-1", search: "8431" });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith({
        itemTypeId: "type-1",
        search: "8431",
      });
    });
  });
});
