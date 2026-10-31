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
      requiresReconciliation: false,
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

  describe("caso #32 — conciliación manual", () => {
    const RECONCILE = /Registrar conciliación|Record reconciliation/;
    const uncertain = (requiresReconciliation: boolean) =>
      detail(
        {
          diagnostic: {
            ...detail({}, "Received").diagnostic,
            requiresReconciliation,
          },
        },
        "Received",
      );

    it("solo con reconcile y resultado SRI desconocido informado por el backend", () => {
      renderPanel({ detail: uncertain(false), canReconcile: true, onReconcile: vi.fn() });
      expect(screen.queryByRole("button", { name: RECONCILE })).toBeNull();
      cleanup();
      renderPanel({ detail: uncertain(true), canReconcile: false, onReconcile: vi.fn() });
      expect(screen.queryByRole("button", { name: RECONCILE })).toBeNull();
    });

    it("exige evidencia y envía referencia y observación", async () => {
      const onReconcile = vi.fn().mockResolvedValue(undefined);
      renderPanel({ detail: uncertain(true), canReconcile: true, onReconcile });

      fireEvent.click(screen.getByRole("button", { name: RECONCILE }));
      const submit = screen.getByRole("button", { name: /Registrar y consultar SRI|Record and query SRI/ });
      fireEvent.click(submit);
      expect(await screen.findByText(/referencia de la evidencia es obligatoria|evidence reference is required/i)).toBeTruthy();
      expect(onReconcile).not.toHaveBeenCalled();

      fireEvent.change(screen.getByLabelText(/Referencia de la verificación|Verification reference/), {
        target: { value: "SRI en Línea 2026-10-29 10:15" },
      });
      fireEvent.change(screen.getByLabelText(/^Observación$|^Observation$/), {
        target: { value: "Sin registro de la clave" },
      });
      fireEvent.click(submit);
      await vi.waitFor(() =>
        expect(onReconcile).toHaveBeenCalledWith({
          reference: "SRI en Línea 2026-10-29 10:15",
          observation: "Sin registro de la clave",
        }),
      );
    });
  });

  describe("R12 Fase B — corregir y retransmitir", () => {
    const RETRANSMIT = /^Corregir y retransmitir$|^Correct and retransmit$/;
    const rejected = (eligible: boolean) =>
      detail(
        {
          diagnostic: {
            ...detail({}, "Rejected").diagnostic,
            retransmission: {
              eligible,
              transmissionAttempt: 1,
              rejectionStage: "Authorization",
              rejectionCodes: eligible ? ["52"] : ["39"],
              blocks: eligible
                ? []
                : [
                    {
                      code: "CODES_NOT_CORRECTABLE",
                      message: "Los códigos SRI 39 no son corregibles con la misma clave de acceso.",
                    },
                  ],
            },
          },
        },
        "Rejected",
      );

    it("requiere el permiso y la elegibilidad calculada por el backend", () => {
      renderPanel({ detail: rejected(true), canRetransmitCorrected: false, onRetransmitCorrected: vi.fn() });
      expect(screen.queryByRole("button", { name: RETRANSMIT })).toBeNull();
      cleanup();

      renderPanel({ detail: rejected(false), canRetransmitCorrected: true, onRetransmitCorrected: vi.fn() });
      expect(screen.queryByRole("button", { name: RETRANSMIT })).toBeNull();
      expect(screen.getByText(/39 no son corregibles/)).toBeTruthy();
      cleanup();

      renderPanel({ detail: rejected(true), canRetransmitCorrected: true, onRetransmitCorrected: vi.fn() });
      expect(screen.getByRole("button", { name: RETRANSMIT })).toBeTruthy();
    });

    it("no se ofrece fuera de Rejected aunque llegue elegibilidad", () => {
      const authorized = detail({
        diagnostic: { ...detail().diagnostic, retransmission: rejected(true).diagnostic.retransmission },
      });
      renderPanel({ detail: authorized, canRetransmitCorrected: true, onRetransmitCorrected: vi.fn() });
      expect(screen.queryByRole("button", { name: RETRANSMIT })).toBeNull();
    });

    it("confirma con motivo obligatorio y reutiliza la clave de idempotencia", async () => {
      const onRetransmitCorrected = vi
        .fn()
        .mockRejectedValueOnce(new Error("network"))
        .mockResolvedValueOnce(undefined);
      renderPanel({ detail: rejected(true), canRetransmitCorrected: true, onRetransmitCorrected });

      fireEvent.click(screen.getByRole("button", { name: RETRANSMIT }));
      expect(screen.getByText(/NO AUTORIZADO/)).toBeTruthy();
      const submit = screen.getByRole("button", { name: /Confirmar y retransmitir|Confirm and retransmit/ });
      fireEvent.click(submit);
      expect(await screen.findByText(/motivo de la corrección es obligatorio|correction reason is required/i)).toBeTruthy();
      expect(onRetransmitCorrected).not.toHaveBeenCalled();

      fireEvent.change(screen.getByLabelText(/^Motivo de la corrección$|^Correction reason$/), {
        target: { value: "Dirección matriz corregida" },
      });
      fireEvent.click(submit);
      await vi.waitFor(() => expect(onRetransmitCorrected).toHaveBeenCalledTimes(1));
      fireEvent.click(submit);
      await vi.waitFor(() => expect(onRetransmitCorrected).toHaveBeenCalledTimes(2));

      const [first, second] = onRetransmitCorrected.mock.calls.map((c) => c[0]);
      expect(first.reason).toBe("Dirección matriz corregida");
      expect(first.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);
      expect(second.idempotencyKey).toBe(first.idempotencyKey);
    });
  });
});
