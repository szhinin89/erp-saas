// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import {
  cleanup,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { AccountingPostingsPage } from "./AccountingPostingsPage";
import type { AccountingPostingDto } from "../api/accountingApi";

/**
 * ACCOUNTING-CONTINUITY-01 — la pantalla lista obligaciones contables y ofrece reproceso manual
 * individual solo para Pending/Failed y solo con `accounting.postings.reprocess`.
 */

const mocks = vi.hoisted(() => ({
  listPostings: vi.fn(),
  reprocessPosting: vi.fn(),
  reprocessPostings: vi.fn(),
  getPosting: vi.fn(),
  confirm: vi.fn(),
  success: vi.fn(),
  warning: vi.fn(),
  canShow: vi.fn(),
}));
vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listPostings: mocks.listPostings,
    reprocessPosting: mocks.reprocessPosting,
    reprocessPostings: mocks.reprocessPostings,
    getPosting: mocks.getPosting,
  },
}));
vi.mock("../../../lib/messages", () => ({
  message: {
    error: vi.fn(),
    success: mocks.success,
    warning: mocks.warning,
    confirm: mocks.confirm,
  },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => ({
    canShow: mocks.canShow,
    has: mocks.canShow,
    isAdminRole: false,
  }),
}));

const posting = (
  overrides: Partial<AccountingPostingDto>,
): AccountingPostingDto => ({
  id: "p-1",
  sourceModule: "Purchases",
  factType: "InvoiceReceived",
  sourceEventId: "inv-1",
  sourceDocumentNumber: "001-001-000000001",
  businessDate: "2026-07-25",
  entryDate: "2026-07-25",
  status: "Failed",
  errorCode: "RULE_NOT_FOUND",
  errorMessage: "No existe regla de contabilización.",
  attempts: 1,
  lastAttemptAtUtc: "2026-07-25T15:00:00Z",
  journalEntryId: null,
  createdAt: "2026-07-25T15:00:00Z",
  createdBy: "user-1",
  ...overrides,
});

function show() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <AccountingPostingsPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.canShow.mockReturnValue(true);
});
afterEach(cleanup);

it("reprocesa una obligación Failed tras confirmar y refresca la lista", async () => {
  mocks.listPostings.mockResolvedValue([posting({})]);
  mocks.confirm.mockResolvedValue(true);
  mocks.reprocessPosting.mockResolvedValue(
    posting({ status: "Posted", errorMessage: null }),
  );
  show();

  fireEvent.click(await screen.findByRole("button", { name: "Reprocesar" }));

  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "Correccion" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
  await waitFor(() =>
    expect(mocks.reprocessPosting).toHaveBeenCalledWith("p-1", "Correccion"),
  );
  expect(mocks.success).toHaveBeenCalledWith("Contabilización publicada.");
  expect(mocks.listPostings).toHaveBeenCalledTimes(2);
});

it("no ofrece reprocesar obligaciones Posted ni sin permiso", async () => {
  mocks.listPostings.mockResolvedValue([
    posting({ id: "p-2", status: "Posted", errorMessage: null }),
  ]);
  show();
  await screen.findByText("Contabilizada");
  expect(screen.queryByRole("button", { name: "Reprocesar" })).toBeNull();

  cleanup();
  mocks.canShow.mockReturnValue(false);
  mocks.listPostings.mockResolvedValue([posting({})]);
  show();
  await screen.findByText("No existe regla de contabilización.");
  expect(screen.queryByRole("button", { name: "Reprocesar" })).toBeNull();
});

it("informa cuando el reproceso vuelve a fallar", async () => {
  mocks.listPostings.mockResolvedValue([posting({})]);
  mocks.confirm.mockResolvedValue(true);
  mocks.reprocessPosting.mockResolvedValue(posting({ attempts: 2 }));
  show();

  fireEvent.click(await screen.findByRole("button", { name: "Reprocesar" }));

  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "Correccion" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
  await waitFor(() =>
    expect(mocks.warning).toHaveBeenCalledWith(
      "El intento volvió a fallar: No existe regla de contabilización.",
    ),
  );
});

it("batch requires reason and confirmation and displays individual outcomes", async () => {
  mocks.listPostings.mockResolvedValue([
    posting({}),
    posting({ id: "p-2", status: "Pending" }),
    posting({ id: "p-3", status: "Canceled" }),
  ]);
  mocks.confirm.mockResolvedValue(true);
  mocks.reprocessPostings.mockResolvedValue({
    items: [
      {
        id: "p-1",
        outcome: "Attempted",
        posting: posting({ status: "Posted" }),
      },
      { id: "p-2", outcome: "NotFound", posting: null },
    ],
  });
  show();
  const checks = await screen.findAllByRole("checkbox");
  expect(checks).toHaveLength(2);
  checks.forEach((check) => fireEvent.click(check));
  fireEvent.click(screen.getByRole("button", { name: /Reprocesar selecci/ }));
  expect(
    (screen.getByRole("button", { name: "Continuar" }) as HTMLButtonElement)
      .disabled,
  ).toBe(true);
  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "Correccion" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
  await waitFor(() =>
    expect(mocks.reprocessPostings).toHaveBeenCalledWith(
      ["p-1", "p-2"],
      "Correccion",
    ),
  );
  expect(await screen.findByText("Intentado")).toBeTruthy();
  expect(screen.getByText("No encontrada")).toBeTruthy();
});

it("caps selection at 100 and cancellation sends no request", async () => {
  mocks.listPostings.mockResolvedValue(
    Array.from({ length: 101 }, (_, i) => posting({ id: `p-${i}` })),
  );
  mocks.confirm.mockResolvedValue(false);
  show();
  const checks = await screen.findAllByRole("checkbox");
  checks.slice(0, 100).forEach((check) => fireEvent.click(check));
  expect((checks[100] as HTMLInputElement).disabled).toBe(true);
  fireEvent.click(screen.getByRole("button", { name: /Reprocesar selecci/ }));
  fireEvent.change(screen.getByRole("textbox"), {
    target: { value: "Correccion" },
  });
  fireEvent.click(screen.getByRole("button", { name: "Continuar" }));
  await waitFor(() => expect(mocks.confirm).toHaveBeenCalledOnce());
  expect(mocks.reprocessPostings).not.toHaveBeenCalled();
}, 15000);
