// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, waitFor } from "@testing-library/react";
import { SessionBootstrap } from "./SessionBootstrap";
import { useAuthStore } from "../store/authStore";
import { useSessionStore } from "../store/sessionStore";
import { useElectronicInvoicingStatusStore } from "../store/electronicInvoicingStatusStore";
import { setAccessToken, clearAccessToken } from "../lib/session/authTokenMemory";
import { sessionService } from "../modules/session/api/sessionService";
import { electronicInvoicingService } from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";
import { apiGet } from "../modules/lib/apiEnvelope";
import { TEST_PRECISION_POLICY } from "../test/precisionPolicyFixture";
import { usePrecisionPolicy } from "../hooks/usePrecisionPolicy";
import { getPrecisionPolicySnapshot, loadPrecisionPolicy } from "../lib/config/precisionPolicy.config";

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

vi.mock("../modules/lib/apiEnvelope", () => ({ apiGet: vi.fn() }));
vi.mock("../lib/config/precisionPolicy.config", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../lib/config/precisionPolicy.config")>();
  return { ...actual, loadPrecisionPolicy: vi.fn(actual.loadPrecisionPolicy) };
});

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
  vi.mocked(apiGet).mockResolvedValue(TEST_PRECISION_POLICY);
  vi.mocked(sessionService.getContext).mockResolvedValue({
    identity: { userId: "u1", fullName: "Test", email: "t@t.com" },
    tenant: { id: "t1", displayName: "Tenant", logo: null, timezone: "America/Guayaquil" },
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
  cleanup();
  resetStores();
});

describe("SessionBootstrap — estado de facturación electrónica", () => {
  it("al autenticarse, llama getStatus() sin checkConnectivity (nunca pide ping externo al SRI)", async () => {
    setAccessToken("fake-token");
    useAuthStore.setState({ hasHydrated: true, isAuthenticated: true,
      user: { companyId: "c1" } as never,
    });

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
    expect(loadPrecisionPolicy).toHaveBeenCalled();
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

describe("SessionBootstrap — política de precisión (fail-closed)", () => {
  it("con empresa: no renderiza la app hasta cargar la política; si falla muestra error y no la app", async () => {
    setAccessToken("fake-token");
    useAuthStore.setState({
      hasHydrated: true,
      isAuthenticated: true,
      user: { companyId: "c1" } as never,
    });
    vi.mocked(loadPrecisionPolicy).mockRejectedValueOnce(new Error("down"));

    const { queryByText, findByRole } = render(
      <SessionBootstrap>
        <div>app</div>
      </SessionBootstrap>,
    );
    await findByRole("alert");
    expect(queryByText("app")).toBeNull();
  });

  it("con empresa: renderiza la app cuando la política carga", async () => {
    setAccessToken("fake-token");
    useAuthStore.setState({
      hasHydrated: true,
      isAuthenticated: true,
      user: { companyId: "c1" } as never,
    });
    const { findByText } = render(
      <SessionBootstrap>
        <div>app</div>
      </SessionBootstrap>,
    );
    await findByText("app");
  });
});

// El mismo consumidor fail-closed que usa DashboardPage; no sustituye la policy por un mock.
function OperationalDashboard() {
  const policy = usePrecisionPolicy();
  return <div>dashboard:{policy.quantityDecimals}</div>;
}
function ModeScreen() {
  const companyId = useAuthStore((s) => s.user?.companyId);
  return companyId ? <OperationalDashboard /> : <div>admin global</div>;
}

it.each(["c1", "c2"])("BUG REAL: global / c1 / global / %s sin F5 espera una nueva policy", async (targetCompany) => {
  setAccessToken("fake-token");
  useAuthStore.setState({ hasHydrated: true, isAuthenticated: true, user: { companyId: null } as never });
  const view = render(<SessionBootstrap><ModeScreen /></SessionBootstrap>);
  await view.findByText("admin global");
  await act(async () => { useAuthStore.setState({ user: { companyId: "c1" } as never }); });
  await view.findByText(`dashboard:${TEST_PRECISION_POLICY.quantityDecimals}`);
  await act(async () => { useAuthStore.setState({ user: { companyId: null } as never }); });
  await view.findByText("admin global");
  expect(getPrecisionPolicySnapshot().loaded).toBe(false);

  let resolvePolicy!: (value: typeof TEST_PRECISION_POLICY) => void;
  vi.mocked(apiGet).mockReturnValueOnce(new Promise((resolve) => { resolvePolicy = resolve; }));
  await act(async () => { useAuthStore.setState({ user: { companyId: targetCompany } as never }); });
  expect(view.queryByText(/^dashboard:/)).toBeNull();
  await act(async () => { resolvePolicy({ ...TEST_PRECISION_POLICY, quantityDecimals: 3 }); });
  await view.findByText("dashboard:3");
});
