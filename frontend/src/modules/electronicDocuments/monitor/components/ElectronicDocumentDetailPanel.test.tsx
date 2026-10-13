// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ElectronicDocumentDetailPanel } from "./ElectronicDocumentDetailPanel";
import type { ElectronicDocumentDetailDto } from "../api/electronicDocumentsMonitorService";

/**
 * Monitor electrónico — acción "Descargar RIDE": reutiliza el hook RIDE genérico (vía
 * `onDownloadRide`), solo con `ride.view`, nunca para Retención (sin RIDE autorizado en el
 * pipeline genérico). "Reenviar correo" sigue deshabilitado.
 */

const DOWNLOAD_RIDE = /Descargar RIDE|Download RIDE/;
const RESEND_EMAIL = /Reenviar correo|Resend email/;
const RETENTION_NOTICE =
  /RIDE autorizado de retenciones|authorized RIDE for retentions/;

function detail(
  overrides: Partial<ElectronicDocumentDetailDto> = {},
  currentState = "Authorized",
): ElectronicDocumentDetailDto {
  return {
    id: "edoc-1",
    companyId: "company-1",
    companyName: "ZH Technologies",
    companyTaxId: "0999999999001",
    documentType: "Invoice",
    sourceModule: "Sales",
    sourceEntityId: "invoice-1",
    documentNumber: "001-001-000000123",
    counterpartyName: "Juan Pérez",
    createdAt: "2026-10-01T10:00:00Z",
    updatedAt: null,
    observations: null,
    diagnostic: {
      currentState,
      environment: "2",
      lastAttemptUtc: null,
      messages: [],
      timeline: [],
      technicalInfo: {
        accessKey: "1".repeat(49),
        authorizationNumber: "1".repeat(49),
        environment: "2",
        authorizationDate: "2026-10-01T10:00:00Z",
        retryCount: 0,
        lastAttemptUtc: null,
        correlationId: null,
      },
      xmlDraftAvailable: true,
      xmlSignedAvailable: true,
      xmlAuthorizedAvailable: true,
    },
    ...overrides,
  };
}

function renderPanel(
  props: Partial<Parameters<typeof ElectronicDocumentDetailPanel>[0]> = {},
) {
  const onDownloadRide = vi.fn();
  render(
    <I18nProvider>
      <ElectronicDocumentDetailPanel
        open
        detail={detail()}
        loading={false}
        error={null}
        xmlContent={null}
        xmlVariant={null}
        xmlLoading={false}
        xmlError={null}
        canRetry={false}
        retryLoading={false}
        canDownloadRide
        rideDownloading={false}
        onClose={vi.fn()}
        onViewXml={vi.fn()}
        onRetryNow={vi.fn()}
        onDownloadRide={onDownloadRide}
        {...props}
      />
    </I18nProvider>,
  );
  return { onDownloadRide };
}

describe("ElectronicDocumentDetailPanel — Descargar RIDE", () => {
  afterEach(() => cleanup());

  it("Authorized + ride.view: botón habilitado y el clic llama al handler", () => {
    const { onDownloadRide } = renderPanel();

    const btn = screen.getByRole("button", { name: DOWNLOAD_RIDE });
    expect((btn as HTMLButtonElement).disabled).toBe(false);
    fireEvent.click(btn);
    expect(onDownloadRide).toHaveBeenCalledTimes(1);
  });

  it("sin ride.view: la acción no se muestra (no provoca 403)", () => {
    renderPanel({ canDownloadRide: false });

    expect(
      screen.queryByRole("button", { name: DOWNLOAD_RIDE }),
    ).toBeNull();
  });

  it("Retention: deshabilitado con aviso específico y nunca llama al handler", () => {
    const { onDownloadRide } = renderPanel({
      detail: detail({ documentType: "Retention", sourceModule: "Retentions" }),
    });

    const btn = screen.getByRole("button", { name: DOWNLOAD_RIDE });
    expect((btn as HTMLButtonElement).disabled).toBe(true);
    fireEvent.click(btn);
    expect(onDownloadRide).not.toHaveBeenCalled();
    expect(screen.getByText(RETENTION_NOTICE)).toBeTruthy();
  });

  it("no Authorized: la acción no está disponible", () => {
    renderPanel({ detail: detail({}, "Received") });

    expect(
      screen.queryByRole("button", { name: DOWNLOAD_RIDE }),
    ).toBeNull();
  });

  it("descarga en curso: deshabilitado para evitar doble clic", () => {
    renderPanel({ rideDownloading: true });

    const btn = screen.getByRole("button", { name: DOWNLOAD_RIDE });
    expect((btn as HTMLButtonElement).disabled).toBe(true);
  });

  it("Reenviar correo sigue deshabilitado", () => {
    renderPanel();

    const btn = screen.getByRole("button", { name: RESEND_EMAIL });
    expect((btn as HTMLButtonElement).disabled).toBe(true);
  });
});
