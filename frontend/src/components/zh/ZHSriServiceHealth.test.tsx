// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { ZHSriServiceHealth } from "./ZHSriServiceHealth";
import { RetentionSriServiceHealth } from "../../modules/retentions/components/RetentionSriServiceHealth";
import { useElectronicInvoicingStatusStore } from "../../store/electronicInvoicingStatusStore";
import { electronicInvoicingService } from "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";
import type { SriServiceHealthDto } from "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService";

vi.mock(
  "../../modules/configuracion/facturacionElectronica/api/electronicInvoicingService",
  () => ({ electronicInvoicingService: { getStatus: vi.fn(), getSriHealth: vi.fn() } }),
);

function health(overrides: Partial<SriServiceHealthDto> = {}): SriServiceHealthDto {
  return {
    environment: "Production",
    environmentName: "Producción",
    reception: {
      state: "Unavailable",
      reason: "ConsecutiveTransportFailures",
      evidence: "Call",
      lastObservedAtUtc: "2026-11-01T15:00:00Z",
    },
    authorization: {
      state: "Available",
      reason: "RespondedWithoutDecision",
      evidence: "Call",
      lastObservedAtUtc: "2026-11-01T14:58:00Z",
    },
    ...overrides,
  };
}

function seed(dto: SriServiceHealthDto) {
  useElectronicInvoicingStatusStore.setState({ health: dto, healthLoadedAt: Date.now() });
}

beforeEach(() => {
  vi.clearAllMocks();
  useElectronicInvoicingStatusStore.getState().clear();
});

afterEach(() => {
  cleanup();
  useElectronicInvoicingStatusStore.getState().clear();
});

describe("ZHSriServiceHealth", () => {
  it("lee el monitor al montar y no renderiza nada sin datos", () => {
    vi.mocked(electronicInvoicingService.getSriHealth).mockReturnValue(new Promise(() => {}));

    const { container } = render(<ZHSriServiceHealth />);

    expect(electronicInvoicingService.getSriHealth).toHaveBeenCalledTimes(1);
    expect(electronicInvoicingService.getStatus).not.toHaveBeenCalled();
    expect(container.innerHTML).toBe("");
  });

  it("compacto: ambiente de Producción y un badge por servicio", () => {
    seed(health());
    render(<ZHSriServiceHealth />);

    expect(screen.getByText("Producción")).toBeTruthy();
    expect(screen.getByTestId("sri-health-reception").textContent).toBe("Recepción: No disponible");
    expect(screen.getByTestId("sri-health-authorization").textContent).toBe("Autorización: Disponible");
  });

  it("distingue Pruebas de Producción", () => {
    seed(health({ environment: "Test", environmentName: "Pruebas" }));
    render(<ZHSriServiceHealth />);

    expect(screen.getByText("Pruebas")).toBeTruthy();
    expect(screen.queryByText("Producción")).toBeNull();
  });

  it("detalle: motivo comprensible, última observación y aclaración fiscal visibles", () => {
    seed(health());
    render(<ZHSriServiceHealth variant="detail" />);

    const reception = screen.getByTestId("sri-health-reception");
    expect(reception.textContent).toContain("posible indisponibilidad del SRI");
    expect(reception.textContent).toContain("Última observación:");
    expect(screen.getByTestId("sri-health-authorization").textContent).toContain(
      "aún no tenía la decisión de autorización",
    );
    expect(screen.getByText(/no indica si un comprobante fue autorizado/)).toBeTruthy();
  });

  it("sin observaciones muestra Sin verificar y no inventa una fecha", () => {
    const unverified = {
      state: "Unverified",
      reason: "NoRecentEvidence",
      evidence: "None",
      lastObservedAtUtc: null,
    } as const;
    seed(health({ reception: unverified, authorization: unverified }));
    render(<ZHSriServiceHealth variant="detail" />);

    const reception = screen.getByTestId("sri-health-reception");
    expect(reception.textContent).toContain("Sin verificar");
    expect(reception.textContent).toContain("Sin observaciones recientes");
    expect(reception.textContent).not.toContain("Última observación");
  });

  it("no usa estilos inline", () => {
    seed(health());
    const { container } = render(<ZHSriServiceHealth variant="detail" />);

    container.querySelectorAll("*").forEach((el) => expect(el.getAttribute("style")).toBeNull());
  });
});

describe("RetentionSriServiceHealth", () => {
  it.each(["Pending", "Processing", "RequiresReconciliation"] as const)(
    "se muestra mientras la retención depende del SRI (%s)",
    (status) => {
      seed(health());
      render(<RetentionSriServiceHealth status={status} />);
      expect(screen.getByTestId("sri-health-reception")).toBeTruthy();
    },
  );

  it.each(["Authorized", "Rejected", "Discarded", "AnnulmentPending", "Annulled"] as const)(
    "no se muestra en estados donde no aporta (%s)",
    (status) => {
      seed(health());
      render(<RetentionSriServiceHealth status={status} />);
      expect(screen.queryByTestId("sri-health-reception")).toBeNull();
    },
  );
});
