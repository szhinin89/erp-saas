// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { JournalEntriesPage } from "./JournalEntriesPage";
import type { JournalEntryListItemDto } from "../api/accountingApi";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ACCOUNTING-JOURNAL-UX-01 — listado de asientos: 7 columnas compuestas, filtros en ZHFilterBar
 * (origen, estado, rango de fechas) con restablecimiento, estados, importe/descuadre, navegación al
 * detalle (botón y fila), permiso de alta manual y estado vacío.
 */

const mocks = vi.hoisted(() => ({ listJournalEntries: vi.fn(), canShow: vi.fn((_: string) => true) }));
vi.mock("../api/accountingApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/accountingApi")>()),
  accountingApi: { listJournalEntries: mocks.listJournalEntries },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: mocks.canShow }) }));
vi.mock("../../../lib/messages", () => ({ message: { error: vi.fn() } }));

const AUTOMATIC: JournalEntryListItemDto = {
  id: "je-1",
  entryNumber: 12,
  entryDate: "2026-08-01",
  sourceModule: "Sales",
  sourceEventType: "InvoiceIssued",
  sourceEventId: "inv-1",
  description: "Sales — InvoiceIssued — inv-1",
  totalDebit: 115,
  totalCredit: 115,
  status: "Posted",
  createdAt: "2026-08-01T00:00:00Z",
  sourceDocumentType: "Factura",
  sourceDocumentNumber: "001-001-000000123",
  sourceDocumentDate: "2026-07-31",
  sourcePartyName: "Cliente Uno",
  sourceStatus: "Autorizada",
  sourceRoute: "/sales/invoices/inv-1",
};

const MANUAL_DRAFT: JournalEntryListItemDto = {
  ...AUTOMATIC,
  id: "je-2",
  entryNumber: null,
  sourceModule: "Accounting",
  sourceEventType: "ManualJournalEntry",
  sourceEventId: "je-2",
  description: "Ajuste de provisión",
  totalDebit: 50,
  totalCredit: 40,
  status: "Draft",
  sourceDocumentType: null,
  sourceDocumentNumber: null,
  sourceDocumentDate: null,
  sourcePartyName: null,
};

function respond(items: JournalEntryListItemDto[]) {
  mocks.listJournalEntries.mockResolvedValue({ items, pageNumber: 1, pageSize: 20, totalCount: items.length });
}

