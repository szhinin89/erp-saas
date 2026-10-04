// @vitest-environment jsdom
import { describe, it, expect, vi, afterEach } from "vitest";
import { render, screen, cleanup, fireEvent, waitFor } from "@testing-library/react";
import type { SalesInvoiceDto } from "../api/salesService";
import { issueStepsFor } from "../utils/salesIssueSteps";

vi.mock("../../configuracion/facades/operationalPreferencesLookupFacade", () => ({
  operationalPreferencesLookupFacade: {
    getPreferences: () =>
      Promise.resolve({ printing: { salesReceiptMode: "AskBeforePrint", salesReceiptCopies: 1 } }),
  },
}));

const printMocks = vi.hoisted(() => ({
  getReceiptPrintPayload: vi.fn(),
  buildReceiptPrintJobRequest: vi.fn(),
  submitReceiptPrintJob: vi.fn(),
}));
vi.mock("../api/salesService", () => ({
  salesService: { getReceiptPrintPayload: printMocks.getReceiptPrintPayload },
}));
vi.mock("../api/printAgentClient", () => ({
  buildReceiptPrintJobRequest: printMocks.buildReceiptPrintJobRequest,
  submitReceiptPrintJob: printMocks.submitReceiptPrintJob,
  retryReceiptPrintJob: vi.fn(),
  printAgentUserMessage: () => "error",
  PrintAgentError: class extends Error {},
}));

import { SalesIssueModal } from "./SalesIssueModal";

// POS-EMISSION-VISIBILITY-01 / POS-ISSUE-REAL-PROGRESS-01 — el modal de emisión no simula pasos
// electrónicos y el resultado se decide por el snapshot de la factura emitida.

afterEach(() => cleanup());

function cashPayment(amount: number, tendered: number | null) {
  return {
    id: "p-cash",
    paymentMethodId: "pm-cash",
    paymentMethodCode: "EFECTIVO",
    paymentMethodName: "Efectivo",
    amount,
    reference: null,
    cardDetail: null,
    transferDetail: null,
    chequeDetail: null,
    tenderedAmount: tendered,
    changeAmount: tendered === null ? null : Math.round((tendered - amount) * 100) / 100,
  };
}

function cardPayment(amount: number) {
  return { ...cashPayment(amount, null), id: "p-card", paymentMethodId: "pm-card", paymentMethodName: "Tarjeta" };
}

function invoice(overrides: Partial<SalesInvoiceDto> = {}): SalesInvoiceDto {
  return {
    id: "inv-1",
    invoiceNumber: "001-001-000000010",
    emissionType: "Physical",
    electronicStatus: "None",
    accessKey: null,
    authorizationNumber: null,
    authorizationDate: null,
    createdAt: "2026-10-03T15:00:00Z",
    grandTotal: 14.66,
    electronicIssueError: null,
    payments: [cashPayment(14.66, 20)],
    ...overrides,
  } as SalesInvoiceDto;
}

function renderModal(props: Partial<Parameters<typeof SalesIssueModal>[0]> = {}) {
  return render(
    <SalesIssueModal
      phase="success"
      isElectronic={false}
      customerName="Consumidor Final"
      lineCount={1}
      subtotal={12.75}
      discount={0}
      vat={1.91}
      total={14.66}
      steps={issueStepsFor(false)}
      stepIndex={0}
      result={invoice()}
      ridePending={false}
      xmlDownloading={false}
      onPrintRide={vi.fn()}
      onDownloadPdf={vi.fn()}
      onDownloadXml={vi.fn()}
      error={null}
      onRetry={vi.fn()}
      onCancel={vi.fn()}
      onConfirm={vi.fn()}
      onNewSale={vi.fn()}
      {...props}
    />,
  );
}

describe("SalesIssueModal — procesamiento sin progreso simulado", () => {
  it("física: pasos reales, sin XML / Firma / SRI", () => {
    renderModal({ phase: "processing", steps: issueStepsFor(false), stepIndex: 2 });
    expect(screen.getByText("Procesando venta...")).toBeTruthy();
    expect(screen.getByText("Registrando venta")).toBeTruthy();
    for (const fake of [/XML/, /Firmando/, /SRI/, /autorización/i]) {
      expect(screen.queryByText(fake)).toBeNull();
    }
  });

  it("electrónica: un único paso 'Procesando factura electrónica', sin telemetría inventada", () => {
    renderModal({
      phase: "processing",
      isElectronic: true,
      steps: issueStepsFor(true),
      stepIndex: 2,
    });
    expect(screen.getAllByText(/Procesando factura electrónica/).length).toBeGreaterThan(0);
    for (const fake of ["Generando XML", "Firmando", "Enviando al SRI", "Consultando autorización"]) {
      expect(screen.queryByText(fake)).toBeNull();
    }
  });
});

