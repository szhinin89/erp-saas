// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { render, waitFor } from "@testing-library/react";
import { SessionBootstrap } from "./SessionBootstrap";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../store/electronicInvoicingStatusStore";
import { setAccessToken, clearAccessToken } from "../lib/session/authTokenMemory";
import { sessionService } from "../modules/session/api/sessionService";
import { electronicInvoicingService } from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";
import { loadDecimalConfig } from "../lib/config/decimal.config";

// ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: el bootstrap global de sesión no debe
// disparar el ping externo al SRI (el que produce el ruido SocketException 10054 en logs al
// cargar cualquier pantalla) — solo resuelve estado LOCAL de facturación electrónica. La
// conectividad real se verifica aparte, solo en Ventas (ver useSalesPage.ts).

vi.mock("../modules/session/api/sessionService", () => ({
  sessionService: { getContext: vi.fn() },
}));

vi.mock(
  "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService",
  () => ({
    electronicInvoicingService: { getStatus: vi.fn() },
  }),
);

vi.mock("../lib/config/decimal.config", () => ({
  loadDecimalConfig: vi.fn().mockResolvedValue({}),
}));

vi.mock("../lib/session/authRefreshManager", () => ({
  initializeAuthBroadcastListener: vi.fn(),
}));

function resetStores() {
  useAuthStore.setState({ hasHydrated: false, isAuthenticated: false });
  useSessionStore.getState().clear();
  useElectronicInvoicingStatusStore.getState().clear();
  clearAccessToken();
}

beforeEach(() => {
  vi.clearAllMocks();
  resetStores();
  vi.mocked(sessionService.getContext).mockResolvedValue({
    identity: { userId: "u1", fullName: "Test", email: "t@t.com" },
    tenant: { id: "t1", displayName: "Tenant", logo: null },
    authorization: { roles: [], permissions: ["*"] },
    preferences: { language: "es" },
    branch: { id: "b1", name: "Matriz", isMainBranch: true },
  });
  vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue({
    status: "Ready",
    configured: true,
    environment: "Production",
    environmentName: "Producción",
    emissionType: "Normal",
    certificateInstalled: true,
    certificateValid: true,
    certificateExpiresAt: null,
    certificateDaysRemaining: null,
    sriAvailability: "Unknown",
    canIssue: true,
  });
});

afterEach(() => {
  resetStores();
});

describe("SessionBootstrap — estado de facturación electrónica", () => {
  it("al autenticarse, llama getStatus() sin checkConnectivity (nunca pide ping externo al SRI)", async () => {
    setAccessToken("fake-token");
    useAuthStore.setState({ hasHydrated: true, isAuthenticated: true });

    render(
      <SessionBootstrap>
        <div>app</div>
      </SessionBootstrap>,
    );

    await waitFor(() =>
      expect(electronicInvoicingService.getStatus).toHaveBeenCalled(),
    );

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledWith();
    expect(electronicInvoicingService.getStatus).not.toHaveBeenCalledWith(
      expect.objectContaining({ checkConnectivity: true }),
    );
    expect(loadDecimalConfig).toHaveBeenCalled();
  });

  it("sin autenticación, no llama getStatus y limpia el store", async () => {
    useAuthStore.setState({ hasHydrated: true, isAuthenticated: false });

    render(
      <SessionBootstrap>
        <div>app</div>
      </SessionBootstrap>,
    );

    await waitFor(() =>
      expect(useElectronicInvoicingStatusStore.getState().isLoaded).toBe(false),
    );
    expect(electronicInvoicingService.getStatus).not.toHaveBeenCalled();
  });
});
