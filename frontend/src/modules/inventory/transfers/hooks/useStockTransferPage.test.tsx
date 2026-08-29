// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act, render, screen } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useStockTransferPage } from "./useStockTransferPage";
import { warehouseService } from "../../warehouses/api/warehouseService";
import type { WarehouseDto } from "../../warehouses/api/warehouseService";
import { branchLookupFacade } from "../../../branches/facades/branchLookupFacade";
import { stockService } from "../../stock/api/stockService";
import {
  stockTransferService,
  type StockTransferDto,
} from "../api/stockTransferService";
import { message } from "../../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-INVENTORY-ACCOUNTING-05 — "Confirmar transferencia entre bodegas":
 * confirma antes de ejecutar con resumen (n.º, origen, destino, líneas, total unidades, estado),
 * no llama al backend si se cancela, éxito muestra message.success, fallo muestra el mensaje real
 * vía formatApiRequestError. No cambia payload/cálculos de stockTransferService.create/confirm.
 */

vi.mock("../../warehouses/api/warehouseService", () => ({
  warehouseService: { list: vi.fn() },
}));

vi.mock("../../../branches/facades/branchLookupFacade", () => ({
  branchLookupFacade: { list: vi.fn() },
}));

vi.mock("../../stock/api/stockService", () => ({
  stockService: { getWarehouseAvailability: vi.fn() },
}));

vi.mock("../api/stockTransferService", () => ({
  stockTransferService: { create: vi.fn(), confirm: vi.fn() },
}));

vi.mock("../../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    confirm: vi.fn(),
  },
}));

const WAREHOUSES: WarehouseDto[] = [
  {
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
  },
  {
    id: "wh-2",
    branchId: "branch-1",
    name: "Bodega Sur",
    code: "S01",
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
  },
];

function buildTransfer(overrides: Partial<StockTransferDto> = {}): StockTransferDto {
  return {
    id: "transfer-1",
    transferNumber: "TR-000001",
    sourceWarehouseId: "wh-1",
    targetWarehouseId: "wh-2",
    transferDate: "2026-08-01",
    status: "Draft",
    reason: null,
    notes: null,
    confirmedAt: null,
    lines: [{ id: "line-1", productId: "prod-1", quantity: 5, description: "SKU-1 — Producto 1" }],
    ...overrides,
  };
}

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

/** Renderiza el `message: ReactNode` de la última llamada a message.confirm, para verificar el
 * resumen (n.º/origen/destino/líneas/unidades/estado) sin depender de que sea un string plano. */
