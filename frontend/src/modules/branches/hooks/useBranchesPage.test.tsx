// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useBranchesPage } from "./useBranchesPage";
import { branchService, type BranchListItemDto } from "../api/branchService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Activar/desactivar sucursal": confirma antes de
 * ejecutar, no llama al backend si se cancela, éxito muestra message.success, fallo muestra el
 * mensaje real vía formatApiRequestError. No cambia payload/lógica de branchService.
 */

vi.mock("../api/branchService", () => ({
  branchService: {
    list: vi.fn(),
    getById: vi.fn(),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
    countries: vi.fn().mockResolvedValue([]),
    provinces: vi.fn().mockResolvedValue([]),
    cantons: vi.fn().mockResolvedValue([]),
    parishes: vi.fn().mockResolvedValue([]),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

vi.mock("../../../lib/messages", () => ({
  message: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    confirm: vi.fn(),
  },
}));

const ACTIVE_BRANCH: BranchListItemDto = {
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
};

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(branchService.list).mockResolvedValue([ACTIVE_BRANCH]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("useBranchesPage — activar/desactivar sucursal: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar y no llama a la API si se cancela", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = renderHook(() => useBranchesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_BRANCH]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_BRANCH);
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(branchService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente llama a la API y muestra message.success", async () => {
    vi.mocked(branchService.disable).mockResolvedValue(ACTIVE_BRANCH);
    const { result } = renderHook(() => useBranchesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_BRANCH]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_BRANCH);
    });

    expect(branchService.disable).toHaveBeenCalledWith("branch-1");
    expect(message.success).toHaveBeenCalledWith("Sucursal desactivada correctamente.");
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(branchService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "La sucursal tiene bodegas asociadas." } },
      },
    });
    const { result } = renderHook(() => useBranchesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_BRANCH]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_BRANCH);
    });

    expect(message.error).toHaveBeenCalledWith("La sucursal tiene bodegas asociadas.");
    expect(message.success).not.toHaveBeenCalled();
  });

  it("activar explica que vuelve a estar disponible", async () => {
    const inactiveBranch = { ...ACTIVE_BRANCH, isActive: false };
    vi.mocked(branchService.list).mockResolvedValue([inactiveBranch]);
    vi.mocked(branchService.enable).mockResolvedValue(inactiveBranch);
    const { result } = renderHook(() => useBranchesPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([inactiveBranch]));

    await act(async () => {
      await result.current.toggleDisable(inactiveBranch);
    });

    expect(branchService.enable).toHaveBeenCalledWith("branch-1");
    const options = vi.mocked(message.confirm).mock.calls[0][0];
    expect(String(options.message)).toMatch(/volverá a estar disponible/i);
    expect(message.success).toHaveBeenCalledWith("Sucursal activada correctamente.");
  });
});
