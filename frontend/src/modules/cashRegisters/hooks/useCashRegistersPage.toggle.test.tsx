// @vitest-environment jsdom
import React from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useCashRegistersPage } from "./useCashRegistersPage";
import {
  cashRegisterAdminFacade,
  type CashRegisterDto,
} from "../../caja/facades/cashRegisterAdminFacade";
import { message } from "../../../lib/messages";
import { I18nProvider } from "../../../i18n/i18n";

/**
 * FUNC-DEACTIVATION-CONFIRM-01 (AA-D5) — Cajas: desactivar confirma, cancelar no llama al
 * backend, una sola petición, error real del backend (p. ej. sesión abierta), permiso
 * caja.manage conservado y activar sin confirmación.
 */

const permissions = { allowed: true };
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => permissions.allowed }),
}));

vi.mock("../../../lib/messages", () => ({
  message: { confirm: vi.fn(), success: vi.fn(), error: vi.fn() },
}));

vi.mock("../../caja/facades/cashRegisterAdminFacade", () => ({
  cashRegisterAdminFacade: {
    listAllCashRegisters: vi.fn(),
    emissionPointLookupsByBranch: vi.fn().mockResolvedValue([]),
    createCashRegister: vi.fn(),
    updateCashRegister: vi.fn(),
    disableCashRegister: vi.fn(),
    enableCashRegister: vi.fn(),
  },
}));

vi.mock("../../branches/facades/branchLookupFacade", () => ({
  branchLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../inventory/facades/warehouseLookupFacade", () => ({
  warehouseLookupFacade: { list: vi.fn().mockResolvedValue([]) },
}));

vi.mock("../../accounting/facades/accountLookupFacade", () => ({
  accountLookupFacade: { listAccounts: vi.fn().mockResolvedValue([]) },
}));

function register(id: string, isActive: boolean): CashRegisterDto {
  return {
    id,
    accountingAccountId: null,
    branchId: "branch-1",
    branchName: "Sucursal Ficticia",
    branchCode: "001",
    emissionPointId: null,
    establishmentCode: null,
    emissionPointCode: null,
    emissionPointName: null,
    code: `CJ-${id}`,
    name: `Caja ${id}`,
    notes: null,
    isActive,
    hasHistory: false,
    defaultWarehouseId: null,
    defaultWarehouseCode: null,
    defaultWarehouseName: null,
  } as unknown as CashRegisterDto;
}

const ACTIVE = register("1", true);
const INACTIVE = register("2", false);
const facade = vi.mocked(cashRegisterAdminFacade);

async function renderReady() {
  const hook = renderHook(() => useCashRegistersPage(), {
    wrapper: ({ children }: { children: React.ReactNode }) =>
      React.createElement(I18nProvider, null, children),
  });
  await waitFor(() => expect(hook.result.current.loading).toBe(false));
  return hook;
}

beforeEach(() => {
  vi.clearAllMocks();
  permissions.allowed = true;
  facade.listAllCashRegisters.mockResolvedValue([ACTIVE, INACTIVE]);
  facade.disableCashRegister.mockResolvedValue(true as never);
  facade.enableCashRegister.mockResolvedValue(true as never);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("useCashRegistersPage — desactivar caja con confirmación", () => {
  it("cancelar no envía petición al backend", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(vi.mocked(message.confirm).mock.calls[0][0]).toMatchObject({
      title: 'Desactivar caja "CJ-1 — Caja 1"',
      confirmLabel: "Desactivar",
      cancelLabel: "Cancelar",
    });
    expect(facade.disableCashRegister).not.toHaveBeenCalled();
  });

  it("confirmar envía una única petición aunque se pulse dos veces", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await Promise.all([
        result.current.toggleDisable(ACTIVE),
        result.current.toggleDisable(ACTIVE),
      ]);
    });
    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(facade.disableCashRegister).toHaveBeenCalledTimes(1);
    expect(facade.disableCashRegister).toHaveBeenCalledWith("1");
  });

  it("expone el estado de carga mientras la petición está pendiente", async () => {
    let resolve: (v: boolean) => void = () => {};
    facade.disableCashRegister.mockReturnValue(
      new Promise<boolean>((r) => (resolve = r)) as never,
    );
    const { result } = await renderReady();
    let pending: Promise<void> = Promise.resolve();
    act(() => {
      pending = result.current.toggleDisable(ACTIVE);
    });
    await waitFor(() => expect(result.current.togglingId).toBe("1"));
    await act(async () => {
      resolve(true);
      await pending;
    });
    expect(result.current.togglingId).toBeNull();
  });

  it("caja no elegible (sesión abierta): muestra el motivo exacto del backend", async () => {
    facade.disableCashRegister.mockRejectedValue(
      new Error("No se puede desactivar: la caja tiene una sesión abierta."),
    );
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(result.current.error).toContain("sesión abierta");
    expect(result.current.togglingId).toBeNull();
  });

  it("sin caja.manage no confirma ni llama al backend", async () => {
    permissions.allowed = false;
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(ACTIVE);
    });
    expect(message.confirm).not.toHaveBeenCalled();
    expect(facade.disableCashRegister).not.toHaveBeenCalled();
  });

  it("activar conserva el flujo directo sin confirmación", async () => {
    const { result } = await renderReady();
    await act(async () => {
      await result.current.toggleDisable(INACTIVE);
    });
    expect(message.confirm).not.toHaveBeenCalled();
    expect(facade.enableCashRegister).toHaveBeenCalledWith("2");
  });
});
