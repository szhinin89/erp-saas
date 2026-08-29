// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { ItemsPage } from "./ItemsPage";
import { itemService } from "../api/itemService";
import { itemTypeService } from "../api/itemTypeService";
import { useItemUiStore } from "../store/itemUiStore";
import { invalidateItemTypeOptionsCache } from "../hooks/useItemTypeOptions";
import { message } from "../../../lib/messages";
import type { ItemDto } from "../../../types/items";

vi.mock("../../../i18n/i18n", () => ({
  useI18n: () => ({
    t: (_key: string, fallback?: string) => fallback ?? _key,
  }),
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    canShow: () => true,
  }),
}));

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
    enable: vi.fn().mockResolvedValue(true),
    disable: vi.fn().mockResolvedValue(true),
  },
}));

vi.mock("../api/itemTypeService", () => ({
  itemTypeService: {
    list: vi.fn().mockResolvedValue([
      {
        id: "type-physical",
        code: "PHYSICAL",
        name: "Físico",
        isActive: true,
        sortOrder: 1,
      },
      {
        id: "type-service",
        code: "SERVICE",
        name: "Servicio",
        isActive: true,
        sortOrder: 2,
      },
    ]),
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

const ACTIVE_ITEM: ItemDto = {
  id: "item-1",
  sku: "SKU-001",
  shortName: "Producto Uno",
  description: "Descripción de Producto Uno",
  itemTypeId: "type-physical",
  itemTypeName: "Físico",
  categoryNodeId: null,
  brandId: null,
  defaultUomCode: "UNI",
  defaultUomAbbrev: "u",
  isForSale: true,
  isFavorite: false,
  isEcommerceActive: false,
  tracksStock: true,
  tracksLot: false,
  tracksSeries: false,
  baseSalePrice: 10,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: null,
};

function openListTab() {
  useItemUiStore.setState({
    activeTab: "listado",
    editingItemId: null,
    readOnly: false,
    searchTerm: "",
    filterIsActive: undefined,
    filterItemTypeId: undefined,
  });
}

async function renderItemsPage() {
  openListTab();
  render(<ItemsPage />);
  await waitFor(() => {
    expect(itemService.getAll).toHaveBeenCalled();
  });
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

beforeEach(() => {
  invalidateItemTypeOptionsCache();
  openListTab();
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("ItemsPage listado", () => {
  it("seleccionar Inactivo y Activo recarga con isActive false/true", async () => {
    await renderItemsPage();

    const statusSelect = screen.getByDisplayValue("Todos los estados");

    fireEvent.change(statusSelect, { target: { value: "false" } });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ isActive: false }),
      );
    });

    fireEvent.change(statusSelect, { target: { value: "true" } });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ isActive: true }),
      );
    });
  });

  it("seleccionar Todos los estados recarga sin isActive", async () => {
    await renderItemsPage();

    const statusSelect = screen.getByDisplayValue("Todos los estados");

    fireEvent.change(statusSelect, { target: { value: "false" } });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ isActive: false }),
      );
    });

    fireEvent.change(statusSelect, { target: { value: "" } });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ isActive: undefined }),
      );
    });
  });

  it("buscar por SKU o nombre recarga con search", async () => {
    await renderItemsPage();

    fireEvent.change(screen.getByPlaceholderText("Buscar por SKU o nombre..."), {
      target: { value: "8431" },
    });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ search: "8431" }),
      );
    });
  });

  it("usa tipos dinámicos en ZhSelect y recarga con itemTypeId", async () => {
    await renderItemsPage();

    await waitFor(() => {
      expect(itemTypeService.list).toHaveBeenCalledWith(true);
    });

    const typeSelect = screen.getByDisplayValue("Todos los tipos");
    expect(typeSelect.className).toContain("zh-select");
    expect(screen.getByText("Físico")).toBeTruthy();
    expect(screen.getByText("Servicio")).toBeTruthy();

    fireEvent.change(typeSelect, { target: { value: "type-service" } });

    await waitFor(() => {
      expect(itemService.getAll).toHaveBeenLastCalledWith(
        expect.objectContaining({ itemTypeId: "type-service" }),
      );
    });
  });

  it("muestra empty state filtrado con i18n y no usa estilos inline", async () => {
    const { container } = render(<ItemsPage />);

    await waitFor(() => {
      expect(
        screen.getByText("Cambie la búsqueda, estado o tipo para ver más resultados."),
      ).toBeTruthy();
    });

    expect(container.querySelector("[style]")).toBeNull();
  });
});

describe("ItemsPage — deshabilitar/habilitar ítem: confirmación y feedback (CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05)", () => {
  beforeEach(() => {
    vi.mocked(itemService.getAll).mockResolvedValue({
      items: [ACTIVE_ITEM],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
  });

  it("pide confirmación antes de deshabilitar, aclarando que no borra histórico", async () => {
    vi.mocked(itemService.disable).mockResolvedValue(true);
    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() => {
      expect(message.confirm).toHaveBeenCalledTimes(1);
      expect(itemService.disable).toHaveBeenCalledWith("item-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/dejará de estar disponible/i);
    expect(String(options.message)).toMatch(/no se eliminan/i);
  });

  it("si se cancela, no llama a itemService.disable", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() => expect(message.confirm).toHaveBeenCalled());
    expect(itemService.disable).not.toHaveBeenCalled();
  });

  it("al deshabilitar exitosamente muestra message.success", async () => {
    vi.mocked(itemService.disable).mockResolvedValue(true);
    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Ítem deshabilitado."),
    );
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(itemService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El ítem tiene movimientos pendientes." } },
      },
    });
    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));

    await waitFor(() =>
      expect(message.error).toHaveBeenCalledWith("El ítem tiene movimientos pendientes."),
    );
    expect(message.success).not.toHaveBeenCalled();
  });

  it("habilitar explica que vuelve a estar disponible", async () => {
    vi.mocked(itemService.getAll).mockResolvedValue({
      items: [{ ...ACTIVE_ITEM, isActive: false }],
      totalCount: 1,
      pageNumber: 1,
      pageSize: 50,
    });
    vi.mocked(itemService.enable).mockResolvedValue(true);
    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());

    fireEvent.click(screen.getByRole("button", { name: "Habilitar" }));

    await waitFor(() => {
      expect(itemService.enable).toHaveBeenCalledWith("item-1");
    });
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("Ítem habilitado."),
    );
  });

  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(itemService.disable).mockResolvedValue(true);

    await renderItemsPage();
    await waitFor(() => expect(screen.getByText("Producto Uno")).toBeTruthy());
    fireEvent.click(screen.getByRole("button", { name: "Deshabilitar" }));
    await waitFor(() => expect(itemService.disable).toHaveBeenCalled());

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
