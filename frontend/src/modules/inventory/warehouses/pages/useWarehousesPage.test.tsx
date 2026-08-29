// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useWarehousesPage } from "./useWarehousesPage";
import { warehouseService, type WarehouseDto } from "../api/warehouseService";
import { branchService } from "../../../branches/api/branchService";
import { message } from "../../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-CLEANUP-07 — residuo encontrado en el barrido: la página llamaba
 * message.info(...) incondicionalmente después de `await page.toggleStatus(row)`, pero el hook
 * atrapaba el error de la API sin relanzarlo — el caller nunca se enteraba de un fallo real y
 * mostraba éxito de todas formas (falso éxito). Se corrige moviendo el feedback dentro del propio
 * `toggleStatus`, que ya solo se ejecuta después de confirmar en `WarehouseListTab` (ZHConfirmModal
 * existente) — por eso el hook no agrega una segunda confirmación.
 */

vi.mock("../api/warehouseService", () => ({
  warehouseService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
  },
}));

vi.mock("../../../branches/api/branchService", () => ({
  branchService: { list: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_WAREHOUSE: WarehouseDto = {
  id: "wh-1",
  branchId: "branch-1",
  name: "Bodega Norte",
  code: "N01",
  storageType: null,
  address: null,
  phone: null,
  email: null,
  manager: null,
  latitude: null,
  longitude: null,
  capacity: null,
  dailyDispatchGoal: null,
  isActive: true,
};

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(warehouseService.list).mockResolvedValue([ACTIVE_WAREHOUSE]);
  vi.mocked(branchService.list).mockResolvedValue([]);
});

describe("useWarehousesPage — toggleStatus: sin falso éxito", () => {
  it("al desactivar exitosamente llama a la API y muestra message.success", async () => {
    vi.mocked(warehouseService.disable).mockResolvedValue(ACTIVE_WAREHOUSE);
    const { result } = renderHook(() => useWarehousesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_WAREHOUSE]));

    await act(async () => {
      await result.current.toggleStatus(ACTIVE_WAREHOUSE);
    });

    expect(warehouseService.disable).toHaveBeenCalledWith("wh-1");
    expect(message.success).toHaveBeenCalledWith("Bodega desactivada.");
  });

  it("si falla, NO muestra éxito y expone el mensaje real del backend (antes: falso éxito)", async () => {
    vi.mocked(warehouseService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La bodega tiene stock disponible." } },
      },
    });
    const { result } = renderHook(() => useWarehousesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_WAREHOUSE]));

    await act(async () => {
      await result.current.toggleStatus(ACTIVE_WAREHOUSE);
    });

    expect(message.error).toHaveBeenCalledWith("La bodega tiene stock disponible.");
    expect(message.success).not.toHaveBeenCalled();
  });
});
