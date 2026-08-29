// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useEstablishmentsPage } from "./useEstablishmentsPage";
import {
  establishmentService,
  type EstablishmentListItemDto,
} from "../api/establishmentService";
import { branchLookupFacade } from "../../branches/facades/branchLookupFacade";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Activar/desactivar establecimiento": confirma
 * antes de ejecutar, no llama al backend si se cancela, éxito muestra message.success, fallo
 * muestra el mensaje real. No cambia numeración ni reglas SRI.
 */

vi.mock("../api/establishmentService", () => ({
  establishmentService: {
    list: vi.fn(),
    lookups: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
  },
}));

vi.mock("../../branches/facades/branchLookupFacade", () => ({
  branchLookupFacade: { list: vi.fn().mockResolvedValue([]) },
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

const ACTIVE_ESTABLISHMENT: EstablishmentListItemDto = {
  id: "est-1",
  code: "001",
  name: "Matriz",
  address: "Av. Principal",
  phone: null,
  branchId: null,
  branchName: null,
  emissionPointCount: 2,
  isMain: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(
    I18nProvider,
    null,
    React.createElement(MemoryRouter, null, children),
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(establishmentService.list).mockResolvedValue([ACTIVE_ESTABLISHMENT]);
  vi.mocked(branchLookupFacade.list).mockResolvedValue([]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("useEstablishmentsPage — activar/desactivar establecimiento: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar y no llama a la API si se cancela", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = renderHook(() => useEstablishmentsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_ESTABLISHMENT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_ESTABLISHMENT);
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(establishmentService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente llama a la API y muestra message.success", async () => {
    vi.mocked(establishmentService.disable).mockResolvedValue(true);
    const { result } = renderHook(() => useEstablishmentsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_ESTABLISHMENT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_ESTABLISHMENT);
    });

    expect(establishmentService.disable).toHaveBeenCalledWith("est-1");
    expect(message.success).toHaveBeenCalledWith("Establecimiento desactivado correctamente.");
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(establishmentService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El establecimiento tiene puntos de emisión activos." } },
      },
    });
    const { result } = renderHook(() => useEstablishmentsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_ESTABLISHMENT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_ESTABLISHMENT);
    });

    expect(message.error).toHaveBeenCalledWith(
      "El establecimiento tiene puntos de emisión activos.",
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});
