// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";

/**
 * Monitor electrónico — "Descargar RIDE" delega en el hook RIDE genérico con el
 * (sourceModule, sourceEntityId, documentNumber) del detalle; nunca un módulo fijo.
 */

const granted = new Set<string>();
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: (p: string) => granted.has(p) }),
}));

const getDetailMock = vi.fn();
vi.mock("../api/electronicDocumentsMonitorService", () => ({
  electronicDocumentsMonitorService: {
    getList: vi.fn().mockResolvedValue({
      items: [],
      total: 0,
      pageNumber: 1,
      pageSize: 25,
    }),
    getDashboard: vi.fn().mockResolvedValue(null),
    getDetail: (...a: unknown[]) => getDetailMock(...a),
  },
}));

const useRideActionsMock = vi.fn();
const handleDownloadRideMock = vi.fn();
vi.mock("../../../ride/facades/rideGenerationFacade", () => ({
  useRideActions: (sourceModule: string) => {
    useRideActionsMock(sourceModule);
    return {
      ridePending: false,
      handleViewRide: vi.fn(),
      handleDownloadRide: handleDownloadRideMock,
      handleRegenerateRide: vi.fn(),
    };
  },
}));

import { useElectronicDocumentsMonitor } from "./useElectronicDocumentsMonitor";

describe("useElectronicDocumentsMonitor — downloadRide", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    granted.clear();
    granted.add("electronic-documents.view");
    granted.add("electronic-documents.detail");
  });

  it("expone canDownloadRide solo con ride.view", () => {
    const without = renderHook(() => useElectronicDocumentsMonitor());
    expect(without.result.current.canDownloadRide).toBe(false);

    granted.add("ride.view");
    const withPerm = renderHook(() => useElectronicDocumentsMonitor());
    expect(withPerm.result.current.canDownloadRide).toBe(true);
  });

  it("descarga con el sourceModule/sourceEntityId/documentNumber del detalle", async () => {
    granted.add("ride.view");
    getDetailMock.mockResolvedValue({
      id: "edoc-1",
      sourceModule: "Sales",
      sourceEntityId: "return-7",
      documentNumber: "001-001-000000045",
      documentType: "CreditNote",
    });
    const { result } = renderHook(() => useElectronicDocumentsMonitor());

    await act(async () => {
      await result.current.openDetail("edoc-1");
    });
    await waitFor(() => expect(result.current.detail).not.toBeNull());

    await act(async () => {
      await result.current.downloadRide();
    });

    expect(useRideActionsMock).toHaveBeenLastCalledWith("Sales");
    expect(handleDownloadRideMock).toHaveBeenCalledWith(
      "return-7",
      "001-001-000000045",
    );
  });

  it("sin detalle cargado no descarga nada", async () => {
    granted.add("ride.view");
    const { result } = renderHook(() => useElectronicDocumentsMonitor());

    await act(async () => {
      await result.current.downloadRide();
    });

    expect(handleDownloadRideMock).not.toHaveBeenCalled();
  });
});
