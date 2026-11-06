import { beforeEach, expect, it, vi } from "vitest";
import { itemService } from "./itemService";
import { apiPatch } from "../../lib/apiEnvelope";
import es from "../../../i18n/locales/es.json";
import en from "../../../i18n/locales/en.json";

vi.mock("../../lib/apiEnvelope", () => ({ apiPatch: vi.fn(), apiGet: vi.fn(), apiPost: vi.fn(), apiPut: vi.fn() }));
beforeEach(() => vi.clearAllMocks());

it("uses the published enable endpoint without body and preserves its response", async () => {
  const result = { id: "image", isActive: true, isMain: false };
  vi.mocked(apiPatch).mockResolvedValue(result);
  expect(await itemService.enableImage("item", "image")).toBe(result);
  expect(apiPatch).toHaveBeenCalledExactlyOnceWith("/api/v1/items/item/images/image/enable");
});

it("keeps the existing disable endpoint", async () => {
  await itemService.disableImage("item", "image");
  expect(apiPatch).toHaveBeenCalledExactlyOnceWith("/api/v1/items/item/images/image/disable");
});

it("provides every new image key in Spanish and English", () => {
  const keys = ["filter.label", "filter.active", "filter.inactive", "filter.all", "col.status",
    "status.active", "status.inactive", "activate", "activate.hint", "activate.error",
    "activate.refreshFailed", "activate.refreshError"];
  for (const key of keys) {
    const full = `items.images.${key}`;
    expect((es as Record<string, string>)[full]).toBeTruthy();
    expect((en as Record<string, string>)[full]).toBeTruthy();
    expect((en as Record<string, string>)[full]).not.toBe((es as Record<string, string>)[full]);
  }
});
