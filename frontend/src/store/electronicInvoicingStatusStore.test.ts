// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  useElectronicInvoicingStatusStore,
  SRI_CONNECTIVITY_TTL_MS,
} from "./electronicInvoicingStatusStore";
import { electronicInvoicingService } from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";
import type { ElectronicInvoicingStatusDto } from "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

// ELECTRONIC-INVOICING-SRI-CONNECTIVITY-CHECK-SCOPE-01: `refresh()` (bootstrap global) nunca debe
// pedir ping externo al SRI; `refreshConnectivity()` (acotado a Ventas) sí lo pide, pero cachea
// el resultado por SRI_CONNECTIVITY_TTL_MS y deduplica llamadas concurrentes — para que entrar a
// /sales, cambiar de cliente/producto, o reintentar emitir no multiplique pings al SRI.

vi.mock(
  "../modules/configuracion/facturacionElectronica/api/electronicInvoicingService",
  () => ({
    electronicInvoicingService: {
      getStatus: vi.fn(),
    },
  }),
);

function baseStatus(
  overrides: Partial<ElectronicInvoicingStatusDto> = {},
): ElectronicInvoicingStatusDto {
  return {
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
    ...overrides,
  };
}

function resetStore() {
  useElectronicInvoicingStatusStore.setState({
    status: null,
    isLoaded: false,
    isLoading: false,
    connectivityCheckedAt: null,
  });
}

beforeEach(() => {
  vi.clearAllMocks();
  vi.useRealTimers();
  resetStore();
});

afterEach(() => {
  resetStore();
});

describe("electronicInvoicingStatusStore.refresh — estado local (bootstrap global)", () => {
  it("llama getStatus sin checkConnectivity (nunca pide ping externo al SRI)", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(
      baseStatus(),
    );

    await useElectronicInvoicingStatusStore.getState().refresh();

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledWith();
    expect(useElectronicInvoicingStatusStore.getState().isLoaded).toBe(true);
    expect(useElectronicInvoicingStatusStore.getState().connectivityCheckedAt).toBeNull();
  });
});

describe("electronicInvoicingStatusStore.refreshConnectivity — check explícito (Ventas)", () => {
  it("llama getStatus con checkConnectivity: true", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(
      baseStatus({ sriAvailability: "Available", status: "Ready" }),
    );

    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledWith({
      checkConnectivity: true,
    });
    expect(useElectronicInvoicingStatusStore.getState().status?.sriAvailability).toBe(
      "Available",
    );
    expect(
      useElectronicInvoicingStatusStore.getState().connectivityCheckedAt,
    ).not.toBeNull();
  });

  it("dentro del TTL, una segunda llamada es no-op (no repite el ping)", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(baseStatus());

    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledTimes(1);
  });

  it("vencido el TTL, una nueva llamada sí dispara un nuevo check", async () => {
    vi.useFakeTimers();
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(baseStatus());

    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    vi.advanceTimersByTime(SRI_CONNECTIVITY_TTL_MS + 1);
    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledTimes(2);
    vi.useRealTimers();
  });

  it("force:true dispara un nuevo check aunque el TTL siga vigente", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(baseStatus());

    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    await useElectronicInvoicingStatusStore
      .getState()
      .refreshConnectivity({ force: true });

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledTimes(2);
  });

  it("deduplica llamadas concurrentes (mismo patrón que refresh)", async () => {
    let resolveFetch: (dto: ElectronicInvoicingStatusDto) => void;
    vi.mocked(electronicInvoicingService.getStatus).mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );

    const p1 = useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    const p2 = useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    resolveFetch!(baseStatus());
    await Promise.all([p1, p2]);

    expect(electronicInvoicingService.getStatus).toHaveBeenCalledTimes(1);
  });

  it("si falla, apaga isLoading sin dejar connectivityCheckedAt como si hubiera verificado", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockRejectedValue(
      new Error("network error"),
    );

    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();

    expect(useElectronicInvoicingStatusStore.getState().isLoading).toBe(false);
    expect(
      useElectronicInvoicingStatusStore.getState().connectivityCheckedAt,
    ).toBeNull();
  });
});

describe("electronicInvoicingStatusStore.clear", () => {
  it("limpia también connectivityCheckedAt", async () => {
    vi.mocked(electronicInvoicingService.getStatus).mockResolvedValue(baseStatus());
    await useElectronicInvoicingStatusStore.getState().refreshConnectivity();
    expect(
      useElectronicInvoicingStatusStore.getState().connectivityCheckedAt,
    ).not.toBeNull();

    useElectronicInvoicingStatusStore.getState().clear();

    expect(useElectronicInvoicingStatusStore.getState().status).toBeNull();
    expect(useElectronicInvoicingStatusStore.getState().connectivityCheckedAt).toBeNull();
  });
});
