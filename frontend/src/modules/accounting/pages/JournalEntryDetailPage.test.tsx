// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, render, screen, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { JournalEntryDetailPage } from "./JournalEntryDetailPage";
import type { JournalEntryDetailDto } from "../api/accountingApi";

/**
 * ACCOUNTING-JOURNAL-LINE-DESCRIPTIONS-EXPENSES-PAYABLES-01 — la columna Descripción de la tabla
 * de líneas debe mostrar `displayDescription` (texto legible resuelto en lectura) cuando el
 * backend lo envía, y caer a `description` (el texto técnico persistido) cuando no.
 */

const mocks = vi.hoisted(() => ({ getJournalEntryById: vi.fn() }));
vi.mock("../api/accountingApi", () => ({
  accountingApi: { getJournalEntryById: mocks.getJournalEntryById },
}));
vi.mock("../../../lib/messages", () => ({ message: { error: vi.fn() } }));

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
    <MemoryRouter initialEntries={["/accounting/journal-entries/entry-1"]}>
      <Routes>
        <Route path="/accounting/journal-entries/:id" element={<JournalEntryDetailPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
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

it("falls back to the raw technical description when displayDescription cannot be resolved", async () => {
  mocks.getJournalEntryById.mockResolvedValue({
    ...BASE_ENTRY,
    lines: BASE_ENTRY.lines.map((l) => ({ ...l, displayDescription: null })),
  });
  show();

  const table = await screen.findByRole("table");
  const rows = within(table).getAllByRole("row");
  expect(within(rows[1]!).getByText("Expenses — DocumentConfirmed — expense-1")).toBeTruthy();
});