function renderLastConfirmMessage() {
  const calls = vi.mocked(message.confirm).mock.calls;
  render(React.createElement(React.Fragment, null, calls[calls.length - 1][0].message));
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(warehouseService.list).mockResolvedValue(WAREHOUSES);
  vi.mocked(branchLookupFacade.list).mockResolvedValue([
    {
      id: "branch-1",
      name: "Matriz",
      code: "001",
      address: "Av. Principal",
      countryId: null,
      provinceId: null,
      cantonId: null,
      parishId: null,
      phone: null,
      email: null,
      managerName: null,
      isActive: true,
      isMainBranch: true,
    },
  ]);
  vi.mocked(stockService.getWarehouseAvailability).mockResolvedValue([
    {
      warehouseId: "wh-1",
      warehouseName: "Bodega Norte",
      available: 20,
      reserved: 0,
      canSell: true,
    },
  ]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

/** Deja el hook con una transferencia en Draft lista para confirmar — mismo flujo real: elegir
 * bodegas, agregar una línea, crear (queda en Draft). */
async function setupDraftTransfer() {
  const { result } = renderHook(() => useStockTransferPage(), { wrapper });

  await waitFor(() => expect(result.current.warehouses).toEqual(WAREHOUSES));

  act(() => {
    result.current.setSourceWarehouseId("wh-1");
    result.current.setTargetWarehouseId("wh-2");
  });

  await act(async () => {
    await result.current.addLine({ id: "prod-1", sku: "SKU-1", name: "Producto 1" });
  });

  vi.mocked(stockTransferService.create).mockResolvedValue(buildTransfer());
  await act(async () => {
    await result.current.createTransfer();
  });
  await waitFor(() => expect(result.current.transfer?.id).toBe("transfer-1"));

  return result;
}

describe("useStockTransferPage — confirmar transferencia: confirmación y feedback", () => {
  it("pide confirmación antes de llamar a confirm, con resumen de origen/destino/líneas/unidades/estado", async () => {
    vi.mocked(stockTransferService.confirm).mockResolvedValue(
      buildTransfer({ status: "Confirmed" }),
    );
    const result = await setupDraftTransfer();

    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(stockTransferService.confirm).toHaveBeenCalledWith("transfer-1");

    renderLastConfirmMessage();
    expect(screen.getByText(/TR-000001/)).toBeTruthy();
    expect(screen.getByText(/Bodega Norte/)).toBeTruthy();
    expect(screen.getByText(/Bodega Sur/)).toBeTruthy();
    expect(screen.getAllByText("1", { exact: true }).length).toBe(2); // líneas + total unidades
    expect(screen.getByText("Draft")).toBeTruthy(); // estado actual, antes de confirmar
  });

  it("si se cancela, no llama a stockTransferService.confirm", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const result = await setupDraftTransfer();

    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(stockTransferService.confirm).not.toHaveBeenCalled();
    expect(result.current.transfer?.status).toBe("Draft");
  });

  it("al confirmar exitosamente muestra message.success", async () => {
    vi.mocked(stockTransferService.confirm).mockResolvedValue(
      buildTransfer({ status: "Confirmed" }),
    );
    const result = await setupDraftTransfer();

    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(message.success).toHaveBeenCalledWith("Transferencia confirmada correctamente.");
    expect(result.current.transfer?.status).toBe("Confirmed");
  });

  it("si falla, no llama message.success y expone el mensaje real vía formatApiRequestError", async () => {
    vi.mocked(stockTransferService.confirm).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La transferencia ya fue procesada." } },
      },
    });
    const result = await setupDraftTransfer();

    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(message.error).toHaveBeenCalledWith("La transferencia ya fue procesada.");
    expect(message.success).not.toHaveBeenCalled();
    // No estado optimista: sigue en Draft porque el backend no confirmó.
    expect(result.current.transfer?.status).toBe("Draft");
  });

  it("no permite doble submit mientras la confirmación anterior sigue en curso", async () => {
    let resolveConfirm: (dto: StockTransferDto) => void = () => {};
    vi.mocked(stockTransferService.confirm).mockImplementation(
      () =>
        new Promise((resolve) => {
          resolveConfirm = resolve;
        }),
    );
    const result = await setupDraftTransfer();

    let firstCall: Promise<void>;
    act(() => {
      firstCall = result.current.confirmTransfer();
    });
    await waitFor(() => expect(result.current.confirming).toBe(true));

    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(stockTransferService.confirm).toHaveBeenCalledTimes(1);

    await act(async () => {
      resolveConfirm(buildTransfer({ status: "Confirmed" }));
      await firstCall;
    });
  });
});

describe("useStockTransferPage — sin diálogos nativos", () => {
  it("no usa window.confirm/window.prompt/alert", async () => {
    const confirmSpy = vi.spyOn(window, "confirm").mockReturnValue(true);
    const promptSpy = vi.spyOn(window, "prompt").mockReturnValue("");
    const alertSpy = vi.spyOn(window, "alert").mockImplementation(() => {});
    vi.mocked(stockTransferService.confirm).mockResolvedValue(
      buildTransfer({ status: "Confirmed" }),
    );

    const result = await setupDraftTransfer();
    await act(async () => {
      await result.current.confirmTransfer();
    });

    expect(confirmSpy).not.toHaveBeenCalled();
    expect(promptSpy).not.toHaveBeenCalled();
    expect(alertSpy).not.toHaveBeenCalled();

    confirmSpy.mockRestore();
    promptSpy.mockRestore();
    alertSpy.mockRestore();
  });
});