function show() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/accounting/journal-entries"]}>
        <Routes>
          <Route path="/accounting/journal-entries" element={<JournalEntriesPage />} />
          <Route path="/accounting/journal-entries/:id" element={<p>detalle abierto</p>} />
          <Route path="/accounting/journal-entries/new" element={<p>alta manual</p>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

const lastFilter = () => mocks.listJournalEntries.mock.calls.at(-1)!;

beforeEach(() => {
  vi.clearAllMocks();
  mocks.canShow.mockImplementation(() => true);
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
  respond([AUTOMATIC, MANUAL_DRAFT]);
});
afterEach(cleanup);

it("tabla con 7 columnas compuestas y datos de asiento, operación, documento, tercero e importe", async () => {
  show();
  const table = await screen.findByRole("table");
  const headers = within(table).getAllByRole("columnheader").map((h) => h.textContent);
  expect(headers).toEqual(["Asiento", "Operación", "Documento origen", "Cliente / Proveedor", "Importe", "Estado", "Acciones"]);

  const row = within(table).getAllByRole("row")[1]!;
  const cells = within(row).getAllByRole("cell").map((c) => c.textContent?.replace(/\s+/g, " ").trim());
  // Columnas compuestas en dos líneas: principal (default) y secundaria (muted).
  const lines = (col: number) =>
    [...row.querySelectorAll(`td:nth-child(${col}) > .zh-stack > *`)].map((el) => [el.className, el.textContent?.trim()]);
  expect(lines(1)).toEqual([
    ["zh-data-value zh-data-value--default", "#12"],
    ["zh-data-value zh-data-value--muted", "01/08/2026"],
  ]);
  // Operación: nombre arriba, chip del módulo (Badge oficial con icono) debajo.
  expect(lines(2)[0]).toEqual(["zh-data-value zh-data-value--default", "Asiento de venta"]);
  const chip = row.querySelector("td:nth-child(2) .badge")!;
  expect(chip.className).toContain("badge--info");
  expect(chip.textContent).toContain("Ventas");
  // Documento: número arriba; tipo · fecha del documento debajo.
  expect(lines(3)[0]![1]).toBe("001-001-000000123");
  expect(lines(3)[1]).toEqual(["zh-data-value zh-data-value--muted", "Factura · 31/07/2026"]);
  expect(cells[3]).toBe("Cliente Uno");
  expect(cells[4]).toContain("$115.00");
  expect(row.querySelector("td:nth-child(5)")!.className).toContain("zh-table-cell--num");
});

it("descripción secundaria solo si aporta información distinta de la operación", async () => {
  show();
  const table = await screen.findByRole("table");
  const [, automaticRow, manualRow] = within(table).getAllByRole("row");
  // Automático: friendlyDescription reproduce «Ventas — Asiento de venta» → no se repite.
  expect(within(automaticRow!).queryByText("Ventas — Asiento de venta")).toBeNull();
  expect(within(manualRow!).getByText("Asiento manual")).toBeTruthy();
  expect(within(manualRow!).getByText("Ajuste de provisión")).toBeTruthy();
  expect(within(manualRow!).getByText("Sin numerar")).toBeTruthy();
});

it("estados con badge propio y descuadre indicado en el importe", async () => {
  show();
  const table = await screen.findByRole("table");
  const [, automaticRow, manualRow] = within(table).getAllByRole("row");
  expect(within(automaticRow!).getByText("Contabilizado").className).toContain("badge--success");
  expect(within(manualRow!).getByText("Borrador").className).toContain("badge--warning");
  expect(within(automaticRow!).queryByText("Descuadrado")).toBeNull();
  expect(within(manualRow!).getByText("Descuadrado")).toBeTruthy();
});

it("descartado: badge neutral y sin indicador de descuadre", async () => {
  respond([{ ...MANUAL_DRAFT, status: "Discarded" }]);
  show();
  const table = await screen.findByRole("table");
  expect(within(table).getByText("Descartado").className).toContain("badge--neutral");
  expect(within(table).queryByText("Descuadrado")).toBeNull();
});

it("filtros de origen, estado y fechas envían los parámetros existentes y vuelven a la página 1", async () => {
  show();
  await screen.findByRole("table");
  fireEvent.change(screen.getByLabelText("Origen"), { target: { value: "manual" } });
  fireEvent.change(screen.getByLabelText("Estado"), { target: { value: "Posted" } });
  fireEvent.change(screen.getByLabelText("Desde"), { target: { value: "2026-08-01" } });
  fireEvent.change(screen.getByLabelText("Hasta"), { target: { value: "2026-08-31" } });
  await vi.waitFor(() =>
    expect(lastFilter()).toEqual([
      1,
      20,
      { status: "Posted", manual: true, fromDate: "2026-08-01", toDate: "2026-08-31" },
    ]),
  );
});

it("«Limpiar filtros» restablece todos los filtros", async () => {
  show();
  await screen.findByRole("table");
  fireEvent.change(screen.getByLabelText("Origen"), { target: { value: "automatic" } });
  fireEvent.change(screen.getByLabelText("Desde"), { target: { value: "2026-08-01" } });
  await vi.waitFor(() => expect(lastFilter()[2]).toMatchObject({ manual: false, fromDate: "2026-08-01" }));
  await screen.findByRole("table"); // «Limpiar» está deshabilitado mientras carga (ZHFilterBar disabled)
  fireEvent.click(screen.getByRole("button", { name: /Limpiar filtros/ }));
  await vi.waitFor(() =>
    expect(lastFilter()[2]).toEqual({ status: undefined, manual: undefined, fromDate: undefined, toDate: undefined }),
  );
  expect((screen.getByLabelText("Origen") as HTMLSelectElement).value).toBe("");
  expect((screen.getByLabelText("Desde") as HTMLInputElement).value).toBe("");
});

it("«Ver detalle» abre el detalle del asiento", async () => {
  show();
  const table = await screen.findByRole("table");
  fireEvent.click(within(within(table).getAllByRole("row")[1]!).getByRole("button", { name: /Ver detalle/ }));
  expect(await screen.findByText("detalle abierto")).toBeTruthy();
});

it("clic en la fila abre el detalle del asiento", async () => {
  show();
  const table = await screen.findByRole("table");
  fireEvent.click(within(within(table).getAllByRole("row")[2]!).getByText("Ajuste de provisión"));
  expect(await screen.findByText("detalle abierto")).toBeTruthy();
});

it("«Nuevo asiento manual» respeta el permiso accounting.manual.create", async () => {
  show();
  fireEvent.click(await screen.findByRole("button", { name: /Nuevo asiento manual/ }));
  expect(await screen.findByText("alta manual")).toBeTruthy();
  cleanup();
  mocks.canShow.mockImplementation((perm: string) => perm !== "accounting.manual.create");
  show();
  await screen.findByRole("table");
  expect(screen.queryByRole("button", { name: /Nuevo asiento manual/ })).toBeNull();
});

it("estado vacío oficial cuando no hay asientos", async () => {
  respond([]);
  show();
  expect(await screen.findByText("No hay asientos contables registrados.")).toBeTruthy();
});
