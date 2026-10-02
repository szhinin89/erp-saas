// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, configure, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { RetentionAnnulmentPanel } from "./RetentionAnnulmentPanel";
import { retentionsService, type RetentionAnnulmentRequestDto } from "../api/retentionsService";
import { message } from "../../../lib/messages";

configure({ asyncUtilTimeout: 5000 });

vi.mock("../api/retentionsService", async (importOriginal) => {
  const actual = await importOriginal<typeof import("../api/retentionsService")>();
  return {
    ...actual,
    retentionsService: {
      ...actual.retentionsService,
      submitAnnulment: vi.fn(),
      verifyAnnulmentWithSri: vi.fn(),
      abandonAnnulment: vi.fn(),
      retryAnnulmentFinalization: vi.fn(),
    },
  };
});

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
}));

function annulment(overrides: Partial<RetentionAnnulmentRequestDto> = {}): RetentionAnnulmentRequestDto {
  return {
    id: "req-1",
    retentionDocumentId: "ret-1",
    sourceDocumentType: "PurchaseInvoice",
    sourceDocumentId: "purchase-1",
    status: "PendingSubmission",
    reason: "Proveedor facturó mal",
    requestedBy: "user-1",
    requestedAtUtc: "2026-10-01T15:00:00Z",
    accessKey: "1".repeat(49),
    retentionNumber: "001-001-000000007",
    retentionIssueDate: "2026-09-17",
    receptorIdentification: "1791352688001",
    receptorName: "Proveedor Retenido",
    ordinaryDeadline: "2026-10-07",
    isPastOrdinaryDeadline: false,
    submittedOn: null,
    submittedAtUtc: null,
    submissionReference: null,
    resolvedOn: null,
    resolvedAtUtc: null,
    evidenceReference: null,
    notes: null,
    finalizedAtUtc: null,
    finalizationAttempts: 0,
    lastFinalizationError: null,
    requiresFinalization: false,
    lastSriCheckAtUtc: null,
    lastSriQueryOutcome: null,
    lastSriFiscalStatus: null,
    lastSriRawStatus: null,
    sriCheckCount: 0,
    canAbandon: true,
    ...overrides,
  };
}

const submitted = (overrides: Partial<RetentionAnnulmentRequestDto> = {}) =>
  annulment({ status: "PendingSriResolution", submittedOn: "2026-09-20", canAbandon: false, ...overrides });

const checked = (fiscal: RetentionAnnulmentRequestDto["lastSriFiscalStatus"], raw: string) =>
  submitted({
    lastSriCheckAtUtc: "2026-10-01T16:00:00Z",
    lastSriQueryOutcome: "Success",
    lastSriFiscalStatus: fiscal,
    lastSriRawStatus: raw,
    sriCheckCount: 1,
    canAbandon: fiscal === "Authorized",
  });

function renderPanel(a: RetentionAnnulmentRequestDto, canOperate = true) {
  const onChanged = vi.fn();
  render(
    <I18nProvider>
      <RetentionAnnulmentPanel annulment={a} origin="purchase" canOperate={canOperate} onChanged={onChanged} />
    </I18nProvider>,
  );
  return onChanged;
}

