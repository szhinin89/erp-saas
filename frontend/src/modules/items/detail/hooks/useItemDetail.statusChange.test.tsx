// @vitest-environment jsdom
import React from "react";
import { act, cleanup, fireEvent, render, renderHook, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useItemDetailPage } from "./useItemDetail";
import { itemService } from "../../api/itemService";
import { message } from "../../../../lib/messages";
import { ImagesSection } from "../components/CollectionSection";
import { I18nProvider } from "../../../../i18n/i18n";
import type { ItemDetailDto, ItemImageDto, ItemVariantDto } from "../../../../types/items";

/**
 * FUNC-DEACTIVATION-CONFIRM-01 (AA-D5) — Variantes e imágenes del ítem: desactivar confirma,
 * cancelar no llama al backend, una sola petición, error visible, estado pendiente expuesto,
 * activar variante sin confirmación y acción bloqueada cuando la sección está deshabilitada
 * (sin permiso de edición / modo solo lectura).
 */

vi.mock("../../api/itemService", () => ({
  itemService: {
    getById: vi.fn(),
    enableVariant: vi.fn(),
    disableVariant: vi.fn(),
    disableImage: vi.fn(),
  },
}));

vi.mock("../../../../lib/messages", () => ({
  message: { confirm: vi.fn(), success: vi.fn(), error: vi.fn() },
}));

const ACTIVE_VARIANT = {
  id: "var-1",
  sku: "SKU-V1",
  name: "Variante Ficticia",
  isDefault: false,
  sortOrder: 1,
  isActive: true,
  attributes: [],
  barcodes: [],
} satisfies ItemVariantDto;

const INACTIVE_VARIANT = { ...ACTIVE_VARIANT, id: "var-2", sku: "SKU-V2", isActive: false };

const IMAGE = {
  id: "img-1",
  variantId: null,
  storageObjectId: "storage-object-ficticio",
  altText: "Imagen ficticia",
  isMain: true,
  isEcommerce: false,
  sortOrder: 1,
  isActive: true,
} satisfies ItemImageDto;

const ITEM = {
  id: "item-1",
  variants: [ACTIVE_VARIANT, INACTIVE_VARIANT],
  images: [IMAGE],
} as unknown as ItemDetailDto;

const service = vi.mocked(itemService);

async function renderReady() {
  const hook = renderHook(() => useItemDetailPage("item-1"), {
    wrapper: ({ children }: { children: React.ReactNode }) =>
      React.createElement(I18nProvider, null, children),
  });
  await waitFor(() => expect(hook.result.current.item).toEqual(ITEM));
  return hook;
}

beforeEach(() => {
  vi.clearAllMocks();
  service.getById.mockResolvedValue(ITEM);
  service.disableVariant.mockResolvedValue(true as never);
  service.enableVariant.mockResolvedValue(true as never);
  service.disableImage.mockResolvedValue(true as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

afterEach(cleanup);

describe("useItemDetailPage — desactivar variante", () => {
  it("cancelar no envía petición al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleVariant("var-1", false);
    });
    expect(vi.mocked(message.confirm).mock.calls[0][0]).toMatchObject({
      title: 'Desactivar variante "SKU-V1 — Variante Ficticia"',
      confirmLabel: "Desactivar",
      cancelLabel: "Cancelar",
    });
    expect(service.disableVariant).not.toHaveBeenCalled();
  });

  it("confirmar envía una única petición aunque se pulse dos veces", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await Promise.all([
        result.current.toggleVariant("var-1", false),
        result.current.toggleVariant("var-1", false),
      ]);
    });
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(service.disableVariant).toHaveBeenCalledTimes(1);
    expect(service.disableVariant).toHaveBeenCalledWith("item-1", "var-1");
  });

  it("muestra el error del backend y libera el estado pendiente", async () => {
    service.disableVariant.mockRejectedValue(new Error("boom"));
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleVariant("var-1", false);
    });
    expect(result.current.error).toBe("Error al cambiar estado de la variante.");
    expect(result.current.statusChangePending).toBe(false);
  });

  it("activar conserva el flujo directo sin confirmación", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleVariant("var-2", true);
    });
    expect(message.confirm).not.toHaveBeenCalled();
    expect(service.enableVariant).toHaveBeenCalledWith("item-1", "var-2");
  });
});

describe("useItemDetailPage — desactivar imagen", () => {
  it("cancelar no envía petición; el aviso identifica la imagen principal y la irreversibilidad en UI", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = await renderReady();
    await act(async () => {
      await result.current.disableImage("img-1");
    });
    const opts = vi.mocked(message.confirm).mock.calls[0][0];
    expect(opts.title).toBe('Desactivar imagen "Imagen ficticia"');
    expect(String(opts.message)).toContain("imagen principal");
    expect(String(opts.message)).toContain("no ofrece una acción para reactivarla");
    expect(service.disableImage).not.toHaveBeenCalled();
  });

  it("expone el estado pendiente y envía una única petición", async () => {
    let resolve: (v: boolean) => void = () => {};
    service.disableImage.mockReturnValue(new Promise<boolean>((r) => (resolve = r)) as never);
    const { result } = await renderReady();
    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.disableImage("img-1");
    });
    await waitFor(() => expect(result.current.statusChangePending).toBe(true));
    await act(async () => {
      void result.current.disableImage("img-1");
    });
    await act(async () => {
      resolve(true);
      await pending;
    });
    expect(service.disableImage).toHaveBeenCalledTimes(1);
    expect(result.current.statusChangePending).toBe(false);
  });

  it("muestra el error del backend", async () => {
    service.disableImage.mockRejectedValue(new Error("boom"));
    const { result } = await renderReady();
    await act(async () => {
      await result.current.disableImage("img-1");
    });
    expect(result.current.error).toBe("Error al desactivar la imagen.");
  });
});

describe("ImagesSection — sección deshabilitada (sin permiso / solo lectura)", () => {
  it("el botón Desactivar queda deshabilitado y no invoca la acción", () => {
    const onDisable = vi.fn().mockResolvedValue(undefined);
    render(
      <ImagesSection
        t={(_k: string, fallback?: string) => fallback ?? _k}
        images={[IMAGE]}
        disabled
        onDisable={onDisable}
      />,
    );
    const btn = screen.getByTitle("Desactivar");
    expect(btn).toHaveProperty("disabled", true);
    fireEvent.click(btn);
    expect(onDisable).not.toHaveBeenCalled();
  });
});
