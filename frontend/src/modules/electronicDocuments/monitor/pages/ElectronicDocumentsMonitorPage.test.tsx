// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { I18nProvider } from "../../../../i18n/i18n";
import { useElectronicInvoicingStatusStore } from "../../../../store/electronicInvoicingStatusStore";
import type {
  ElectronicDocumentDetailDto,
  ElectronicDocumentListItemDto,
  ElectronicDocumentsDashboardDto,
} from "../api/electronicDocumentsMonitorService";

/**
 * SRI-MONITOR-UX-01 — Monitor en tres pestañas (Resumen, Documentos, Servicios SRI): cada pestaña
 * consulta solo lo que muestra; los indicadores aplican sus filtros y abren Documentos; la cabecera
 * (salud compacta, Actualizar) y la pausa SRI quedan fuera de las pestañas.
 */

const granted = new Set<string>();
vi.mock("../../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({ canShow: (p: string) => granted.has(p) }),
}));

const getList = vi.fn();
const getDashboard = vi.fn();
const getDetail = vi.fn();
vi.mock("../api/electronicDocumentsMonitorService", () => ({
  electronicDocumentsMonitorService: {
    getList: (...a: unknown[]) => getList(...a),
    getDashboard: (...a: unknown[]) => getDashboard(...a),
    getDetail: (...a: unknown[]) => getDetail(...a),
  },
}));

vi.mock("../../../ride/facades/rideGenerationFacade", () => ({
  useRideActions: () => ({
    ridePending: false,
    handleViewRide: vi.fn(),
    handleDownloadRide: vi.fn(),
    handleRegenerateRide: vi.fn(),
  }),
}));

import { ElectronicDocumentsMonitorPage } from "./ElectronicDocumentsMonitorPage";

const DASHBOARD: ElectronicDocumentsDashboardDto = {
  pending: 2,
  signed: 3,
  sent: 4,
  authorized: 120,
  rejected: 5,
  error: 6,
  authorizedToday: 11,
  errorsToday: 8,
  averageAuthorizationMinutes: 90,
  pendingRetries: 9,
  totalToday: 10,
};

const ROW: ElectronicDocumentListItemDto = {
  id: "doc-dl",
  createdAt: "2026-11-01T10:00:00Z",
  documentType: "Invoice",
  documentNumber: "001-001-000000777",
  companyId: "company-1",
  companyName: "ZH Technologies",
  counterpartyName: "Juan Pérez",
  currentState: "DeadLetter",
  environment: "2",
  accessKey: "1".repeat(49),
  retryCount: 5,
  updatedAt: "2026-11-01T11:00:00Z",
  lastMessage: "Reintentos agotados tras 5 intentos.",
};

function page(items: ElectronicDocumentListItemDto[], total = items.length) {
  return { items, total, pageNumber: 1, pageSize: 25 };
}

type SriHealth = NonNullable<
  ReturnType<typeof useElectronicInvoicingStatusStore.getState>["health"]
>;

/**
 * La salud SRI se siembra en el store (el componente la lee de allí) y `refreshHealth` se espía:
 * el Monitor no importa internals del módulo de configuración.
 */
const refreshHealth = vi.fn().mockResolvedValue(undefined);
function seedHealth(dto: SriHealth) {
  useElectronicInvoicingStatusStore.setState({
    health: dto,
    healthLoadedAt: Date.now(),
    refreshHealth,
  });
}

function health(state: "Unverified" | "Available"): SriHealth {
  return {
    environment: "Production",
    environmentName: "Producción",
    reception: {
      state,
      reason: state === "Unverified" ? "NoRecentEvidence" : "RecentResponses",
      evidence: "Call",
      lastObservedAtUtc: state === "Unverified" ? null : "2026-11-01T15:00:00Z",
    },
    authorization: {
      state: "Available",
      reason: "RecentResponses",
      evidence: "Call",
      lastObservedAtUtc: "2026-11-01T15:00:00Z",
    },
  } as SriHealth;
}

function detail(): ElectronicDocumentDetailDto {
  return {
    id: "doc-dl",
    companyId: "company-1",
    companyName: "ZH Technologies",
    companyTaxId: "0999999999001",
    documentType: "Invoice",
    sourceModule: "Sales",
    sourceEntityId: "invoice-1",
    documentNumber: "001-001-000000777",
    counterpartyName: "Juan Pérez",
    createdAt: "2026-11-01T10:00:00Z",
    updatedAt: null,
    observations: null,
    diagnostic: {
      currentState: "DeadLetter",
      environment: "2",
      lastAttemptUtc: "2026-11-01T11:00:00Z",
      messages: [],
      timeline: [],
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
    },
  };
}

const documentListCalls = () =>
  getList.mock.calls.filter(([params]) => params.pageSize === 25);
const attentionCalls = () =>
  getList.mock.calls.filter(([params]) => params.pageSize === 5);

function renderPage() {
  return render(
    <I18nProvider>
      <ElectronicDocumentsMonitorPage />
    </I18nProvider>,
  );
}