describe("SalesIssueModal — resultado", () => {
  it("efectivo: 'Venta completada' con Total / Recibido / Vuelto leídos de los pagos persistidos", () => {
    renderModal();
    expect(screen.getByText("Venta completada")).toBeTruthy();
    expect(screen.getByText("Recibido").nextElementSibling?.textContent).toBe("$20.00");
    expect(screen.getByText("Vuelto").nextElementSibling?.textContent).toBe("$5.34");
  });

  it("multipago: Recibido / Vuelto solo de la porción efectivo", () => {
    renderModal({ result: invoice({ payments: [cardPayment(10), cashPayment(4.66, 10)] }) });
    expect(screen.getByText("Recibido").nextElementSibling?.textContent).toBe("$10.00");
    expect(screen.getByText("Vuelto").nextElementSibling?.textContent).toBe("$5.34");
  });

  it("sin efectivo: no muestra Recibido / Vuelto", () => {
    renderModal({ result: invoice({ payments: [cardPayment(14.66)] }) });
    expect(screen.queryByText("Recibido")).toBeNull();
    expect(screen.queryByText("Vuelto")).toBeNull();
  });

  it("física: nada de XML / RIDE / estado SRI — solo tirilla y nueva venta", () => {
    renderModal();
    expect(screen.queryByText("Imprimir RIDE")).toBeNull();
    expect(screen.queryByText("Descargar PDF")).toBeNull();
    expect(screen.queryByText("Descargar XML")).toBeNull();
    expect(screen.queryByText("Estado electrónico")).toBeNull();
    expect(screen.queryByText(/pendiente de autorización/)).toBeNull();
    expect(screen.getByText("Imprimir tirilla")).toBeTruthy();
    expect(screen.getByText("Nueva venta")).toBeTruthy();
  });

  it("física aunque el DTO traiga clave de acceso: no se muestra (manda el tipo de la factura)", () => {
    renderModal({ result: invoice({ accessKey: "1234567890" }) });
    expect(screen.queryByText("Clave de acceso")).toBeNull();
  });

  it("electrónica autorizada: estado real + RIDE + XML", () => {
    renderModal({
      isElectronic: true,
      result: invoice({
        emissionType: "Electronic",
        electronicStatus: "Authorized",
        accessKey: "0310202601179001",
        authorizationNumber: "0310202601179001",
      }),
    });
    expect(screen.getByText("Estado electrónico")).toBeTruthy();
    expect(screen.getByText("Autorizado")).toBeTruthy();
    expect(screen.getByText("Imprimir RIDE")).toBeTruthy();
    expect(screen.getByText("Descargar XML")).toBeTruthy();
  });

  it("electrónica pendiente: estado real + aviso, sin RIDE (aún no hay XML autorizado)", () => {
    renderModal({
      isElectronic: true,
      result: invoice({ emissionType: "Electronic", electronicStatus: "Sent" }),
    });
    expect(screen.getByText("Enviado")).toBeTruthy();
    expect(screen.getByText(/pendiente de autorización/)).toBeTruthy();
    expect(screen.queryByText("Imprimir RIDE")).toBeNull();
    expect(screen.getByText("Descargar XML")).toBeTruthy();
  });
});

describe("SalesIssueModal — tirilla desde el payload oficial persistido", () => {
  it("imprime Recibido / Vuelto tal como vienen del backend (misma fuente que una reimpresión)", async () => {
    const backendPayload = { cashReceived: 20, cashChange: 5.34 };
    printMocks.getReceiptPrintPayload.mockResolvedValue(backendPayload);
    printMocks.buildReceiptPrintJobRequest.mockReturnValue({ jobId: "invoice-inv-1-receipt" });
    printMocks.submitReceiptPrintJob.mockResolvedValue({ duplicate: false });
    printMocks.buildReceiptPrintJobRequest.mockClear();

    renderModal();
    fireEvent.click(screen.getByText("Imprimir tirilla"));

    await waitFor(() => expect(printMocks.buildReceiptPrintJobRequest).toHaveBeenCalled());
    expect(printMocks.buildReceiptPrintJobRequest.mock.calls[0][0]).toBe(backendPayload);
  });

  it("venta sin efectivo: el payload del backend no trae Recibido/Vuelto y el POS no los inventa", async () => {
    const backendPayload = { cashReceived: null, cashChange: null };
    printMocks.getReceiptPrintPayload.mockResolvedValue(backendPayload);
    printMocks.buildReceiptPrintJobRequest.mockReturnValue({ jobId: "invoice-inv-1-receipt" });
    printMocks.submitReceiptPrintJob.mockResolvedValue({ duplicate: false });
    printMocks.buildReceiptPrintJobRequest.mockClear();

    renderModal({ result: invoice({ payments: [cardPayment(14.66)] }) });
    fireEvent.click(screen.getByText("Imprimir tirilla"));

    await waitFor(() => expect(printMocks.buildReceiptPrintJobRequest).toHaveBeenCalled());
    const payload = printMocks.buildReceiptPrintJobRequest.mock.calls[0][0];
    expect(payload.cashReceived).toBeNull();
    expect(payload.cashChange).toBeNull();
  });
});
