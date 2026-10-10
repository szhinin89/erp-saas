// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, configure, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { AxiosError, type AxiosResponse } from "axios";
import { OpeningReconciliationCard } from "./OpeningReconciliationCard";
import { initialLoadService } from "../api/initialLoadService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { message } from "../../../lib/messages";
import { accountLookupFacade, type AccountDto } from "../../accounting/facades/accountLookupFacade";
import type {
  OpeningBalanceReconciliationDto,
  OpeningReconciliationBatchDto,
  OpeningReconciliationJournalEntryDto,
} from "../types/importBatch.types";

configure({ asyncUtilTimeout: 5000 });

vi.mock("../api/initialLoadService", () => ({
  initialLoadService: {
    getOpeningReconciliation: vi.fn(),
    postOpeningBalance: vi.fn(),
    publishOpeningJournal: vi.fn(),
    reverseOpeningJournal: vi.fn(),
    closeInitialLoad: vi.fn(),
  },
}));

vi.mock("../../accounting/facades/accountLookupFacade", () => ({
  accountLookupFacade: { listAccounts: vi.fn() },
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

function journal(
  overrides: Partial<OpeningReconciliationJournalEntryDto> = {},
): OpeningReconciliationJournalEntryDto {
  return {
    state: "Posted",
    postingId: "p1",
    version: 1,
    postingStatus: "Posted",
    entryDate: "2026-08-31",
    totalAmount: 250,
    lineCount: 2,
    journalEntryId: "je-asi",
    journalEntryNumber: 99,
    postedAt: "2026-09-02T15:00:00Z",
    attempts: 1,
    errorCode: null,
    errorMessage: null,
    versionCount: 1,
    lastSupersededVersion: null,
    lastSupersededAt: null,
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
    openingJournalEntry: journal(),
    canCloseImplementation: false,
    blockers: [
      {
        code: "OPENING_BRIDGE_NOT_CLEARED",
        message: "La cuenta de Saldos de apertura mantiene un saldo pendiente de reclasificación.",
        importBatchId: null,
      },
    ],
    isClosed: false,
    closedAt: null,
    closedBy: null,
    closedByName: null,
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

const BRIDGE = {
  accountId: "acc-bridge",
  accountCode: "3.9.01",
  accountName: "Saldos de apertura",
  balanceAtCutoff: 250,
  currentBalance: 250,
  fromOpeningPostings: 250,
  pendingReclassification: 250,
};

function account(id: string, code: string, name: string, overrides: Partial<AccountDto> = {}): AccountDto {
  return {
    id,
    code,
    name,
    parentAccountId: null,
    parentAccountCode: null,
    parentAccountName: null,
    level: 3,
    accountType: "Equity",
    nature: "Credit",
    allowsPosting: true,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    ...overrides,
  };
}

function apiError(code: string, msg: string): AxiosError {
  return new AxiosError("Request failed", "ERR_BAD_REQUEST", undefined, undefined, {
    status: 400,
    statusText: "Bad Request",
    headers: {},
    config: {},
    data: { success: false, code, message: msg },
  } as unknown as AxiosResponse);
}

const MISSING = {
  state: "Missing",
  postingId: null,
  version: null,
  postingStatus: null,
  entryDate: null,
  totalAmount: null,
  lineCount: null,
  journalEntryId: null,
  journalEntryNumber: null,
  postedAt: null,
  attempts: 0,
  versionCount: 0,
} as const;

const CORRECT = [...ALL, "accounting.delete"];

describe("OpeningReconciliationCard — ASI de apertura (IL-8D)", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPermissions(ALL);
    vi.mocked(accountLookupFacade.listAccounts).mockResolvedValue([
      account("acc-bridge", "3.9.01", "Saldos de apertura"),
      account("acc-capital", "3.1.01", "Capital social"),
      account("acc-inactive", "3.1.99", "Inactiva", { isActive: false }),
      account("acc-group", "3.1", "Patrimonio grupo", { allowsPosting: false }),
    ]);
  });

  afterEach(() => cleanup());

  it("OpeningJournalPending no rompe la página: badge 'ASI pendiente' y ASI Missing publicable", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], { status: "OpeningJournalPending", openingJournalEntry: journal(MISSING) }),
    );

    renderCard();

    expect(await screen.findByText("ASI pendiente")).toBeTruthy();
    expect(screen.getByText("Sin publicar")).toBeTruthy();
    expect(screen.getByText("Aún no se ha publicado ninguna versión.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Publicar asiento de apertura" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Corregir apertura" })).toBeNull();
  });

  it("un status desconocido del backend se muestra en un badge neutro sin tumbar el hub", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], {
        status: "SomeFutureStatus" as OpeningBalanceReconciliationDto["status"],
        openingJournalEntry: journal({
          state: "SomeFutureState" as OpeningReconciliationJournalEntryDto["state"],
        }),
      }),
    );

    renderCard();

    expect(await screen.findByText("SomeFutureStatus")).toBeTruthy();
    expect(screen.getByText("SomeFutureState")).toBeTruthy();
    expect(screen.getByText("Inventario inicial")).toBeTruthy();
  });

  it("Failed muestra el error del backend y ofrece reintentar", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], {
        status: "OpeningJournalPending",
        openingJournalEntry: journal({
          state: "Failed",
          postingStatus: "Failed",
          journalEntryId: null,
          journalEntryNumber: null,
          postedAt: null,
          errorCode: "OPENING_BRIDGE_NOT_CLEARED",
          errorMessage: "La cuenta puente no queda en cero.",
        }),
      }),
    );

    renderCard();

    expect(await screen.findByText("Falló")).toBeTruthy();
    expect(screen.getByText("La cuenta puente no queda en cero.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Reintentar publicación" })).toBeTruthy();
  });

  it("Posted muestra versión, enlace al asiento, historial y 'Corregir apertura' (con permiso)", async () => {
    mockPermissions(CORRECT);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], {
        openingJournalEntry: journal({ version: 2, versionCount: 2, lastSupersededVersion: 1 }),
      }),
    );

    renderCard();

    expect(await screen.findByText("Publicado")).toBeTruthy();
    expect(screen.getByText("N° 99").closest("a")?.getAttribute("href")).toBe(
      "/accounting/journal-entries/je-asi",
    );
    expect(screen.getByText(/Versiones registradas: 2\..*Última versión corregida: v1/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Corregir apertura" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Publicar|Reintentar publicación/ })).toBeNull();
    expect(screen.queryByText(/Anular/)).toBeNull();
  });

  it("ReversedNotReplaced muestra 'Apertura incompleta' y permite publicar nueva versión", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], {
        status: "OpeningJournalPending",
        openingJournalEntry: journal({
          ...MISSING,
          state: "ReversedNotReplaced",
          versionCount: 1,
          lastSupersededVersion: 1,
          lastSupersededAt: "2026-10-01T12:00:00Z",
        }),
      }),
    );

    renderCard();

    expect((await screen.findAllByText("Apertura incompleta")).length).toBeGreaterThan(0);
    expect(screen.getByText(/La versión 1 fue reversada/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Publicar nueva versión" })).toBeTruthy();
  });

  it("sin accounting.delete no ofrece corregir; sin accounting.create no ofrece publicar", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(dto([batch({})]));
    renderCard();
    expect(await screen.findByText("Publicado")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Corregir apertura" })).toBeNull();
    cleanup();

    mockPermissions(["accounting.view", "initialload.batches.confirm"]);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], { openingJournalEntry: journal(MISSING) }),
    );
    renderCard();
    expect(await screen.findByText("Sin publicar")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Publicar/ })).toBeNull();
  });

  it("cuenta puente pendiente: warning; publicar con totales Debe/Haber y ayuda 'Agregar cuenta puente'", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], {
        status: "OpeningJournalPending",
        bridgeAccount: BRIDGE,
        openingJournalEntry: journal(MISSING),
      }),
    );
    vi.mocked(initialLoadService.publishOpeningJournal).mockResolvedValue({
      id: "p1",
      version: 1,
      entryDate: "2026-08-31",
      totalAmount: 250,
      lineCount: 2,
      status: "Posted",
      journalEntryId: "je-asi",
      postedAt: "2026-10-10T10:00:00Z",
      attempts: 1,
      alreadyPosted: false,
    });

    renderCard();

    expect(await screen.findByText(/tiene saldo pendiente de reclasificación/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Publicar asiento de apertura" }));

    // Solo cuentas activas que admiten movimiento.
    await waitFor(() =>
      expect(screen.getAllByRole("option", { name: /Capital social/ }).length).toBeGreaterThan(0),
    );
    expect(screen.queryByRole("option", { name: /Inactiva/ })).toBeNull();
    expect(screen.queryByRole("option", { name: /Patrimonio grupo/ })).toBeNull();

    fireEvent.change(screen.getByLabelText("Cuenta línea 1"), { target: { value: "acc-capital" } });
    fireEvent.change(screen.getByLabelText("Haber línea 1"), { target: { value: "250" } });
    fireEvent.click(screen.getAllByRole("button", { name: "Eliminar línea" })[1]);
    // Ayuda UI: agrega la cuenta puente por el pendiente actual (saldo acreedor → Debe).
    fireEvent.click(screen.getByRole("button", { name: "Agregar cuenta puente" }));

    await waitFor(() => expect(screen.getByLabelText("Total Debe").textContent).toBe("250.00"));
    expect(screen.getByLabelText("Total Haber").textContent).toBe("250.00");
    expect(screen.getByLabelText("Diferencia").textContent).toBe("0.00");

    fireEvent.click(screen.getByRole("button", { name: "Publicar" }));

    await waitFor(() =>
      expect(initialLoadService.publishOpeningJournal).toHaveBeenCalledWith([
        { accountId: "acc-capital", debit: 0, credit: 250, description: null },
        {
          accountId: "acc-bridge",
          debit: 250,
          credit: 0,
          description: "Reclasificación de Saldos de apertura",
        },
      ]),
    );
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    expect(initialLoadService.getOpeningReconciliation).toHaveBeenCalledTimes(2);
  });

  it("totales Debe/Haber reflejan una diferencia sin bloquear (el backend valida el cuadre)", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], { openingJournalEntry: journal(MISSING) }),
    );

    renderCard();

    fireEvent.click(await screen.findByRole("button", { name: "Publicar asiento de apertura" }));
    fireEvent.change(await screen.findByLabelText("Debe línea 1"), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("Haber línea 2"), { target: { value: "40" } });

    await waitFor(() => expect(screen.getByLabelText("Total Debe").textContent).toBe("100.00"));
    expect(screen.getByLabelText("Total Haber").textContent).toBe("40.00");
    expect(screen.getByLabelText("Diferencia").textContent).toBe("60.00");
  });

  it("corregir apertura: motivo obligatorio, advertencia de saldos históricos, reversa y recarga", async () => {
    mockPermissions(CORRECT);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(dto([batch({})]));
    vi.mocked(initialLoadService.reverseOpeningJournal).mockResolvedValue({
      postingId: "p1",
      version: 1,
      journalEntryId: "je-asi",
      reversalJournalEntryId: "je-rev",
      reversedAtUtc: "2026-10-10T10:00:00Z",
      reason: "Capital mal clasificado",
      alreadyReversed: false,
    });

    renderCard();

    fireEvent.click(await screen.findByRole("button", { name: "Corregir apertura" }));
    expect(
      await screen.findByText("Corregir la apertura cambia los saldos históricos desde 31/08/2026."),
    ).toBeTruthy();

    const confirm = () => screen.getAllByRole("button", { name: "Corregir apertura" }).at(-1) as HTMLElement;
    fireEvent.click(confirm());
    expect(await screen.findByText("El motivo de la corrección es obligatorio.")).toBeTruthy();
    expect(initialLoadService.reverseOpeningJournal).not.toHaveBeenCalled();

    fireEvent.change(screen.getByLabelText(/Motivo de la corrección/), {
      target: { value: "Capital mal clasificado" },
    });
    fireEvent.click(confirm());

    await waitFor(() =>
      expect(initialLoadService.reverseOpeningJournal).toHaveBeenCalledWith("p1", "Capital mal clasificado"),
    );
    await waitFor(() => expect(initialLoadService.getOpeningReconciliation).toHaveBeenCalledTimes(2));
  });

  it("PERIOD_NOT_OPEN muestra el mensaje de período cerrado", async () => {
    mockPermissions(CORRECT);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(dto([batch({})]));
    vi.mocked(initialLoadService.reverseOpeningJournal).mockRejectedValue(
      apiError("PERIOD_NOT_OPEN", "El período 2026-08 está cerrado."),
    );

    renderCard();

    fireEvent.click(await screen.findByRole("button", { name: "Corregir apertura" }));
    fireEvent.change(await screen.findByLabelText(/Motivo de la corrección/), {
      target: { value: "Ajuste" },
    });
    fireEvent.click(screen.getAllByRole("button", { name: "Corregir apertura" }).at(-1) as HTMLElement);

    expect(
      await screen.findByText(
        "La apertura pertenece a un período contable cerrado y ya no puede modificarse. Registre un ajuste contable en un período abierto.",
      ),
    ).toBeTruthy();
    expect(initialLoadService.getOpeningReconciliation).toHaveBeenCalledTimes(1);
  });

  // ── IL-8E: cierre definitivo de la carga inicial ──────────────────────────────────────────

  it("con blockers el botón 'Cerrar carga inicial' está deshabilitado y los blockers se muestran", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(dto([batch({})]));

    renderCard();

    const button = await screen.findByRole("button", { name: "Cerrar carga inicial" });
    expect((button as HTMLButtonElement).disabled).toBe(true);
    expect(screen.getByText(/no puede cerrarse todavía/)).toBeTruthy();
    expect(screen.getByText(/saldo pendiente de reclasificación/)).toBeTruthy();
  });

  it("sin blockers cierra solo tras confirmar la acción irreversible y recarga", async () => {
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], { canCloseImplementation: true, blockers: [] }),
    );
    vi.mocked(initialLoadService.closeInitialLoad).mockResolvedValue({
      closedAt: "2026-10-10T15:00:00Z",
      closedBy: "u1",
      alreadyClosed: false,
    });

    renderCard();

    fireEvent.click(await screen.findByRole("button", { name: "Cerrar carga inicial" }));
    expect(await screen.findByText(/DEFINITIVA e irreversible/)).toBeTruthy();
    expect(initialLoadService.closeInitialLoad).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Cerrar definitivamente" }));

    await waitFor(() => expect(initialLoadService.closeInitialLoad).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(message.success).toHaveBeenCalled());
    expect(initialLoadService.getOpeningReconciliation).toHaveBeenCalledTimes(2);
  });

  it("sin permiso para cerrar no ofrece el botón", async () => {
    mockPermissions(["accounting.view"]);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto([batch({})], { canCloseImplementation: true, blockers: [] }),
    );

    renderCard();

    expect(await screen.findByText(/puede cerrarse/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Cerrar carga inicial" })).toBeNull();
  });

  it("cerrada: muestra fecha y usuario del cierre y oculta contabilizar, publicar y corregir", async () => {
    mockPermissions([...ALL, "accounting.delete"]);
    vi.mocked(initialLoadService.getOpeningReconciliation).mockResolvedValue(
      dto(
        [batch({ status: "PendingPosting", postingStatus: null, canPost: true, journalEntryId: null })],
        {
          blockers: [],
          isClosed: true,
          closedAt: "2026-10-10T15:00:00Z",
          closedBy: "u1",
          closedByName: "Ana Pérez",
        },
      ),
    );

    renderCard();

    expect(await screen.findByText(/Carga inicial cerrada el .+ por Ana Pérez\./)).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Cerrar carga inicial" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Contabilizar|Reintentar/ })).toBeNull();
    expect(screen.queryByRole("button", { name: "Corregir apertura" })).toBeNull();
    expect(screen.queryByRole("button", { name: /Publicar/ })).toBeNull();
  });
});
