// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { PurchaseReceptionPage } from "./PurchaseReceptionPage";
import { purchaseReceptionService } from "../api/purchaseReceptionService";
import { buildReceptionRow as row } from "../utils/reception.testFixtures";
import { useSessionStore } from "../../../store/sessionStore";

vi.mock("../api/purchaseReceptionService", () => ({
  purchaseReceptionService: {
    importTxt: vi.fn(),
    downloadXml: vi.fn(),
    downloadXmlPending: vi.fn(),
    getXmlView: vi.fn(),
    refreshStatus: vi.fn(),
  },
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn(), confirm: vi.fn() },
}));

beforeEach(() => vi.clearAllMocks());
afterEach(cleanup);

const items = [
  row({
    documentId: "reg",
    supplierName: "Registrada SA",
    purchaseState: "CONFIRMED",
    status: "IMPORTED",
    documentStatus: "PROCESSED",
    processingStatus: "PROCESSED",
  }),
  row({ documentId: "todo", supplierName: "Pendiente SA" }),
  row({
    documentId: "sup",
    supplierName: "Sin Proveedor SA",
    supplierRuc: "0911111111001",
    supplierExists: false,
    supplierId: null,
    status: "NEW_SUPPLIER",
  }),
  row({
    documentId: "nc",
    supplierName: "Nota Sin Proveedor",
    sourceDocType: "CREDIT_NOTE",
    supplierExists: false,
    supplierId: null,
    status: "NEW_SUPPLIER",
  }),
];

async function renderImported() {
  vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
    items,
    totalParsed: items.length,
    parseErrorCount: 0,
    skippedUnsupportedCount: 0,
  });
  const view = render(
    <MemoryRouter>
      <I18nProvider>
        <PurchaseReceptionPage />
      </I18nProvider>
    </MemoryRouter>,
  );
  const input = view.container.querySelector('input[type="file"]') as HTMLInputElement;
  await act(async () => {
    fireEvent.change(input, { target: { files: [new File(["x"], "r.txt")] } });
  });
  return view;
}

const tableNames = () =>
  Array.from(document.querySelectorAll(".pur-supplier-name")).map((n) => n.textContent);


// IAM-V1-F2: canShow es fail-closed — sesión cargada con los permisos que estas pruebas ejercitan
// (antes los recibían implícitamente porque canShow devolvía true sin sesión).
beforeEach(() => {
  useSessionStore.setState({
    isLoaded: true,
    isLoading: false,
    authorization: { permissions: ["purchases.view", "purchases.create", "expenses.documents.create"] } as never,
  });
});
afterEach(() => {
  useSessionStore.setState({ isLoaded: false, isLoading: false, authorization: null });
});

describe("PurchaseReceptionPage — KPI accionables (PURCHASES-RECEPTION-FILTERS-01)", () => {
  it("las tarjetas son botones con estado: filtran su partición y se desactivan al pulsarlas de nuevo", async () => {
    await renderImported();
    expect(screen.getByText("Mostrando 4 de 4 comprobantes")).toBeTruthy();

    const pendingCard = screen.getByText("Pendientes de proveedor").closest("button")!;
    expect(pendingCard.getAttribute("aria-pressed")).toBe("false");
    fireEvent.click(pendingCard);
    expect(pendingCard.getAttribute("aria-pressed")).toBe("true");
    expect(tableNames().sort()).toEqual(["Nota Sin Proveedor", "Sin Proveedor SA"]);
    expect(screen.getByText("Mostrando 2 de 4 comprobantes")).toBeTruthy();
    // Los KPI siguen sobre el lote completo.
    expect(within(pendingCard).getByText("2")).toBeTruthy();

    const registeredCard = screen.getByText("Registrados en ERP").closest("button")!;
    fireEvent.click(registeredCard);
    expect(tableNames()).toEqual(["Registrada SA"]);
    expect(pendingCard.getAttribute("aria-pressed")).toBe("false");

    fireEvent.click(registeredCard);
    expect(tableNames()).toHaveLength(4);
  });

  it("conserva las acciones existentes: 'Crear proveedor' solo en facturas sin proveedor, nunca en NC", async () => {
    await renderImported();
    const buttons = screen.getAllByRole("button", { name: "Crear proveedor" });
    expect(buttons).toHaveLength(1);
    expect(buttons[0].closest(".pur-supplier-cell")?.textContent).toContain("Sin Proveedor SA");
  });

  it("documento repetido con datos distintos: 'En revisión' en Registro ERP, aviso con el total y filtro", async () => {
    vi.mocked(purchaseReceptionService.importTxt).mockResolvedValue({
      items: [
        row({ documentId: "dup", supplierName: "Repetido Uno", total: 10 }),
        row({ documentId: "dup", supplierName: "Repetido Dos", total: 20 }),
        row({ documentId: "ok", supplierName: "Normal SA" }),
      ],
      totalParsed: 3,
      parseErrorCount: 0,
      skippedUnsupportedCount: 0,
    });
    const view = render(
      <MemoryRouter>
        <I18nProvider>
          <PurchaseReceptionPage />
        </I18nProvider>
      </MemoryRouter>,
    );
    await act(async () => {
      fireEvent.change(view.container.querySelector('input[type="file"]')!, {
        target: { files: [new File(["x"], "r.txt")] },
      });
    });

    expect(screen.getByText("Comprobantes en revisión: 1")).toBeTruthy();
    expect(screen.getByText("Mostrando 2 de 2 comprobantes")).toBeTruthy();
    expect(screen.getAllByText("En revisión")).toHaveLength(2);
    // Ninguna fila se elige como correcta: cada una conserva su propio estado.
    expect(screen.getAllByText("Sin procesar")).toHaveLength(3);
    const toProcessCard = screen.getByText("Por procesar").closest("button")!;
    expect(within(toProcessCard).getByText("1")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Más filtros" }));
    const situation = screen.getByText("Situación ERP").closest(".zh-field")!.querySelector("select")!;
    fireEvent.change(situation, { target: { value: "review" } });
    expect(tableNames()).toEqual(["Repetido Uno", "Repetido Dos"]);
    expect(screen.getByText("Mostrando 1 de 2 comprobantes")).toBeTruthy();
  });

  it("'Solo requieren atención' deja fuera los registrados", async () => {
    await renderImported();
    fireEvent.click(screen.getByRole("checkbox", { name: "Solo requieren atención" }));
    expect(tableNames()).not.toContain("Registrada SA");
    expect(tableNames()).toHaveLength(3);
  });
});
