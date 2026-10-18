// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { ManualJournalActions } from "./ManualJournalActions";
import type { JournalEntryDetailDto } from "../api/accountingApi";

/**
 * ACCOUNTING-FINAL-FIX — «Reversar» depende del reverso resuelto en servidor por la relación persistida
 * OriginalJournalEntryId (`reverseJournalEntryId` del detalle): disponible sin reverso, ausente si ya existe.
 */

vi.mock("../api/accountingApi", () => ({ accountingApi: {}, FISCAL_YEAR_CLOSING_FACT: "FiscalYearClosing" }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: () => true }) }));
vi.mock("../../../lib/messages", () => ({ message: { confirm: vi.fn(), error: vi.fn() } }));

const POSTED_CLOSING = {
  id: "closing-1",
  entryNumber: 7,
  entryDate: "2026-12-31",
  sourceModule: "Accounting",
  sourceEventType: "FiscalYearClosing",
  status: "Posted",
  createdAt: "2027-01-05T00:00:00Z",
  reverseJournalEntryId: null,
  manual: {
    reference: null, version: 2, createdBy: "admin", updatedBy: null,
    discardedAtUtc: null, discardedBy: null, discardReason: null, attempts: [],
  },
} as unknown as JournalEntryDetailDto;

function show(entry: JournalEntryDetailDto) {
  return render(
    <MemoryRouter>
      <ManualJournalActions entry={entry} onChanged={() => {}} />
    </MemoryRouter>,
  );
}

afterEach(cleanup);

it("asiento sin reverso: la acción Reversar está disponible", () => {
  show(POSTED_CLOSING);
  expect(screen.getByRole("button", { name: "Reversar" })).toBeTruthy();
});

it("asiento ya reversado: la acción Reversar no está disponible", () => {
  show({ ...POSTED_CLOSING, reverseJournalEntryId: "reversal-1" });
  expect(screen.queryByRole("button", { name: "Reversar" })).toBeNull();
});
