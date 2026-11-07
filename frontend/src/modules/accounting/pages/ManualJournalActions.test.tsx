// @vitest-environment jsdom
import { afterEach, expect, it, vi } from "vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ManualJournalActions } from "./ManualJournalActions";
import type { JournalEntryDetailDto } from "../api/accountingApi";

/**
 * ACCOUNTING-FINAL-FIX — «Reversar» depende del reverso resuelto en servidor por la relación persistida
 * OriginalJournalEntryId (`reverseJournalEntryId` del detalle): disponible sin reverso, ausente si ya existe.
 * ACCOUNTING-JOURNAL-UX-01 — metadatos en `.zh-summary-grid`, contenido del asiento (`children`)
 * entre la card de acciones y los intentos, y sin tabla de intentos cuando no hay ninguno.
 */

vi.mock("../api/accountingApi", () => ({ accountingApi: {}, FISCAL_YEAR_CLOSING_FACT: "FiscalYearClosing" }));
const permissions = vi.hoisted(() => ({ canShow: vi.fn((_: string) => true) }));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: permissions.canShow }) }));
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

const MANUAL_DRAFT = {
  ...POSTED_CLOSING,
  id: "manual-1",
  entryNumber: null,
  sourceEventType: "ManualJournalEntry",
  status: "Draft",
  manual: {
    ...POSTED_CLOSING.manual!,
    reference: "REF-9",
    attempts: [
      { id: "a-1", attemptedAtUtc: "2027-01-05T10:00:00Z", attemptedBy: "admin", success: false, errorMessage: "Período cerrado" },
    ],
  },
} as unknown as JournalEntryDetailDto;

function show(entry: JournalEntryDetailDto, children?: React.ReactNode) {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <ManualJournalActions entry={entry} onChanged={() => {}}>{children}</ManualJournalActions>
      </MemoryRouter>
    </I18nProvider>,
  );
}

afterEach(() => {
  cleanup();
  permissions.canShow.mockImplementation(() => true);
});

it("asiento sin reverso: la acción Reversar está disponible", () => {
  show(POSTED_CLOSING);
  expect(screen.getByRole("button", { name: "Reversar" })).toBeTruthy();
});

it("asiento ya reversado: la acción Reversar no está disponible", () => {
  show({ ...POSTED_CLOSING, reverseJournalEntryId: "reversal-1" });
  expect(screen.queryByRole("button", { name: "Reversar" })).toBeNull();
});

it("cierre anual: título propio, metadatos agrupados y sin tabla de intentos vacía", () => {
  const { container } = show(POSTED_CLOSING);
  expect(screen.getByText("Asiento de cierre anual")).toBeTruthy();
  const grid = container.querySelector(".zh-summary-grid")!;
  expect(grid.textContent).toContain("Creado por");
  expect(grid.textContent).toContain("admin");
  expect(screen.queryByText("Intentos de publicación")).toBeNull();
});

it("borrador manual: acciones de borrador, contenido entre acciones e intentos, intentos visibles", () => {
  const { container } = show(MANUAL_DRAFT, <section data-testid="entry-content">contenido</section>);
  expect(screen.getByText("Asiento manual")).toBeTruthy();
  for (const name of ["Editar borrador", "Descartar", "Publicar"]) {
    expect(screen.getByRole("button", { name })).toBeTruthy();
  }
  expect(screen.queryByRole("button", { name: "Reversar" })).toBeNull();
  expect(screen.getByText("REF-9")).toBeTruthy();

  const cards = [...container.querySelectorAll(":scope > .card, :scope > section")];
  const order = cards.map((el) => (el.matches("section") ? "content" : el.querySelector(".card-header")?.textContent ?? ""));
  expect(order[0]).toContain("Asiento manual");
  expect(order[1]).toBe("content");
  expect(order[2]).toContain("Intentos de publicación");
  expect(screen.getByText("Período cerrado")).toBeTruthy();
});

it("sin permisos manuales: no muestra acciones", () => {
  permissions.canShow.mockImplementation(() => false);
  show(MANUAL_DRAFT);
  for (const name of ["Editar borrador", "Descartar", "Publicar", "Reversar"]) {
    expect(screen.queryByRole("button", { name })).toBeNull();
  }
});
