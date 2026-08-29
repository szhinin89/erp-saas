// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, renderHook, waitFor } from "@testing-library/react";
import { useItems } from "./useItems";
import { itemService } from "../api/itemService";
import { message } from "../../../lib/messages";

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

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    confirm: vi.fn(),
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

  describe("toggleStatus — feedback (CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05)", () => {
    it("éxito: refetch, devuelve true, no llama message.error", async () => {
      vi.mocked(itemService.disable).mockResolvedValue(true);
      const { result } = renderHook(() => useItems());
      await waitFor(() => expect(itemService.getAll).toHaveBeenCalled());
      vi.mocked(itemService.getAll).mockClear();

      let ok: boolean | undefined;
      await act(async () => {
        ok = await result.current.toggleStatus("item-1", false);
      });

      expect(ok).toBe(true);
      expect(itemService.disable).toHaveBeenCalledWith("item-1");
      expect(message.error).not.toHaveBeenCalled();
    });

    it("fallo: no deja catch vacío — setea toggleError y llama message.error con el mensaje real", async () => {
      vi.mocked(itemService.disable).mockRejectedValue({
        isAxiosError: true,
        response: {
          status: 409,
          data: { message: { user: "El ítem está referenciado en una compra activa." } },
        },
      });
      const { result } = renderHook(() => useItems());
      await waitFor(() => expect(itemService.getAll).toHaveBeenCalled());

      let ok: boolean | undefined;
      await act(async () => {
        ok = await result.current.toggleStatus("item-1", false);
      });

      expect(ok).toBe(false);
      expect(message.error).toHaveBeenCalledWith(
        "El ítem está referenciado en una compra activa.",
      );
    });
  });
});
