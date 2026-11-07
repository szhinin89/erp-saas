// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { JournalEntryDetailPage } from "./JournalEntryDetailPage";
import type { JournalEntryDetailDto } from "../api/accountingApi";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ACCOUNTING-JOURNAL-LINE-DESCRIPTIONS-EXPENSES-PAYABLES-01 — la columna Descripción de la tabla
 * de líneas debe mostrar `displayDescription` (texto legible resuelto en lectura) cuando el
 * backend lo envía, y caer a `description` (el texto técnico persistido) cuando no.
 */

const mocks = vi.hoisted(() => ({ getJournalEntryById: vi.fn() }));
// Mock parcial: la página también consume constantes reales del módulo (FISCAL_YEAR_CLOSING_FACT).
vi.mock("../api/accountingApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/accountingApi")>()),
  accountingApi: { getJournalEntryById: mocks.getJournalEntryById },
}));
vi.mock("../../../lib/messages", () => ({ message: { error: vi.fn(), confirm: vi.fn() } }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));

const BASE_ENTRY: JournalEntryDetailDto = {
  id: "entry-1",
  entryNumber: 10,
  entryDate: "2026-08-01",
  accountingPeriodId: "period-1",
  fiscalYear: 2026,
  sourceModule: "Expenses",
  sourceEventType: "DocumentConfirmed",
  sourceEventId: "expense-1",
  description: "Expenses — DocumentConfirmed — expense-1",
  status: "Posted",
  postedAtUtc: "2026-08-01T00:00:00Z",
  originalJournalEntryId: null,
  originalJournalEntryNumber: null,
  originalJournalEntryDate: null,
  reverseJournalEntryId: null,
  reverseJournalEntryNumber: null,
  reverseJournalEntryDate: null,
  reversedAtUtc: null,
  reverseReason: null,
  lines: [
    {
      id: "line-1",
      accountId: "acc-1",
      accountCode: "5.1.01",
      accountName: "Gastos generales",
      description: "Expenses — DocumentConfirmed — expense-1",
      displayDescription: "Gasto 001-500-000007861 — Proveedor S.A.",
      debit: 100,
      credit: 0,
      sortOrder: 0,
    },
    {
      id: "line-2",
      accountId: "acc-2",
      accountName: "Cuentas por pagar",
      accountCode: "2.1.01",
      description: "serv nube",
      displayDescription: null,
      debit: 0,
      credit: 100,
      sortOrder: 1,
    },
  ],
  totalDebit: 100,
  totalCredit: 100,
  isBalanced: true,
  createdAt: "2026-08-01T00:00:00Z",
  sourceDocumentType: "Gasto",
  sourceDocumentNumber: "001-500-000007861",
  sourceDocumentDate: "2026-08-01",
  sourcePartyName: "Proveedor S.A.",
  sourceStatus: "Confirmado",
  sourceRoute: "/expenses/documents/expense-1",
} as unknown as JournalEntryDetailDto;

function show() {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={["/accounting/journal-entries/entry-1"]}>
        <Routes>
          <Route path="/accounting/journal-entries/:id" element={<JournalEntryDetailPage />} />
          <Route path="/expenses/documents/:id" element={<p>pantalla documento origen</p>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
});
afterEach(cleanup);

it("shows displayDescription for the automatic line and falls back to description when absent", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  show();

  const table = await screen.findByRole("table");
  const rows = within(table).getAllByRole("row");
  expect(within(rows[1]!).getByText("Gasto 001-500-000007861 — Proveedor S.A.")).toBeTruthy();
  expect(within(rows[2]!).getByText("serv nube")).toBeTruthy();
  expect(within(rows[1]!).queryByText("Expenses — DocumentConfirmed — expense-1")).toBeNull();
});

it("falls back to a friendly (translated) description when displayDescription cannot be resolved", async () => {
  // ACCOUNTING-JOURNAL-LABELS-UX-01: cuando no hay displayDescription, ya no se muestra el texto
  // técnico crudo ("Expenses — DocumentConfirmed — expense-1") — se traduce a negocio ("Gastos —
  // Confirmación de gasto") vía friendlyDescription, que reconoce el formato compuesto por
  // JournalFactory.cs y lo traduce sin tocar backend.
  mocks.getJournalEntryById.mockResolvedValue({
    ...BASE_ENTRY,
    lines: BASE_ENTRY.lines.map((l) => ({ ...l, displayDescription: null })),
  });
  show();

  const table = await screen.findByRole("table");
  const rows = within(table).getAllByRole("row");
  expect(within(rows[1]!).getByText("Gastos — Confirmación de gasto")).toBeTruthy();
  expect(within(rows[1]!).queryByText("Expenses — DocumentConfirmed — expense-1")).toBeNull();
});

