// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";

/**
 * SRI-MONITOR-UX-01 — protección contra respuestas obsoletas: si una consulta anterior responde
 * después de la más reciente, su resultado se descarta (el listado refleja siempre los filtros
 * vigentes) y no apaga el indicador de carga de la consulta en curso.
 */

vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: () => true }),
}));

const getList = vi.fn();
const getDashboard = vi.fn();
vi.mock("../api/electronicDocumentsMonitorService", () => ({
  electronicDocumentsMonitorService: {
    getList: (...a: unknown[]) => getList(...a),
    getDashboard: (...a: unknown[]) => getDashboard(...a),
  },
}));

vi.mock("../../../ride/facades/rideGenerationFacade", () => ({
  useRideActions: () => ({ ridePending: false, handleDownloadRide: vi.fn() }),
}));

import { useElectronicDocumentsMonitor } from "./useElectronicDocumentsMonitor";

type Deferred<T> = { promise: Promise<T>; resolve: (v: T) => void };
function deferred<T>(): Deferred<T> {
  let resolve!: (v: T) => void;
  const promise = new Promise<T>((r) => (resolve = r));
  return { promise, resolve };
}

const listOf = (id: string) => ({
  items: [{ id } as never],
  total: 1,
  pageNumber: 1,
  pageSize: 25,
});

beforeEach(() => {
  vi.clearAllMocks();
  getDashboard.mockResolvedValue(null);
});

describe("useElectronicDocumentsMonitor — respuestas fuera de orden", () => {
  it("descarta el listado de una consulta anterior que responde tarde", async () => {
    const first = deferred<ReturnType<typeof listOf>>();
    const second = deferred<ReturnType<typeof listOf>>();
    getList.mockImplementation((params: { state?: string; pageSize: number }) => {
      if (params.pageSize === 5) return Promise.resolve(listOf("attention"));
      return params.state === "Rejected" ? second.promise : first.promise;
    });

    const { result } = renderHook(() => useElectronicDocumentsMonitor());
    act(() => result.current.setActiveTab("documents"));
    await waitFor(() =>
      expect(getList.mock.calls.filter(([p]) => p.pageSize === 25)).toHaveLength(1),
    );

    act(() => result.current.setState("Rejected"));
    await waitFor(() =>
      expect(getList.mock.calls.filter(([p]) => p.pageSize === 25)).toHaveLength(2),
    );

    await act(async () => second.resolve(listOf("rejected")));
    expect(result.current.list?.items[0].id).toBe("rejected");
    expect(result.current.listLoading).toBe(false);

    await act(async () => first.resolve(listOf("stale")));
    expect(result.current.list?.items[0].id).toBe("rejected");
  });

  it("una respuesta tardía no apaga la carga de la consulta vigente", async () => {
    const first = deferred<ReturnType<typeof listOf>>();
    const second = deferred<ReturnType<typeof listOf>>();
    getList.mockImplementation((params: { state?: string; pageSize: number }) => {
      if (params.pageSize === 5) return Promise.resolve(listOf("attention"));
      return params.state === "Rejected" ? second.promise : first.promise;
    });

    const { result } = renderHook(() => useElectronicDocumentsMonitor());
    act(() => result.current.setActiveTab("documents"));
    await waitFor(() => expect(result.current.listLoading).toBe(true));
    act(() => result.current.setState("Rejected"));
    await waitFor(() =>
      expect(getList.mock.calls.filter(([p]) => p.pageSize === 25)).toHaveLength(2),
    );

    await act(async () => first.resolve(listOf("stale")));
    expect(result.current.listLoading).toBe(true);
    expect(result.current.list).toBeNull();

    await act(async () => second.resolve(listOf("rejected")));
    expect(result.current.listLoading).toBe(false);
    expect(result.current.list?.items[0].id).toBe("rejected");
  });
});
