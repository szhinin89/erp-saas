// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthStore } from "../../store/authStore";
import { useActiveBranchStore } from "../../store/activeBranchStore";
import { useSessionStore } from "../../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../../store/electronicInvoicingStatusStore";
import type { AuthResponse } from "../../types/auth";
import { companyManagementService } from "../company-management/api/companyManagementService";
import { loadDecimalConfig } from "../../lib/config/decimal.config";
import { clearOperationalContext } from "./clearOperationalContext";
import { syncCompanySelection } from "./syncCompanySelection";

vi.mock("../company-management/api/companyManagementService", () => ({
  companyManagementService: { getCurrent: vi.fn() },
}));

vi.mock("../../lib/config/decimal.config", () => ({
  loadDecimalConfig: vi.fn(),
}));

vi.mock("../../lib/session/devSessionLog", () => ({
  logDevSessionContext: vi.fn(),
}));

vi.mock("./clearOperationalContext", () => ({
  clearOperationalContext: vi.fn(),
}));

const authResponse: AuthResponse = {
  userId: "user-1",
  fullName: "Ana Perez",
  username: "ana",
  email: "ana@test.com",
  role: "Admin",
  tenantId: "tenant-1",
  companyId: "company-2",
  token: "access-token",
  refreshToken: "refresh-token",
  refreshTokenExpiry: "2026-09-01T12:00:00Z",
};

const originalAuthLogin = useAuthStore.getState().login;
const originalSessionRefresh = useSessionStore.getState().refresh;
const originalElectronicStatusRefresh =
  useElectronicInvoicingStatusStore.getState().refresh;

describe("syncCompanySelection", () => {
  const order: string[] = [];

  beforeEach(() => {
    order.length = 0;
    vi.clearAllMocks();

    useAuthStore.setState({
      user: {
        userId: "user-1",
        fullName: "Ana Perez",
        username: "ana",
        email: "ana@test.com",
        role: "User",
        tenantId: "tenant-1",
        companyId: "company-1",
      },
      token: "old-token",
      isAuthenticated: true,
      hasHydrated: true,
      companySessionVersion: 0,
      login: vi.fn((response: AuthResponse) => {
        order.push("login");
        originalAuthLogin(response);
      }),
    });

    useActiveBranchStore.setState({
      branch: { id: "old-branch", name: "Sucursal anterior", isMainBranch: false },
    });

    useSessionStore.setState({
      refresh: vi.fn(async () => {
        order.push("sessionStore.refresh");
      }),
    });
    useElectronicInvoicingStatusStore.setState({
      refresh: vi.fn(async () => {
        order.push("electronicInvoicingStatusStore.refresh");
      }),
    });

    vi.mocked(clearOperationalContext).mockImplementation(() => {
      order.push("clearOperationalContext");
    });
    vi.mocked(companyManagementService.getCurrent).mockImplementation(async () => {
      order.push("companyManagementService.getCurrent");
      return null;
    });
    vi.mocked(loadDecimalConfig).mockImplementation(async () => {
      order.push("loadDecimalConfig");
      return {
        salesUnitPrice: 2,
        purchaseUnitPrice: 4,
        quantity: 4,
        percentage: 2,
        totalAmount: 2,
      };
    });
  });

  afterEach(() => {
    useAuthStore.setState({
      user: null,
      token: null,
      isAuthenticated: false,
      hasHydrated: false,
      companySessionVersion: 0,
      login: originalAuthLogin,
    });
    useActiveBranchStore.setState({ branch: null });
    useSessionStore.setState({ refresh: originalSessionRefresh });
    useElectronicInvoicingStatusStore.setState({
      refresh: originalElectronicStatusRefresh,
    });
  });

  it("aplica login nuevo, limpia el contexto operativo antes de refrescar secundarios y actualiza companyId", async () => {
    await syncCompanySelection(authResponse);

    expect(order[0]).toBe("login");
    expect(order[1]).toBe("clearOperationalContext");
    expect(order.slice(2).sort()).toEqual(
      [
        "companyManagementService.getCurrent",
        "electronicInvoicingStatusStore.refresh",
        "loadDecimalConfig",
        "sessionStore.refresh",
      ].sort(),
    );

    expect(useAuthStore.getState().user?.companyId).toBe("company-2");
    expect(clearOperationalContext).toHaveBeenCalledTimes(1);
  });

  it("no deja pendiente la limpieza del contexto si un refresh secundario falla", async () => {
    vi.mocked(loadDecimalConfig).mockImplementation(async () => {
      order.push("loadDecimalConfig");
      throw new Error("network error");
    });

    await syncCompanySelection(authResponse);

    expect(order).toContain("clearOperationalContext");
    expect(order.indexOf("clearOperationalContext")).toBeLessThan(
      order.indexOf("loadDecimalConfig"),
    );
  });
});
