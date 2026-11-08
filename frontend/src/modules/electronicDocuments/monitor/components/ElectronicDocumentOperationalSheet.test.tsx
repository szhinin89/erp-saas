// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { ElectronicDocumentDetailPanel } from "./ElectronicDocumentDetailPanel";
import type { ElectronicDocumentDetailDto } from "../api/electronicDocumentsMonitorService";

/**
 * SRI-MONITOR-SHEET-01 — ficha operativa del detalle del Monitor: datos fiscales a la vista,
 * historial/mensajes/soporte plegados, archivos según disponibilidad real y significado por estado.
 */

const ACCESS_KEY = "0611202601179001234500110010010000002301234567815";
const AUTH_NUMBER = "0611202601179001234500110010010000002309876543210";

function detail(
  currentState: string,
  o: Partial<ElectronicDocumentDetailDto["diagnostic"]> = {},
  info: Partial<ElectronicDocumentDetailDto["diagnostic"]["technicalInfo"]> = {},
): ElectronicDocumentDetailDto {
  return {
    id: "edoc-1",
    companyId: "c1",
    companyName: "Supermercado Central",
    companyTaxId: "0190012345001",
    documentType: "Invoice",
    sourceModule: "Sales",
    sourceEntityId: "invoice-1",
    documentNumber: "001-001-000000230",
    counterpartyName: "Consumidor Final",
    createdAt: "2026-11-06T14:00:00Z",
    updatedAt: "2026-11-06T15:00:00Z",
    observations: null,
    diagnostic: {
      currentState,
      environment: "1",
      lastAttemptUtc: "2026-11-06T15:00:00Z",
      messages: [],
      timeline: [],
      technicalInfo: {
        accessKey: ACCESS_KEY,
        authorizationNumber: null,
        environment: "1",
        authorizationDate: null,
        retryCount: 0,
        lastAttemptUtc: "2026-11-06T15:00:00Z",
        correlationId: null,
        ...info,
      },
      xmlDraftAvailable: true,
      xmlSignedAvailable: false,
      xmlAuthorizedAvailable: false,
      requiresReconciliation: false,
      ...o,
    },
  };
}

function renderSheet(d: ElectronicDocumentDetailDto, canDownloadRide = true) {
  const onDownloadRide = vi.fn();
  const onViewXml = vi.fn();
  render(
    <I18nProvider>
      <ElectronicDocumentDetailPanel
        open
        detail={d}
        loading={false}
        error={null}
        xmlContent={null}
        xmlVariant={null}
        xmlLoading={false}
        xmlError={null}
        canRetry
        retryLoading={false}
        canDownloadRide={canDownloadRide}
        rideDownloading={false}
        onClose={vi.fn()}
        onViewXml={onViewXml}
        onRetryNow={vi.fn()}
        onDownloadRide={onDownloadRide}
      />
    </I18nProvider>,
  );
  return { onDownloadRide, onViewXml };
}

/** Valor de un campo `.zh-summary-grid` por su etiqueta. */
function field(label: string): string | null | undefined {
  const el = Array.from(document.querySelectorAll(".zh-summary-grid__label")).find(
    (l) => l.textContent === label,
  );
  return el?.nextElementSibling?.textContent;
}

afterEach(() => cleanup());