// ZH-DESIGN-SYSTEM-PRECISION-04F — Debe/Haber/totales del asiento declaran `precision="accounting"`
// (no money, aunque hoy ambas escalas coincidan): con escalas distintas manda accountingDecimals.
it("debe/haber y totales usan accountingDecimals (no moneyDecimals) y reaccionan A → B", async () => {
  setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, accountingDecimals: 2, moneyDecimals: 4 });
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  const { container } = show();
  await screen.findByRole("table");
  const amounts = () => [...container.querySelectorAll(".zh-money-value")].map((e) => e.textContent);
  expect(amounts()).toContain("$100.00");
  expect(amounts()).not.toContain("$100.0000");

  act(() => setPrecisionPolicyForTests({ ...TEST_PRECISION_POLICY, accountingDecimals: 3, moneyDecimals: 4 }));
  expect(amounts()).toContain("$100.000");
  expect(amounts()).not.toContain("$100.00");
});

// ── ACCOUNTING-JOURNAL-UX-01 — distribución compacta del detalle ──────────────────────────────

function summaryItem(container: HTMLElement, label: string): HTMLElement {
  const item = [...container.querySelectorAll<HTMLElement>(".zh-summary-grid > div")].find(
    (el) => el.querySelector(".zh-summary-grid__label")?.textContent?.trim().endsWith(label),
  );
  if (!item) throw new Error(`summary item "${label}" not found`);
  return item;
}

const cardTitles = (container: HTMLElement) =>
  [...container.querySelectorAll(".card-header")].map((h) => h.firstChild?.textContent);

it("muestra LoadingState oficial mientras carga", () => {
  mocks.getJournalEntryById.mockReturnValue(new Promise(() => {}));
  const { container } = show();
  expect(container.querySelector(".loading-state")).toBeTruthy();
});

it("cabecera compacta (número, operación, fecha) y estado destacado en aviso propio", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  show();
  expect(await screen.findByRole("heading", { name: "Asiento contable #10" })).toBeTruthy();
  expect(screen.getByText("Confirmación de gasto · 01/08/2026")).toBeTruthy();
  const status = screen.getByText("Contabilizado").closest(".zh-page-notice")!;
  expect(status.textContent).toContain("Asiento registrado en la contabilidad.");
  expect(screen.getByRole("button", { name: /Volver al listado/ })).toBeTruthy();
});

it("borrador: estado con su variante y texto propio", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, status: "Draft" });
  show();
  const draft = (await screen.findByText("Borrador")).closest(".zh-page-notice")!;
  expect(draft.querySelector(".zh-form-alert--warning")).toBeTruthy();
  expect(draft.textContent).toContain("aún no afecta la contabilidad");
});

it("resumen y documento origen en .zh-summary-grid: cada etiqueta junto a su valor", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  const { container } = show();
  await screen.findByRole("table");
  expect(container.querySelector(".zh-info-row")).toBeNull();
  expect(summaryItem(container, "Ejercicio fiscal").textContent).toContain("2026");
  expect(summaryItem(container, "Tipo de asiento").textContent).toContain("Confirmación de gasto");
  expect(summaryItem(container, "Número").textContent).toContain("001-500-000007861");
  expect(summaryItem(container, "Cliente / Proveedor").textContent).toContain("Proveedor S.A.");
  expect(summaryItem(container, "Estado").textContent).toContain("Confirmado");
  // Datos del asiento y Documento de origen: dos bloques horizontales de la misma fila.
  const row = [...container.querySelectorAll(".zh-grid--2")].find((el) => el.querySelector(":scope > .card"))!;
  expect([...row.querySelectorAll(":scope > .card > .card-header")].map((h) => h.textContent)).toEqual([
    "Datos del asiento",
    "Documento de origen",
  ]);
  // La descripción técnica del automático repite la operación → no se muestra.
  expect(() => summaryItem(container, "Descripción")).toThrow();
});

it("las líneas van inmediatamente después del resumen y conservan código y cuenta", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  const { container } = show();
  const table = await screen.findByRole("table");
  expect(cardTitles(container)).toEqual(["Datos del asiento", "Documento de origen", "Movimientos contables"]);
  const header = within(table).getAllByRole("columnheader").map((h) => h.textContent);
  expect(header).toEqual(["Cuenta", "Descripción", "Debe", "Haber"]);
  expect(within(table).getByText("5.1.01")).toBeTruthy();
  expect(within(table).getAllByRole("row")[1]!.textContent).toContain("5.1.01 — Gastos generales");
});

it("«Ver documento origen» navega a sourceRoute", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  show();
  fireEvent.click(await screen.findByRole("button", { name: /Ver documento origen/ }));
  expect(await screen.findByText("pantalla documento origen")).toBeTruthy();
});

it("sin sourceRoute no ofrece navegación al documento origen", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, sourceRoute: null });
  show();
  await screen.findByRole("table");
  expect(screen.queryByRole("button", { name: /Ver documento origen/ })).toBeNull();
});