describe("RetentionAnnulmentPanel (ZH-RETENTION-SRI-ANNULMENT-01/01B)", () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(() => cleanup());

  it("pendiente: la compra NO está anulada, datos del trámite, plazo y 'Ya presenté la solicitud'", () => {
    renderPanel(annulment());

    expect(screen.getByText("Pendiente de presentar al SRI")).toBeTruthy();
    expect(screen.getByText("La compra aún NO está anulada.")).toBeTruthy();
    expect(screen.getByText("1".repeat(49))).toBeTruthy();
    expect(screen.getByText("001-001-000000007")).toBeTruthy();
    expect(screen.getByText("Plazo ordinario: hasta el día 7 del mes siguiente a la emisión.")).toBeTruthy();
    expect(screen.queryByText("Anulada por el SRI")).toBeNull();
    expect(screen.getByRole("button", { name: "Ya presenté la solicitud" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Desistir" })).toBeTruthy();
  });

  it("test 9: el usuario nunca declara el estado fiscal (no hay control de resolución ANULADO)", () => {
    renderPanel(submitted());

    expect(screen.queryByRole("button", { name: /Registrar resolución/ })).toBeNull();
    expect(screen.queryByLabelText("Resolución del SRI")).toBeNull();
    expect(screen.queryByRole("combobox")).toBeNull();
    expect(screen.getByRole("button", { name: "Verificar estado en SRI" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Desistir" })).toBeNull();
  });

  it("sin permiso de anular el origen no hay acciones", () => {
    renderPanel(submitted(), false);

    expect(screen.queryByRole("button", { name: "Verificar estado en SRI" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Desistir" })).toBeNull();
  });

  it("AUTORIZADO: 'El SRI todavía mantiene vigente el comprobante' y permite desistir", () => {
    renderPanel(checked("Authorized", "AUTORIZADO"));

    expect(screen.getByText("El SRI todavía mantiene vigente el comprobante")).toBeTruthy();
    expect(screen.getByText("La compra aún NO está anulada.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Desistir" })).toBeTruthy();
  });

  it("PENDIENTE DE ANULAR: 'Pendiente de anulación en SRI' y no permite desistir", () => {
    renderPanel(checked("PendingAnnulment", "PENDIENTE DE ANULAR"));

    expect(screen.getAllByText("Pendiente de anulación en SRI").length).toBeGreaterThan(0);
    expect(screen.getByText("La compra aún NO está anulada.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Desistir" })).toBeNull();
  });

  it("consulta fallida: 'No fue posible verificar el estado en SRI', nunca un estado fiscal", () => {
    renderPanel(submitted({ lastSriCheckAtUtc: "2026-10-01T16:00:00Z", lastSriQueryOutcome: "Timeout", lastSriFiscalStatus: "Unknown" }));

    expect(screen.getByText("No fue posible verificar el estado en SRI")).toBeTruthy();
    expect(screen.queryByText("Anulada por el SRI")).toBeNull();
  });

  it("verificar en SRI: muestra lo que informa el SRI y propaga la solicitud actualizada", async () => {
    const updated = checked("PendingAnnulment", "PENDIENTE DE ANULAR");
    vi.mocked(retentionsService.verifyAnnulmentWithSri).mockResolvedValue(updated);
    const onChanged = renderPanel(submitted());

    fireEvent.click(screen.getByRole("button", { name: "Verificar estado en SRI" }));

    await waitFor(() => expect(onChanged).toHaveBeenCalledWith(updated));
    expect(retentionsService.verifyAnnulmentWithSri).toHaveBeenCalledWith("req-1");
    expect(message.info).toHaveBeenCalledWith("Pendiente de anulación en SRI");
  });

  it("verificar en SRI con ANULADO: se informa la compra anulada", async () => {
    const updated = annulment({ status: "Accepted", finalizedAtUtc: "2026-10-01T16:00:00Z", canAbandon: false });
    vi.mocked(retentionsService.verifyAnnulmentWithSri).mockResolvedValue(updated);
    renderPanel(submitted());

    fireEvent.click(screen.getByRole("button", { name: "Verificar estado en SRI" }));

    await waitFor(() =>
      expect(message.success).toHaveBeenCalledWith("El SRI confirmó la anulación y la compra quedó anulada."),
    );
  });

  it("verificación que no se pudo ejecutar: 'No fue posible verificar el estado en SRI'", async () => {
    vi.mocked(retentionsService.verifyAnnulmentWithSri).mockResolvedValue(
      submitted({ lastSriCheckAtUtc: "2026-10-01T16:00:00Z", lastSriQueryOutcome: "Rejected", lastSriRawStatus: "RECHAZADA" }),
    );
    renderPanel(submitted());

    fireEvent.click(screen.getByRole("button", { name: "Verificar estado en SRI" }));

    await waitFor(() => expect(message.warning).toHaveBeenCalledWith("No fue posible verificar el estado en SRI"));
  });

  it("ANULADO finalizado: solo entonces la compra se muestra anulada", () => {
    renderPanel(
      annulment({
        status: "Accepted",
        submittedOn: "2026-09-20",
        resolvedOn: "2026-09-22",
        evidenceReference: "ConsultaComprobante ANULADO 2026-09-22T16:00:00Z",
        finalizedAtUtc: "2026-09-22T16:00:00Z",
        canAbandon: false,
      }),
    );

    expect(screen.getByText("Anulada por el SRI")).toBeTruthy();
    expect(screen.getByText("El SRI confirmó la anulación y la compra quedó anulada.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Verificar estado en SRI" })).toBeNull();
  });

  it("ANULADO con finalización pendiente: no se presenta como anulada y ofrece completar", () => {
    renderPanel(
      annulment({ status: "Accepted", requiresFinalization: true, lastFinalizationError: "No se pudo anular", canAbandon: false }),
    );

    expect(screen.queryByText("El SRI confirmó la anulación y la compra quedó anulada.")).toBeNull();
    expect(screen.getByRole("button", { name: "Completar anulación de la compra" })).toBeTruthy();
  });

  it("'Ya presenté la solicitud' abre el registro de la presentación con verificación automática", () => {
    renderPanel(annulment());

    fireEvent.click(screen.getByRole("button", { name: "Ya presenté la solicitud" }));

    expect(screen.getByText("Ya presenté la solicitud en el SRI")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Registrar y verificar en SRI" })).toBeTruthy();
  });
});
