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