it("cuadrado (isBalanced): un único aviso de cuadre con diferencia cero", async () => {
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  show();
  await screen.findByRole("table");
  const notice = screen.getByText("Asiento cuadrado").closest(".zh-page-notice")!;
  expect(notice.textContent).toContain("Debe = Haber · Diferencia: $0.00");
  expect(screen.queryByText("Asiento descuadrado")).toBeNull();
});

it("descuadrado según isBalanced: aviso con la diferencia monetaria", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, totalCredit: 90.5, isBalanced: false });
  show();
  await screen.findByRole("table");
  const notice = screen.getByText("Asiento descuadrado").closest(".zh-page-notice")!;
  expect(notice.querySelector(".zh-form-alert--error")).toBeTruthy();
  expect(notice.textContent).toContain("Diferencia: $9.50");
  expect(screen.queryByText("Asiento cuadrado")).toBeNull();
});

it("isBalanced manda: con isBalanced=true no se marca descuadre", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, totalCredit: 90, isBalanced: true });
  show();
  await screen.findByRole("table");
  expect(screen.queryByText("Asiento descuadrado")).toBeNull();
  expect(screen.getByText("Asiento cuadrado").closest(".zh-page-notice")!.textContent).toContain("$0.00");
});

it("descartado: no muestra estado de cuadre", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, status: "Discarded", isBalanced: false });
  show();
  await screen.findByRole("table");
  expect(screen.getByText("Descartado")).toBeTruthy();
  expect(screen.queryByText("Asiento descuadrado")).toBeNull();
  expect(screen.queryByText("Asiento cuadrado")).toBeNull();
});

it("explicación contable solo cuando existe un texto verificable (factTypeDescription)", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, sourceModule: "Sales", sourceEventType: "InvoiceIssued" });
  show();
  expect(await screen.findByText("Registra el ingreso y el cobro/CxC.")).toBeTruthy();
  expect(screen.getByText("¿Qué registra este asiento?")).toBeTruthy();
  cleanup();
  mocks.getJournalEntryById.mockResolvedValue(BASE_ENTRY);
  show();
  await screen.findByRole("table");
  expect(screen.queryByText("¿Qué registra este asiento?")).toBeNull();
});

it("reverso: muestra la relación y navega al asiento relacionado", async () => {
  mocks.getJournalEntryById.mockResolvedValue({
    ...BASE_ENTRY,
    sourceModule: "Accounting",
    sourceEventType: "Reversal",
    originalJournalEntryId: "entry-0",
    originalJournalEntryNumber: 9,
    originalJournalEntryDate: "2026-07-31",
    reverseReason: "Error de registro",
  });
  const { container } = show();
  expect(await screen.findByText("Es reverso de otro asiento")).toBeTruthy();
  expect(summaryItem(container, "Asiento original").textContent).toContain("9 — 31/07/2026");
  expect(summaryItem(container, "Motivo").textContent).toContain("Error de registro");
  fireEvent.click(screen.getByRole("button", { name: "Ver asiento relacionado" }));
  await vi.waitFor(() => expect(mocks.getJournalEntryById).toHaveBeenLastCalledWith("entry-0"));
});

it("origen no resuelto: aviso y datos técnicos en la misma cuadrícula", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...BASE_ENTRY, sourceDocumentNumber: null });
  const { container } = show();
  expect(await screen.findByText(/Origen documental no disponible/)).toBeTruthy();
  expect(summaryItem(container, "Id de origen (técnico)").textContent).toContain("expense-1");
});

it("asiento manual: sin documento origen, acciones antes del contenido e intentos después de las líneas", async () => {
  mocks.getJournalEntryById.mockResolvedValue({
    ...BASE_ENTRY,
    sourceModule: "Accounting",
    sourceEventType: "ManualJournalEntry",
    status: "Draft",
    entryNumber: null,
    manual: {
      reference: "REF-1", version: 1, createdBy: "admin", updatedBy: null,
      discardedAtUtc: null, discardedBy: null, discardReason: null,
      attempts: [{ id: "a-1", attemptedAtUtc: "2026-08-01T10:00:00Z", attemptedBy: "admin", success: false, errorMessage: "x" }],
    },
  });
  const { container } = show();
  expect(await screen.findByRole("heading", { name: "Asiento (sin numerar)" })).toBeTruthy();
  expect(cardTitles(container)).toEqual(["Asiento manual", "Datos del asiento", "Movimientos contables", "Intentos de publicación"]);
  // Descripción propia (no repite la operación) → visible.
  expect(summaryItem(container, "Descripción").textContent).toContain("Gastos — Confirmación de gasto");
  expect(screen.getByRole("button", { name: "Publicar" })).toBeTruthy();
});
