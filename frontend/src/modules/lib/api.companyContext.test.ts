// @vitest-environment jsdom
import { afterEach, describe, expect, it } from "vitest";
import type { AxiosHeaders, InternalAxiosRequestConfig } from "axios";
import { useAuthStore } from "../../store/authStore";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { clearOperationalContext } from "../auth/clearOperationalContext";
import { api } from "./api";

function readHeader(
  headers: InternalAxiosRequestConfig["headers"],
  name: string,
): unknown {
  const maybeAxiosHeaders = headers as AxiosHeaders;
  return typeof maybeAxiosHeaders.get === "function"
    ? maybeAxiosHeaders.get(name)
    : (headers as Record<string, unknown>)[name];
}

async function captureRequestHeaders(
  originalAdapter: typeof api.defaults.adapter,
): Promise<InternalAxiosRequestConfig> {
  let capturedConfig: InternalAxiosRequestConfig | null = null;
  api.defaults.adapter = async (config) => {
    capturedConfig = config;
    return {
      data: {},
      status: 200,
      statusText: "OK",
      headers: {},
      config,
    };
  };

  await api.get(["", "api", "v1", "session", "context"].join("/"));
  api.defaults.adapter = originalAdapter;
  return capturedConfig!;
}

describe("api company context headers", () => {
  const originalAdapter = api.defaults.adapter;

  afterEach(() => {
    api.defaults.adapter = originalAdapter;
    useAuthStore.setState({
      user: null,
      token: null,
      isAuthenticated: false,
      hasHydrated: false,
      companySessionVersion: 0,
    });
    useActiveBranchStore.setState({ branch: null });
  });

  it("envía X-Company-Id y X-Branch-Id cuando la empresa y la sucursal activas existen", async () => {
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-1",
      },
      token: null,
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
    });
    useActiveBranchStore.setState({
      branch: { id: "branch-1", name: "Matriz", isMainBranch: true },
    });

    const config = await captureRequestHeaders(originalAdapter);

    expect(readHeader(config.headers, "X-Company-Id")).toBe("company-1");
    expect(readHeader(config.headers, "X-Branch-Id")).toBe("branch-1");
  });

  it("no envía X-Branch-Id cuando activeBranchStore no tiene sucursal activa", async () => {
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-2",
      },
      token: null,
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 1,
    });
    useActiveBranchStore.setState({
      branch: {
        id: "old-branch",
        name: "Sucursal anterior",
        isMainBranch: false,
      },
    });

    clearOperationalContext();
    const config = await captureRequestHeaders(originalAdapter);

    expect(useActiveBranchStore.getState().branch).toBeNull();
    expect(readHeader(config.headers, "X-Company-Id")).toBe("company-2");
    expect(readHeader(config.headers, "X-Branch-Id")).toBeUndefined();
  });

  it("tras cambiar de empresa A a B y limpiar el contexto, no queda X-Branch-Id de A ni X-Company-Id de A", async () => {
    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "Admin",
        tenantId: "tenant-1",
        companyId: "company-A",
      },
      token: null,
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
    });
    useActiveBranchStore.setState({
      branch: { id: "branch-A", name: "Sucursal A", isMainBranch: true },
    });

    const beforeSwitch = await captureRequestHeaders(originalAdapter);
    expect(readHeader(beforeSwitch.headers, "X-Company-Id")).toBe("company-A");
    expect(readHeader(beforeSwitch.headers, "X-Branch-Id")).toBe("branch-A");

    useAuthStore.setState((state) => ({
      user: state.user ? { ...state.user, companyId: "company-B" } : state.user,
      companySessionVersion: state.companySessionVersion + 1,
    }));
    clearOperationalContext();

    const afterSwitch = await captureRequestHeaders(originalAdapter);
    expect(readHeader(afterSwitch.headers, "X-Company-Id")).toBe("company-B");
    expect(
      readHeader(afterSwitch.headers, "X-Branch-Id"),
    ).not.toBe("branch-A");
    expect(readHeader(afterSwitch.headers, "X-Branch-Id")).toBeUndefined();
  });
});
