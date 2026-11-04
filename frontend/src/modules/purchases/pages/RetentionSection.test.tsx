// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { RetentionSection } from "./PurchasesPage";
import { useElectronicInvoicingStatusStore } from "../../../store/electronicInvoicingStatusStore";

// PURCHASE-PAYABLE-E2E-01 — una compra confirmada sin retención mostraba la clave i18n cruda
// "purchases.retention.noneIssued" en lugar del texto.

type Ctx = Parameters<typeof RetentionSection>[0]["ctx"];

afterEach(cleanup);

describe("RetentionSection — compra confirmada", () => {
  it("sin retención emitida muestra el texto traducido", () => {
    const ctx = {
      editing: { id: "p-1", status: "Confirmed" },
      retention: null,
      electronicPending: false,
      handleViewRetentionXml: vi.fn(),
      handleViewRetentionRidePdf: vi.fn(),
    } as unknown as Ctx;

    render(
      <MemoryRouter>
        <I18nProvider>
          <RetentionSection ctx={ctx} />
        </I18nProvider>
      </MemoryRouter>,
    );

    expect(screen.getByText("No se emitió retención para esta compra.")).toBeTruthy();
    expect(screen.queryByText("purchases.retention.noneIssued")).toBeNull();
  });
});

it("muestra el ambiente del store solo al activar la emisión de retención en borrador", () => {
  const initialState = useElectronicInvoicingStatusStore.getState();
  const ctx = {
    editing: { id: "p-1", status: "Draft" },
    retention: null,
    whPreview: { lines: [], totalRetained: 0 },
    canApplyRetention: true,
    retentionIntent: { appliesRetention: false, emissionPointId: "", issueDate: "" },
    emissionPoints: [],
    canReadEmissionPoints: true,
  } as unknown as Ctx;
  const section = () => (
    <MemoryRouter><I18nProvider><RetentionSection ctx={ctx} /></I18nProvider></MemoryRouter>
  );
  try {
    useElectronicInvoicingStatusStore.setState({
      isLoaded: true,
      status: {
        status: "Testing", configured: true, environment: "Test", environmentName: "Pruebas",
        emissionType: "Normal", certificateInstalled: true, certificateValid: true,
        certificateExpiresAt: null, certificateDaysRemaining: null,
        sriAvailability: "Unknown", canIssue: true,
      },
    });
    const view = render(section());
    expect(screen.queryByText(/Ambiente: PRUEBAS/i)).toBeNull();
    ctx.retentionIntent = { ...ctx.retentionIntent, appliesRetention: true };
    view.rerender(section());
    expect(screen.getByText(/Ambiente: PRUEBAS/i)).toBeTruthy();
    act(() => {
      useElectronicInvoicingStatusStore.setState({
        status: { ...useElectronicInvoicingStatusStore.getState().status!, status: "Ready", environment: "Production", environmentName: "Producción" },
      });
    });
    expect(screen.queryByText(/Ambiente: PRUEBAS/i)).toBeNull();
    expect(screen.getByText(/Ambiente: PRODUCCIÓN/i)).toBeTruthy();
    ctx.retentionIntent = { ...ctx.retentionIntent, appliesRetention: false };
    view.rerender(section());
    expect(screen.queryByText(/Ambiente: PRODUCCIÓN/i)).toBeNull();
    ctx.editing = { ...ctx.editing!, status: "Confirmed" };
    ctx.retentionIntent = { ...ctx.retentionIntent, appliesRetention: true };
    view.rerender(section());
    expect(screen.queryByText(/Ambiente: PRODUCCIÓN/i)).toBeNull();
  } finally {
    cleanup();
    useElectronicInvoicingStatusStore.setState(initialState, true);
  }
});