const tab = (name: string) => screen.getByRole("tab", { name });

beforeEach(() => {
  vi.clearAllMocks();
  granted.clear();
  granted.add("electronic-documents.view");
  granted.add("electronic-documents.detail");
  getDashboard.mockResolvedValue(DASHBOARD);
  getList.mockImplementation((params: { pageSize: number }) =>
    Promise.resolve(params.pageSize === 5 ? page([ROW], 7) : page([ROW])),
  );
  getDetail.mockResolvedValue(detail());
  useElectronicInvoicingStatusStore.getState().clear();
  seedHealth(health("Available"));
});

afterEach(() => {
  cleanup();
  useElectronicInvoicingStatusStore.getState().clear();
});

describe("Monitor SRI — pestañas", () => {
  it("abre en Resumen con tres pestañas y consulta solo indicadores y atención", async () => {
    renderPage();
    expect(
      screen.getAllByRole("tab").map((t) => t.getAttribute("aria-label")),
    ).toEqual(["Resumen", "Documentos", "Servicios SRI"]);
    expect(tab("Resumen").getAttribute("aria-selected")).toBe("true");
    await waitFor(() => expect(getDashboard).toHaveBeenCalledTimes(1));
    expect(attentionCalls()).toHaveLength(1);
    expect(attentionCalls()[0][0]).toEqual({
      state: "Rejected,DeadLetter,Failed,Dispatching",
      pageNumber: 1,
      pageSize: 5,
    });
    expect(documentListCalls()).toHaveLength(0);
  });

  it("Resumen: los diez indicadores en Estado actual, Hoy y Seguimiento, sin duplicados", async () => {
    renderPage();
    await screen.findByText("120");
    for (const title of ["Estado actual", "Hoy", "Seguimiento"])
      expect(screen.getByText(title)).toBeTruthy();
    const labels = Array.from(document.querySelectorAll(".pg-kpi-label")).map(
      (el) => el.textContent,
    );
    expect(labels).toEqual([
      "Pendientes",
      "En proceso",
      "Autorizados",
      "Con error",
      "Registrados hoy",
      "Autorizados hoy",
      "Errores hoy",
      "Rechazados",
      "Reintentos pendientes",
      "Tiempo promedio de autorización",
    ]);
    expect(new Set(labels).size).toBe(10);
    expect(document.querySelectorAll(".pg-kpis")).toHaveLength(3);
    // En proceso = signed + sent (el backend ya suma Sent + Received en `sent`).
    expect(screen.getByText("7", { selector: ".pg-kpi-value" })).toBeTruthy();
    // 90 min → 1.5 h, marcado como histórico.
    expect(screen.getByText("Histórico")).toBeTruthy();
    expect(document.body.textContent).toContain("1.5h");
    expect(screen.queryByText("Total emitidos hoy")).toBeNull();
  });

  it("Documentos consulta el listado solo al abrir la pestaña; Servicios SRI no consulta documentos", async () => {
    renderPage();
    await waitFor(() => expect(getDashboard).toHaveBeenCalledTimes(1));
    fireEvent.click(tab("Servicios SRI"));
    expect(documentListCalls()).toHaveLength(0);
    expect(getDashboard).toHaveBeenCalledTimes(1);
    fireEvent.click(tab("Documentos"));
    await waitFor(() => expect(documentListCalls()).toHaveLength(1));
  });
});

describe("Monitor SRI — indicadores conectados a los filtros", () => {
  it("Pendientes aplica su grupo de estados, abre Documentos y el selector lo refleja", async () => {
    renderPage();
    await screen.findByText("120");
    fireEvent.click(screen.getByRole("button", { name: /Pendientes/ }));
    expect(tab("Documentos").getAttribute("aria-selected")).toBe("true");
    await waitFor(() =>
      expect(documentListCalls().at(-1)?.[0]).toMatchObject({
        state: "Draft,XmlGenerated",
        pageNumber: 1,
      }),
    );
    const select = screen.getByLabelText("Estado") as HTMLSelectElement;
    expect(select.value).toBe("Draft,XmlGenerated");
    expect(select.selectedOptions[0].textContent).toBe("Pendientes");
  });

  it("Registrados hoy aplica el rango de fecha de hoy", async () => {
    renderPage();
    await screen.findByText("120");
    fireEvent.click(screen.getByRole("button", { name: /Registrados hoy/ }));
    await waitFor(() => {
      const params = documentListCalls().at(-1)?.[0];
      expect(params?.dateFrom).toBeTruthy();
      expect(params?.dateFrom).toBe(params?.dateTo);
    });
  });

  it("los indicadores sin filtro asociado no son botones", async () => {
    renderPage();
    await screen.findByText("120");
    for (const name of [
      /Autorizados hoy/,
      /Errores hoy/,
      /Reintentos pendientes/,
      /Tiempo promedio/,
    ])
      expect(screen.queryByRole("button", { name })).toBeNull();
  });
});

