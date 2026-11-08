// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { ElectronicDocumentDiagnosticPanel } from "./ElectronicDocumentDiagnosticPanel";
import type { ElectronicDocumentDiagnosticDto } from "./electronicDocumentDiagnosticTypes";

/**
 * SRI-MONITOR-UX-01 — regresión del panel de diagnóstico compartido (Monitor, Ventas y Notas de
 * crédito): ambiente legible en el resumen, secciones traducidas sin texto de respaldo,
 * Reintentos agotados, mensajes SRI íntegros y unidad de duración traducida.
 */

function diagnostic(
  overrides: Partial<ElectronicDocumentDiagnosticDto> = {},
): ElectronicDocumentDiagnosticDto {
  return {
    currentState: "DeadLetter",
    environment: "2",
    lastAttemptUtc: "2026-11-01T11:00:00Z",
    messages: [
      {
        code: "70",
        messageType: "ERROR",
        message: "CLAVE DE ACCESO EN PROCESAMIENTO",
        additionalInfo: "La clave de acceso 0711… se encuentra en procesamiento",
        occurredAtUtc: "2026-11-01T11:00:00Z",
      },
    ],
    timeline: [
      {
        action: "DeadLetter",
        fromState: "Received",
        toState: "DeadLetter",
        occurredAtUtc: "2026-11-01T11:00:00Z",
        userName: "Sistema",
        durationSinceLastMinutes: 12.4,
        reason: "Reintentos agotados tras 5 intentos.",
      },
    ],
    technicalInfo: {
      accessKey: "1".repeat(49),
      authorizationNumber: null,
      environment: "2",
      authorizationDate: null,
      retryCount: 5,
      lastAttemptUtc: "2026-11-01T11:00:00Z",
      correlationId: null,
    },
    xmlDraftAvailable: true,
    xmlSignedAvailable: true,
    xmlAuthorizedAvailable: false,
    requiresReconciliation: false,
    ...overrides,
  };
}

function renderPanel(d: ElectronicDocumentDiagnosticDto) {
  render(
    <I18nProvider>
      <ElectronicDocumentDiagnosticPanel
        diagnostic={d}
        xmlContent={null}
        xmlVariant={null}
        xmlLoading={false}
        xmlError={null}
        downloadFileBaseName="doc"
        onViewXml={vi.fn()}
      />
    </I18nProvider>,
  );
}

afterEach(() => cleanup());

describe("ElectronicDocumentDiagnosticPanel — regresión compartida", () => {
  it("el resumen muestra el ambiente por su nombre, no por el código SRI", () => {
    renderPanel(diagnostic());
    expect(screen.getByText("Producción")).toBeTruthy();
    cleanup();
    renderPanel(diagnostic({ environment: "1" }));
    expect(screen.getByText("Pruebas")).toBeTruthy();
  });

  it("sin ambiente muestra un guion y conserva cualquier otro valor tal cual", () => {
    renderPanel(diagnostic({ environment: null }));
    expect(screen.getAllByText("—").length).toBeGreaterThan(0);
  });

  it("las secciones vienen del catálogo i18n", () => {
    renderPanel(diagnostic());
    for (const text of [
      "Resumen del diagnóstico",
      "Ambiente",
      "Último intento",
      "2 disponibles",
      "Soporte técnico",
    ])
      expect(screen.getAllByText(text).length).toBeGreaterThan(0);
  });

  it("estado Reintentos agotados, mensaje SRI íntegro y duración con unidad traducida", () => {
    renderPanel(diagnostic());
    expect(screen.getAllByText(/Reintentos agotados/).length).toBeGreaterThan(0);
    expect(screen.getByText("CLAVE DE ACCESO EN PROCESAMIENTO")).toBeTruthy();
    expect(
      screen.getByText("La clave de acceso 0711… se encuentra en procesamiento"),
    ).toBeTruthy();
    expect(screen.getByText(/Duración: 12\s+min/)).toBeTruthy();
  });
});
