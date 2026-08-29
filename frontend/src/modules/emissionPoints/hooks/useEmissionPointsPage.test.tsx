// @vitest-environment jsdom
import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, waitFor, act } from "@testing-library/react";
import React from "react";
import { I18nProvider } from "../../../i18n/i18n";
import { useEmissionPointsPage } from "./useEmissionPointsPage";
import {
  emissionPointsService,
  type EmissionPointListItemDto,
} from "../api/emissionPointsService";
import { message } from "../../../lib/messages";

/**
 * CRITICAL-CONFIRMATIONS-SENSITIVE-CONFIG-06 — "Activar/desactivar punto de emisión": confirma
 * antes de ejecutar, no llama al backend si se cancela, éxito muestra message.success, fallo
 * muestra el mensaje real. No cambia lógica SRI ni numeración.
 */

vi.mock("../api/emissionPointsService", () => ({
  EMISSION_TYPE_ELECTRONIC: "Electronic",
  EMISSION_TYPE_PHYSICAL: "Physical",
  emissionPointsService: {
    list: vi.fn(),
    establishmentLookups: vi.fn().mockResolvedValue([]),
    create: vi.fn(),
    update: vi.fn(),
    disable: vi.fn(),
    enable: vi.fn(),
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

const ACTIVE_EMISSION_POINT: EmissionPointListItemDto = {
  id: "ep-1",
  establishmentId: "est-1",
  establishmentCode: "001",
  establishmentName: "Matriz",
  branchName: null,
  code: "001",
  name: "Punto principal",
  emissionType: "Electronic",
  isDefault: true,
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
};

function wrapper({ children }: { children: React.ReactNode }) {
  return React.createElement(I18nProvider, null, children);
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(emissionPointsService.list).mockResolvedValue([ACTIVE_EMISSION_POINT]);
  vi.mocked(message.confirm).mockResolvedValue(true);
});

describe("useEmissionPointsPage — activar/desactivar punto de emisión: confirmación y feedback", () => {
  it("pide confirmación antes de desactivar y no llama a la API si se cancela", async () => {
    vi.mocked(message.confirm).mockResolvedValue(false);
    const { result } = renderHook(() => useEmissionPointsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_EMISSION_POINT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_EMISSION_POINT);
    });

    expect(message.confirm).toHaveBeenCalledTimes(1);
    expect(emissionPointsService.disable).not.toHaveBeenCalled();
  });

  it("al desactivar exitosamente llama a la API y muestra message.success", async () => {
    vi.mocked(emissionPointsService.disable).mockResolvedValue(true);
    const { result } = renderHook(() => useEmissionPointsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_EMISSION_POINT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_EMISSION_POINT);
    });

    expect(emissionPointsService.disable).toHaveBeenCalledWith("ep-1");
    expect(message.success).toHaveBeenCalledWith("Punto de emisión desactivado correctamente.");
  });

  it("si falla, muestra el mensaje real del backend y no muestra éxito", async () => {
    vi.mocked(emissionPointsService.disable).mockRejectedValue({
      isAxiosError: true,
      response: {
        status: 409,
        data: { message: { user: "El punto de emisión tiene documentos pendientes." } },
      },
    });
    const { result } = renderHook(() => useEmissionPointsPage(), { wrapper });
    await waitFor(() => expect(result.current.items).toEqual([ACTIVE_EMISSION_POINT]));

    await act(async () => {
      await result.current.toggleDisable(ACTIVE_EMISSION_POINT);
    });

    expect(message.error).toHaveBeenCalledWith(
      "El punto de emisión tiene documentos pendientes.",
    );
    expect(message.success).not.toHaveBeenCalled();
  });
});