describe("Ficha operativa — datos a la vista", () => {
  it("Autorizado: identificación, ambiente, autorización, clave completa, fechas y archivos sin expandir", () => {
    const d = detail(
      "Authorized",
      {
        xmlSignedAvailable: true,
        xmlAuthorizedAvailable: true,
        timeline: [
          { action: "Sent", fromState: "Signed", toState: "Sent", occurredAtUtc: "2026-11-06T14:01:00Z", userName: "Sistema", durationSinceLastMinutes: null },
          { action: "Received", fromState: "Sent", toState: "Received", occurredAtUtc: "2026-11-06T14:01:30Z", userName: "Sistema", durationSinceLastMinutes: 0.5 },
          { action: "Authorized", fromState: "Received", toState: "Authorized", occurredAtUtc: "2026-11-06T14:02:00Z", userName: "Sistema", durationSinceLastMinutes: 0.5 },
        ],
      },
      { authorizationNumber: AUTH_NUMBER, authorizationDate: "2026-11-06T14:02:00Z", retryCount: 1 },
    );
    renderSheet(d);
    expect(screen.getByText("Situación ante el SRI")).toBeTruthy();
    expect(screen.getByText("El SRI autorizó el comprobante.")).toBeTruthy();
    expect(screen.getAllByText("Pruebas").length).toBeGreaterThan(0);
    expect(field("Tipo Documento")).toBe("Factura");
    expect(field("Documento")).toBe("001-001-000000230");
    expect(field("Empresa")).toContain("Supermercado Central");
    expect(field("Empresa")).toContain("0190012345001");
    expect(field("Recepción SRI")).toMatch(/^Recibida · /);
    expect(field("Autorización SRI")).toBe("Autorizada");
    expect(field("Número de Autorización")).toBe(AUTH_NUMBER);
    expect(field("Número de Autorización")).not.toBe(field("Clave de Acceso"));
    expect(field("Clave de Acceso")).toContain(ACCESS_KEY);
    expect(field("Intentos")).toBe("1");
    expect(field("Último envío registrado")).not.toBe("No disponible");
    expect((screen.getByRole("button", { name: "Ver XML autorizado" }) as HTMLButtonElement).disabled).toBe(false);
    expect((screen.getByRole("button", { name: /Descargar RIDE/ }) as HTMLButtonElement).disabled).toBe(false);
  });

  it("sin autorización: número y fecha «No disponible», nunca la clave de acceso en su lugar", () => {
    renderSheet(detail("Signed", {}, { retryCount: 37 }));
    expect(field("Número de Autorización")).toBe("No disponible");
    expect(field("Fecha de Autorización")).toBe("No disponible");
    expect(field("Autorización SRI")).toBe("Pendiente de decisión del SRI");
    expect(field("Recepción SRI")).toBe("Sin confirmación de recepción");
    expect(field("Intentos")).toBe("37");
    expect(
      screen.getByText("XML firmado. El SRI todavía no confirma su recepción y no está autorizado."),
    ).toBeTruthy();
  });

  it("Borrador sin clave: «Aún no generada», XML no disponibles deshabilitados y sin RIDE", () => {
    renderSheet(
      detail("Draft", { xmlDraftAvailable: false }, { accessKey: null, lastAttemptUtc: null }),
    );
    expect(field("Clave de Acceso")).toBe("Aún no generada");
    expect(field("Recepción SRI")).toBe("No enviada al SRI");
    expect(field("Autorización SRI")).toBe("Sin autorización");
    for (const name of ["Ver XML generado", "Ver XML firmado", "Ver XML autorizado"])
      expect((screen.getByRole("button", { name }) as HTMLButtonElement).disabled).toBe(true);
    expect(screen.queryByRole("button", { name: /Descargar RIDE/ })).toBeNull();
    expect(
      screen.getByText("El RIDE está disponible solo para comprobantes autorizados."),
    ).toBeTruthy();
  });

  it("Enviado y Recibido distinguen recepción y autorización", () => {
    renderSheet(detail("Sent"));
    expect(field("Recepción SRI")).toBe("Sin confirmación de recepción");
    cleanup();
    renderSheet(detail("Received"));
    expect(field("Recepción SRI")).toBe("Recibida");
    expect(field("Autorización SRI")).toBe("Pendiente de decisión del SRI");
    expect(
      screen.getByText("El SRI lo recibió (RECIBIDA); falta su decisión de autorización."),
    ).toBeTruthy();
  });

  it("Rechazado en recepción: incidencia DEVUELTA y no autorizado", () => {
    renderSheet(
      detail("Rejected", {
        retransmission: { eligible: false, transmissionAttempt: 1, rejectionStage: "Reception", rejectionCodes: ["45"], blocks: [] },
      }),
    );
    const notice = screen
      .getByText(/devolvió en la recepción \(DEVUELTA\)/)
      .closest(".zh-page-notice");
    // Incidencia principal: aviso con rol de alerta (error/advertencia).
    expect(notice?.getAttribute("role")).toBe("alert");
    expect(field("Recepción SRI")).toBe("Devuelta (no recibida)");
    expect(field("Autorización SRI")).toBe("No autorizada");
  });

  it("Reintentos agotados: incidencia principal y reintento según permisos", () => {
    renderSheet(detail("DeadLetter", {}, { retryCount: 5 }));
    expect(
      screen.getByText(/Se agotaron los reintentos automáticos sin un resultado definitivo/),
    ).toBeTruthy();
    expect(screen.getAllByText("Reintentos agotados").length).toBeGreaterThan(0);
    expect(screen.getByRole("button", { name: "Reintentar ahora" })).toBeTruthy();
  });

  it("Anulado: significado propio, autorización histórica conservada, sin reintento", () => {
    renderSheet(detail("Cancelled", {}, { authorizationNumber: AUTH_NUMBER }));
    expect(screen.getByText("Comprobante anulado.")).toBeTruthy();
    expect(field("Autorización SRI")).toBe("Autorizada");
    expect(screen.queryByRole("button", { name: "Reintentar ahora" })).toBeNull();
  });
});

