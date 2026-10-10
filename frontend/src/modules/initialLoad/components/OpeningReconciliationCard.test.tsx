// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, configure, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { OpeningReconciliationCard } from "./OpeningReconciliationCard";
import { initialLoadService } from "../api/initialLoadService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import type {
  OpeningBalanceReconciliationDto,
  OpeningReconciliationBatchDto,
} from "../types/importBatch.types";

configure({ asyncUtilTimeout: 5000 });

vi.mock("../api/initialLoadService", () => ({
  initialLoadService: {
    getOpeningReconciliation: vi.fn(),
    postOpeningBalance: vi.fn(),
  },
}));

vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: vi.fn(),
}));

vi.mock("../../../lib/messages", () => ({
  message: { success: vi.fn(), error: vi.fn() },
}));

vi.mock("../../../components/zh/ZHNumberValue", () => ({
  ZHNumberValue: ({ value }: { value: number }) => <span>{value.toFixed(2)}</span>,
}));

function batch(overrides: Partial<OpeningReconciliationBatchDto>): OpeningReconciliationBatchDto {
  return {
    importBatchId: "b1",
    importType: "InitialStock",
    factType: "OpeningInventory",
    label: null,
    batchStatus: "Completed",
    confirmedAt: "2026-09-01T10:00:00Z",
    operationalAmount: 100,
    accountingAmount: 100,
    difference: 0,
    status: "Reconciled",
    postingStatus: "Posted",
    journalEntryId: "je-1",
    journalEntryNumber: 7,
    errorCode: null,
    errorMessage: null,
    canPost: false,
    ...overrides,
  };
}

function dto(batches: OpeningReconciliationBatchDto[], overrides: Partial<OpeningBalanceReconciliationDto> = {}): OpeningBalanceReconciliationDto {
  return {
    cutoffDate: "2026-08-31",
    status: "Reconciled",
    batches,
    types: [],
    bridgeAccount: null,
    canCloseImplementation: false,
    blockers: [
      {
        code: "OPENING_BRIDGE_NOT_CLEARED",
        message: "La cuenta de Saldos de apertura mantiene un saldo pendiente de reclasificación.",
        importBatchId: null,
      },
    ],
    ...overrides,
  };
}

function mockPermissions(granted: string[]) {
  vi.mocked(usePermissionsUi).mockReturnValue({
    canShow: (p: string) => granted.includes(p),
    has: (p: string) => granted.includes(p),
    isAdminRole: false,
  } as ReturnType<typeof usePermissionsUi>);
}

const ALL = ["accounting.view", "accounting.create", "initialload.batches.confirm"];

function renderCard() {
  return render(
    <MemoryRouter>
      <OpeningReconciliationCard />
    </MemoryRouter>,
  );
}

describe("OpeningReconciliationCard (IL-7C)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPermissions(ALL);
  });

  afterEach(() => cleanup());

  it("muestra estados por lote, enlace al asiento y bloqueos de cierre", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([
        batch({}),
        batch({
          importBatchId: "b2",
          importType: "InitialReceivables",
          accountingAmount: 90,
          difference: 10,
          status: "Difference",
          journalEntryId: "je-2",
          journalEntryNumber: 8,
        }),
      ]),
    );

    renderCard();

    expect(await screen.findByText("Inventario inicial")).toBeTruthy();
    expect(screen.getAllByText("Conciliado").length).toBeGreaterThan(0);
    expect(screen.getByText("Con diferencia")).toBeTruthy();
    expect(screen.getByText("N° 7").closest("a")?.getAttribute("href")).toBe(
      "/accounting/journal-entries/je-1",
    );
    expect(screen.getByText(/no puede cerrarse/)).toBeTruthy();
    expect(screen.getByText(/saldo pendiente de reclasificación/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Contabilizar|Reintentar/ })).toBeNull();
  });

  it("lote fallido muestra el error y reintenta con confirmación", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([
        batch({
          importType: "InitialPayables",
          accountingAmount: 0,
          difference: 100,
          status: "PendingPosting",
          postingStatus: "Failed",
          journalEntryId: null,
          journalEntryNumber: null,
          errorCode: "PERIOD_NOT_OPEN",
          errorMessage: "El período contable no está abierto.",
          canPost: true,
        }),
      ]),
    );
    vi.mocked(initialLoadService.postOpeningBalance).mockResolvedValue({
      importBatchId: "b1",
      importType: "InitialPayables",
      factType: "OpeningPayables",
      entryDate: "2026-08-31",
      amount: 100,
      status: "Posted",
      journalEntryId: "je-9",
      postedAt: "2026-10-10T10:00:00Z",
      attempts: 2,
      alreadyPosted: false,
    });

    renderCard();

    expect(await screen.findByText("Pendiente de posting")).toBeTruthy();
    expect(screen.getByText("El período contable no está abierto.")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Reintentar" }));
    fireEvent.click(await screen.findByRole("button", { name: "Contabilizar" }));

    await waitFor(() => expect(initialLoadService.postOpeningBalance).toHaveBeenCalledWith("b1"));
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    expect(initialLoadService.getOpeningReconciliation).toHaveBeenCalledTimes(2);
  });

  it("sin permiso contable de creación no ofrece contabilizar", async () => {
    mockPermissions(["accounting.view"]);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({ status: "PendingPosting", postingStatus: null, canPost: true, journalEntryId: null })]),
    );

    renderCard();

    expect(await screen.findByText("Pendiente de posting")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Contabilizar" })).toBeNull();
  });

  it("sin permiso de ver contabilidad no consulta ni muestra la tarjeta", () => {
    mockPermissions(["initialload.batches.confirm"]);

    const { container } = renderCard();

    expect(container.textContent).toBe("");
    expect(initialLoadService.getOpeningReconciliation).not.toHaveBeenCalled();
  });
});
