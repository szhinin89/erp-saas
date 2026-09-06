// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { renderHook, waitFor, act, cleanup } from "@testing-library/react";
import { useAuthStore } from "../store/authStore";
import { useActiveBranchStore } from "../store/activeBranchStore";
import { useSessionStore } from "../store/sessionStore";
import { sessionService } from "../modules/session/api/sessionService";
import { useBranchGate } from "./useBranchGate";

vi.mock("../modules/session/api/sessionService", () => ({
  sessionService: {
    getAvailableBranches: vi.fn(),
    switchBranch: vi.fn(),
    getContext: vi.fn(),
  },
}));

const branchOptions = [
  { id: "branch-1", name: "Matriz", isMainBranch: true },
  { id: "branch-2", name: "Norte", isMainBranch: false },
];

function setCompany(companyId: string | null) {
  useAuthStore.setState({
    user:
      companyId === null
        ? null
        : {
            userId: "user-1",
            fullName: "Test User",
            username: "test.user",
            email: "test@example.com",
            role: "Cajero",
            tenantId: "tenant-1",
            companyId,
          },
    isAuthenticated: true,
    hasHydrated: true,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  useActiveBranchStore.setState({ branch: null });
  useSessionStore.setState({ isLoading: false, isLoaded: false });
  setCompany(null);
});

afterEach(() => {
  // Sin esto, instancias de renderHook de tests previos siguen suscritas a los stores
  // globales (zustand) y vuelven a disparar su propio efecto cuando un test posterior
  // muta isLoading/branch, contaminando los conteos de llamadas entre tests.
  cleanup();
  useActiveBranchStore.setState({ branch: null });
  useAuthStore.setState({ user: null, isAuthenticated: false });
  useSessionStore.setState({ isLoading: false, isLoaded: false });
});

describe("useBranchGate", () => {
  it("no abre el gate cuando ya existe una sucursal activa", async () => {
    setCompany("company-1");
    useActiveBranchStore.setState({
      branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
    });

    const { result } = renderHook(() => useBranchGate());

    expect(result.current.gateOpen).toBe(false);
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();
  });

  it("no abre el gate cuando no hay empresa operativa activa", () => {
    setCompany(null);

    const { result } = renderHook(() => useBranchGate());

    expect(result.current.gateOpen).toBe(false);
  });

  it("DirectToDefault con default autorizado ejecuta switch automático sin mostrar selector", async () => {
    vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
      branches: branchOptions,
      loginMode: "DirectToDefault",
      defaultBranchId: "branch-1",
    });
    vi.mocked(sessionService.switchBranch).mockResolvedValue({
      id: "branch-1",
      name: "Matriz",
      isMainBranch: true,
    });

    setCompany("company-1");
    const { result } = renderHook(() => useBranchGate());

    await waitFor(() =>
      expect(sessionService.switchBranch).toHaveBeenCalledWith("branch-1"),
    );
    await waitFor(() => expect(result.current.gateOpen).toBe(false));
    expect(useActiveBranchStore.getState().branch?.id).toBe("branch-1");
  });

  it("AskBranch mantiene el gate abierto con las opciones para selección manual", async () => {
    vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
      branches: branchOptions,
      loginMode: "AskBranch",
      defaultBranchId: null,
    });

    setCompany("company-1");
    const { result } = renderHook(() => useBranchGate());

    await waitFor(() => expect(result.current.options).toEqual(branchOptions));
    expect(result.current.gateOpen).toBe(true);
    expect(sessionService.switchBranch).not.toHaveBeenCalled();
  });

  it("error al cambiar de sucursal manualmente se expone en error y no cierra el gate", async () => {
    vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
      branches: branchOptions,
      loginMode: "AskBranch",
      defaultBranchId: null,
    });
    vi.mocked(sessionService.switchBranch).mockRejectedValue({
      isAxiosError: true,
      response: undefined,
    });

    setCompany("company-1");
    const { result } = renderHook(() => useBranchGate());

    await waitFor(() => expect(result.current.gateOpen).toBe(true));

    await act(async () => {
      await result.current.selectBranch("branch-2");
    });

    expect(result.current.error).toBeTruthy();
    expect(result.current.gateOpen).toBe(true);
  });

  it("ERP-CORE-BRANCH-GATE-FLICKER-01: mientras session/context está cargando el gate NO abre (AppLayout evita render mientras tanto) ni consulta available-branches", () => {
    setCompany("company-1");
    useSessionStore.setState({ isLoading: true });

    const { result } = renderHook(() => useBranchGate());

    expect(result.current.gateOpen).toBe(false);
    expect(result.current.contextLoading).toBe(true);
    expect(result.current.loading).toBe(true);
    expect(result.current.options).toEqual([]);
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();
  });

  it("si session/context resuelve una sucursal mientras carga, el gate nunca llega a abrirse ni consulta available-branches", async () => {
    setCompany("company-1");
    useSessionStore.setState({ isLoading: true });

    const { result } = renderHook(() => useBranchGate());
    expect(result.current.gateOpen).toBe(false);

    act(() => {
      useActiveBranchStore.getState().setBranch({
        id: "branch-1",
        name: "Matriz",
        isMainBranch: true,
      });
      useSessionStore.setState({ isLoading: false, isLoaded: true });
    });

    await waitFor(() => expect(result.current.gateOpen).toBe(false));
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();
  });

  it("ZH-AUTH-BRANCH-CONTEXT-EXPENSES-AUDIT-12: si la sucursal activa se limpia externamente (BRANCH_SCOPE_FORBIDDEN), el gate reabre y consulta available-branches", async () => {
    vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
      branches: branchOptions,
      loginMode: "AskBranch",
      defaultBranchId: null,
    });

    setCompany("company-1");
    useActiveBranchStore.setState({
      branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
    });

    const { result } = renderHook(() => useBranchGate());
    expect(result.current.gateOpen).toBe(false);
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();

    // Simula lo que hace el interceptor de axios al recibir BRANCH_SCOPE_FORBIDDEN: limpiar
    // la sucursal inválida, sin ninguna otra intervención del componente que hizo el request.
    act(() => {
      useActiveBranchStore.getState().clear();
    });

    await waitFor(() => expect(result.current.gateOpen).toBe(true));
    await waitFor(() =>
      expect(sessionService.getAvailableBranches).toHaveBeenCalledTimes(1),
    );
    expect(result.current.options).toEqual(branchOptions);
  });

  it("cuando session/context termina sin resolver sucursal, recién ahí consulta available-branches", async () => {
    vi.mocked(sessionService.getAvailableBranches).mockResolvedValue({
      branches: branchOptions,
      loginMode: "AskBranch",
      defaultBranchId: null,
    });

    setCompany("company-1");
    useSessionStore.setState({ isLoading: true });

    const { result } = renderHook(() => useBranchGate());
    expect(sessionService.getAvailableBranches).not.toHaveBeenCalled();

    act(() => {
      useSessionStore.setState({ isLoading: false });
    });

    await waitFor(() =>
      expect(sessionService.getAvailableBranches).toHaveBeenCalledTimes(1),
    );
    await waitFor(() => expect(result.current.options).toEqual(branchOptions));
    expect(result.current.gateOpen).toBe(true);
  });
});