describe("Ficha operativa — respuesta SRI y secciones plegadas", () => {
  const messages = [
    { code: "43", messageType: "ERROR", message: "CLAVE ACCESO REGISTRADA", additionalInfo: null, occurredAtUtc: "2026-11-06T14:01:00Z" },
    { code: "70", messageType: "ADVERTENCIA", message: "CLAVE DE ACCESO EN PROCESAMIENTO", additionalInfo: "x".repeat(300), occurredAtUtc: "2026-11-06T15:00:00Z" },
  ];

  it("la última respuesta queda visible; su detalle largo se expande; el resto de mensajes se pliega", () => {
    renderSheet(detail("Signed", { messages }));
    const latestCard = screen.getByText("Última respuesta del SRI").closest(".card")!;
    expect(within(latestCard as HTMLElement).getByText("CLAVE DE ACCESO EN PROCESAMIENTO")).toBeTruthy();
    expect(within(latestCard as HTMLElement).queryByText("x".repeat(300))).toBeNull();
    const full = screen.getByRole("button", { name: "Ver respuesta completa" });
    expect(full.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(full);
    expect(within(latestCard as HTMLElement).getByText("x".repeat(300))).toBeTruthy();

    expect(screen.queryByText("CLAVE ACCESO REGISTRADA")).toBeNull();
    const all = screen.getByRole("button", { name: "Todos los mensajes del SRI (2)" });
    expect(all.getAttribute("aria-expanded")).toBe("false");
    fireEvent.click(all);
    expect(all.getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByText("CLAVE ACCESO REGISTRADA")).toBeTruthy();
    expect(document.getElementById(all.getAttribute("aria-controls")!)).toBeTruthy();
  });

  it("con una sola respuesta no ofrece «Todos los mensajes»; sin respuestas lo indica", () => {
    renderSheet(detail("Signed", { messages: [messages[0]] }));
    expect(screen.queryByRole("button", { name: /Todos los mensajes/ })).toBeNull();
    cleanup();
    renderSheet(detail("Draft"));
    expect(
      screen.getByText("No hay respuestas del SRI registradas para este documento."),
    ).toBeTruthy();
  });

  it("historial y datos de soporte inician plegados", () => {
    const d = detail("Signed", {
      timeline: [
        { action: "RetryAttempted", fromState: "Signed", toState: "Signed", occurredAtUtc: "2026-11-06T15:00:00Z", userName: "Sistema", durationSinceLastMinutes: 60 },
      ],
    });
    renderSheet(d);
    expect(document.querySelector(".zh-edoc-timeline")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Historial (1)" }));
    expect(screen.getByText(/hasta 20/)).toBeTruthy();
    expect(document.querySelector(".zh-edoc-timeline")?.textContent).toMatch(/^Reintento/);

    expect(screen.queryByText("invoice-1")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Datos de soporte" }));
    expect(screen.getByText("invoice-1")).toBeTruthy();
    expect(screen.getByText("edoc-1")).toBeTruthy();
  });

  it("no usa el panel de diagnóstico compartido (Ventas y NC lo conservan)", () => {
    renderSheet(detail("Signed"));
    expect(screen.queryByText("Resumen del diagnóstico")).toBeNull();
    expect(document.querySelector(".zh-edoc-tech-info")).toBeNull();
  });
});