describe("Monitor SRI — documentos que requieren atención", () => {
  it("lista los más recientes y «Ver todos» abre Documentos con el mismo filtro y sin otros filtros", async () => {
    renderPage();
    expect(await screen.findByText("001-001-000000777")).toBeTruthy();
    expect(screen.getByText("Reintentos agotados")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Ver todos (7)" }));
    expect(tab("Documentos").getAttribute("aria-selected")).toBe("true");
    await waitFor(() =>
      expect(documentListCalls().at(-1)?.[0]).toEqual({
        dateFrom: undefined,
        dateTo: undefined,
        state: "Rejected,DeadLetter,Failed,Dispatching",
        documentType: undefined,
        environment: undefined,
        search: undefined,
        pageNumber: 1,
        pageSize: 25,
      }),
    );
  });

  it("sin documentos muestra el estado vacío y oculta «Ver todos»", async () => {
    getList.mockResolvedValue(page([], 0));
    renderPage();
    expect(
      await screen.findByText("No hay documentos que requieran atención."),
    ).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Ver todos/ })).toBeNull();
  });

  it("«Ver detalle» abre el panel con las acciones existentes según permisos", async () => {
    granted.add("electronic-documents.retry");
    renderPage();
    fireEvent.click(
      await screen.findByRole("button", {
        name: "Ver detalle de 001-001-000000777",
      }),
    );
    await waitFor(() => expect(getDetail).toHaveBeenCalledWith("doc-dl"));
    const dialog = await screen.findByRole("dialog");
    expect(
      within(dialog).getByRole("button", { name: "Reintentar ahora" }),
    ).toBeTruthy();
  });
});

describe("Monitor SRI — búsqueda", () => {
  it("espera a que termine la escritura, no deshabilita el campo y consulta una sola vez", async () => {
    renderPage();
    fireEvent.click(tab("Documentos"));
    await waitFor(() => expect(documentListCalls()).toHaveLength(1));
    const input = screen.getByLabelText("Búsqueda") as HTMLInputElement;
    input.focus();
    for (const value of ["0", "00", "001"])
      fireEvent.change(input, { target: { value } });
    expect(input.disabled).toBe(false);
    expect(document.activeElement).toBe(input);
    expect(documentListCalls().some(([p]) => p.search)).toBe(false);
    await waitFor(() =>
      expect(documentListCalls().filter(([p]) => p.search)).toHaveLength(1),
    );
    expect(documentListCalls().at(-1)?.[0].search).toBe("001");
    expect(input.disabled).toBe(false);
  });
});

describe("Monitor SRI — cabecera, salud y pausa", () => {
  it("Actualizar recarga la pestaña visible y fuerza la lectura de la salud SRI", async () => {
    renderPage();
    await waitFor(() => expect(getDashboard).toHaveBeenCalledTimes(1));
    expect(refreshHealth).not.toHaveBeenCalledWith({ force: true });
    fireEvent.click(screen.getByRole("button", { name: /Actualizar/ }));
    await waitFor(() => expect(getDashboard).toHaveBeenCalledTimes(2));
    expect(attentionCalls()).toHaveLength(2);
    expect(documentListCalls()).toHaveLength(0);
    expect(refreshHealth).toHaveBeenCalledWith({ force: true });
  });

  it("Servicios SRI muestra el detalle; «Sin verificar» no se presenta como caído", async () => {
    seedHealth(health("Unverified"));
    renderPage();
    fireEvent.click(tab("Servicios SRI"));
    expect(await screen.findByText("Comunicación con el SRI")).toBeTruthy();
    const detailHealth = await screen.findAllByTestId("sri-health-reception");
    const badges = detailHealth.map((el) =>
      el.classList.contains("badge") ? el : el.querySelector(".badge"),
    );
    for (const badge of badges) {
      expect(badge?.textContent).toBe("Recepción: Sin verificar");
      expect(badge?.className).toContain("badge--neutral");
      expect(badge?.className).not.toContain("badge--error");
    }
    expect(
      screen.getByText(
        "Estado técnico de comunicación; no indica si un comprobante fue autorizado.",
      ),
    ).toBeTruthy();
  });

  it("la salud compacta y la pausa SRI permanecen visibles en todas las pestañas", async () => {
    useElectronicInvoicingStatusStore.setState({
      status: { status: "Disabled" },
    } as never);
    seedHealth(health("Available"));
    renderPage();
    for (const name of ["Resumen", "Documentos", "Servicios SRI"]) {
      fireEvent.click(tab(name));
      expect(
        screen.getByText(/emisión electrónica|pausad/i, {
          selector: ".zh-page-notice *",
        }),
      ).toBeTruthy();
      expect(
        (await screen.findAllByTestId("sri-health-authorization")).length,
      ).toBeGreaterThan(0);
    }
  });

  it("sin electronic-documents.view no muestra el monitor ni consulta", () => {
    granted.clear();
    renderPage();
    expect(screen.queryByRole("tab")).toBeNull();
    expect(getList).not.toHaveBeenCalled();
    expect(getDashboard).not.toHaveBeenCalled();
  });
});
