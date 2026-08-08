// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi, beforeEach } from "vitest";
import { render, screen, cleanup, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import type { ElectronicDocumentDiagnosticDto } from "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticTypes";

const getDiagnosticBySourceMock = vi.fn();
vi.mock(
  "../../../components/zh/electronicDocuments/electronicDocumentDiagnosticService",
  () => ({
    electronicDocumentDiagnosticService: {
      getDiagnosticBySource: (...a: unknown[]) =>
        getDiagnosticBySourceMock(...a),
    },
  }),
);

const getXmlMock = vi.fn();
vi.mock("../../electronicDocuments/facades/electronicDocumentAccessFacade", () => ({
  electronicDocumentAccessFacade: {
    getXml: (...a: unknown[]) => getXmlMock(...a),
  },
}));

const messageErrorMock = vi.fn();
vi.mock("../../../lib/messages", () => ({
  message: {
    error: (...a: unknown[]) => messageErrorMock(...a),
    success: vi.fn(),
    warning: vi.fn(),
  },
}));

const handleViewRideMock = vi.fn();
const handleDownloadRideMock = vi.fn();
vi.mock("../hooks/useRideActions", () => ({
  useRideActions: () => ({
    ridePending: false,
    handleViewRide: handleViewRideMock,
    handleDownloadRide: handleDownloadRideMock,
    handleRegenerateRide: vi.fn(),
  }),
}));

import { SalesReturnCreditNoteSection } from "./SalesReturnCreditNoteSection";

function diagnostic(
  overrides: Partial<ElectronicDocumentDiagnosticDto> = {},
): ElectronicDocumentDiagnosticDto {
  return {
    currentState: "Authorized",
    environment: "2",
    lastAttemptUtc: "2026-07-30T10:00:00Z",
    messages: [
      {
        code: null,
        messageType: "INFORMATIVO",
        message: "Comprobante autorizado.",
        additionalInfo: null,
        occurredAtUtc: "2026-07-30T10:00:00Z",
      },
    ],
    timeline: [],
    technicalInfo: {
      accessKey: "1".repeat(49),
      authorizationNumber: "1".repeat(49),
      environment: "2",
      authorizationDate: "2026-07-30T10:00:00Z",
      retryCount: 0,
      lastAttemptUtc: "2026-07-30T10:00:00Z",
      correlationId: null,
    },
    xmlDraftAvailable: true,
    xmlSignedAvailable: true,
    xmlAuthorizedAvailable: true,
    ...overrides,
  };
}

function renderSection(props?: { salesReturnId?: string; returnNumber?: string }) {
  return render(
    <I18nProvider>
      <SalesReturnCreditNoteSection
        salesReturnId={props?.salesReturnId ?? "return-1"}
        returnNumber={props?.returnNumber ?? "DEV-001-001-000000001"}
      />
    </I18nProvider>,
  );
}

describe("SalesReturnCreditNoteSection", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  afterEach(() => {
    cleanup();
  });

  it("consulta el diagnóstico con sourceModule=Sales y el id de la devolución", async () => {
    getDiagnosticBySourceMock.mockResolvedValue(diagnostic());

    renderSection({ salesReturnId: "return-42" });

    await waitFor(() =>
      expect(getDiagnosticBySourceMock).toHaveBeenCalledWith(
        "Sales",
        "return-42",
      ),
    );
  });

  it("muestra el estado electrónico usando el badge/mapper compartido de ElectronicDocuments", async () => {
    getDiagnosticBySourceMock.mockResolvedValue(
      diagnostic({ currentState: "Signed" }),
    );

    renderSection();

    // "Firmado" es el label es-ES de electronicDocuments.monitor.state.Signed — mismo
    // diccionario/badge que usa el Monitor, no un mapeo propio de SalesReturn.
    expect(await screen.findByText("Firmado")).not.toBeNull();
  });

  it("muestra los mensajes del SRI reutilizando ElectronicDocumentSriMessages", async () => {
    getDiagnosticBySourceMock.mockResolvedValue(diagnostic());

    renderSection();

    expect(await screen.findByText("Comprobante autorizado.")).not.toBeNull();
  });

  it('"Refrescar estado" vuelve a consultar el diagnóstico', async () => {
    getDiagnosticBySourceMock.mockResolvedValue(diagnostic());

    renderSection({ salesReturnId: "return-7" });
    await screen.findByText("Comprobante autorizado.");
    expect(getDiagnosticBySourceMock).toHaveBeenCalledTimes(1);

    screen.getByText("Refrescar estado").click();

    await waitFor(() =>
      expect(getDiagnosticBySourceMock).toHaveBeenCalledTimes(2),
    );
    expect(getDiagnosticBySourceMock).toHaveBeenLastCalledWith(
      "Sales",
      "return-7",
    );
  });

  it("descarga el RIDE usando useRideActions con el id real y el número de devolución", async () => {
    getDiagnosticBySourceMock.mockResolvedValue(diagnostic());

    renderSection({
      salesReturnId: "return-99",
      returnNumber: "DEV-001-001-000000099",
    });

    const btn = await screen.findByText("Descargar RIDE");
    btn.click();

    expect(handleDownloadRideMock).toHaveBeenCalledWith(
      "return-99",
      "DEV-001-001-000000099",
    );
  });

  it('"Ver RIDE" usa useRideActions con el id real de la devolución', async () => {
    getDiagnosticBySourceMock.mockResolvedValue(diagnostic());

    renderSection({ salesReturnId: "return-55" });

    const btn = await screen.findByText("Ver RIDE");
    btn.click();

    expect(handleViewRideMock).toHaveBeenCalledWith("return-55");
  });

  it("un 404 (sin documento electrónico registrado) se trata como estado vacío, no como error", async () => {
    getDiagnosticBySourceMock.mockRejectedValue({
      response: { status: 404 },
    });

    renderSection();

    expect(
      await screen.findByText(
        "Todavía no se ha registrado un documento electrónico para esta devolución.",
      ),
    ).not.toBeNull();
    expect(messageErrorMock).not.toHaveBeenCalled();
    // Sin diagnóstico no hay acciones de RIDE que ofrecer.
    expect(screen.queryByText("Descargar RIDE")).toBeNull();
  });

  it("un error real de consulta se muestra mediante message.error sin romper la página", async () => {
    getDiagnosticBySourceMock.mockRejectedValue(new Error("network down"));

    renderSection();

    await waitFor(() => expect(messageErrorMock).toHaveBeenCalledTimes(1));
    // La página sigue montada y utilizable (no crashea) — el botón de refrescar sigue presente.
    expect(screen.getByText("Refrescar estado")).not.toBeNull();
  });
});
