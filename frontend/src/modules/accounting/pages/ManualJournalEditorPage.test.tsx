// @vitest-environment jsdom
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { ManualJournalEditorPage } from "./ManualJournalEditorPage";
import type { AccountDto, JournalEntryDetailDto } from "../api/accountingApi";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";

/**
 * ACCOUNTING-JOURNAL-UX-01 — editor de asiento manual (alta y edición comparten la página):
 * distribución, selector de cuentas `ZhSearchSelect`, importes no negativos y vacíos, totales y
 * aviso de cuadre informativo, `ZHFormActions`, confirmación antes de publicar, fallo de
 * publicación visible, permisos, errores y carga — sin cambiar contratos ni reglas.
 */

const mocks = vi.hoisted(() => ({
  listAccounts: vi.fn(),
  getJournalEntryById: vi.fn(),
  createManualJournal: vi.fn(),
  editManualJournal: vi.fn(),
  postManualJournal: vi.fn(),
  canShow: vi.fn((_: string) => true),
  confirm: vi.fn(),
  error: vi.fn(),
}));
vi.mock("../api/accountingApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/accountingApi")>()),
  accountingApi: {
    listAccounts: mocks.listAccounts,
    getJournalEntryById: mocks.getJournalEntryById,
    createManualJournal: mocks.createManualJournal,
    editManualJournal: mocks.editManualJournal,
    postManualJournal: mocks.postManualJournal,
  },
}));
vi.mock("../../../access/usePermissionsUi", () => ({ usePermissionsUi: () => ({ canShow: mocks.canShow }) }));
vi.mock("../../../lib/messages", () => ({ message: { confirm: mocks.confirm, error: mocks.error } }));

const account = (id: string, code: string, name: string, extra: Partial<AccountDto> = {}): AccountDto => ({
  id, code, name, parentAccountId: null, parentAccountCode: null, parentAccountName: null, level: 3,
  accountType: "Asset", nature: "Debit", allowsPosting: true, isActive: true, createdAt: "2026-01-01T00:00:00Z",
  updatedAt: null, ...extra,
});
const CASH = account("11111111-1111-4111-8111-111111111111", "1.1.01.001", "Caja general");
const BANK = account("22222222-2222-4222-8222-222222222222", "1.1.02.001", "Bancos");
const GROUP = account("33333333-3333-4333-8333-333333333333", "1.1", "Activo corriente", { allowsPosting: false });
const INACTIVE = account("44444444-4444-4444-8444-444444444444", "1.1.09.001", "Caja chica antigua", { isActive: false });

const DRAFT = {
  id: "draft-1", entryNumber: null, entryDate: "2026-11-03", status: "Draft", description: "Ajuste de caja",
  sourceModule: "Accounting", sourceEventType: "ManualJournalEntry",
  lines: [
    { id: "l1", accountId: CASH.id, accountCode: CASH.code, accountName: CASH.name, description: "Sobrante", debit: 25.5, credit: 0, sortOrder: 0 },
    { id: "l2", accountId: BANK.id, accountCode: BANK.code, accountName: BANK.name, description: null, debit: 0, credit: 25.5, sortOrder: 1 },
  ],
  manual: { reference: "AJ-1", version: 4, createdBy: "admin", updatedBy: null, discardedAtUtc: null, discardedBy: null, discardReason: null, attempts: [] },
} as unknown as JournalEntryDetailDto;

function show(path = "/accounting/journal-entries/new") {
  return render(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/accounting/journal-entries/new" element={<ManualJournalEditorPage />} />
          <Route path="/accounting/journal-entries/:id/edit" element={<ManualJournalEditorPage />} />
          <Route path="/accounting/journal-entries/:id" element={<p>detalle abierto</p>} />
          <Route path="/accounting/journal-entries" element={<p>listado abierto</p>} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  );
}

const ready = () => screen.findByRole("heading", { name: /asiento manual/ }).then(() => screen.findByLabelText("Cuenta línea 1"));
const input = (label: string | RegExp) => screen.getByLabelText(label) as HTMLInputElement;
// «Concepto» es obligatorio: su etiqueta incluye el asterisco oficial de ZHField.
const concept = /^Concepto/;

function pickAccount(line: number, query: string, label: string) {
  const box = screen.getByRole("combobox", { name: `Cuenta línea ${line}` });
  fireEvent.focus(box);
  fireEvent.change(box, { target: { value: query } });
  fireEvent.click(screen.getByRole("option", { name: new RegExp(label) }));
}

async function fillBalancedDraft() {
  await ready();
  fireEvent.change(input(concept), { target: { value: "Ajuste de caja" } });
  pickAccount(1, "1.1.01", "Caja general");
  pickAccount(2, "Bancos", "Bancos");
  fireEvent.change(input("Debe línea 1"), { target: { value: "10" } });
  fireEvent.change(input("Haber línea 2"), { target: { value: "10" } });
}

beforeEach(() => {
  vi.clearAllMocks();
  mocks.canShow.mockImplementation(() => true);
  mocks.confirm.mockResolvedValue(true);
  mocks.listAccounts.mockResolvedValue([CASH, BANK, GROUP, INACTIVE]);
  mocks.createManualJournal.mockResolvedValue({ id: "new-1", status: "Draft", version: 1, entryNumber: null });
  mocks.editManualJournal.mockResolvedValue({ id: "draft-1", status: "Draft", version: 5, entryNumber: null });
  mocks.postManualJournal.mockResolvedValue({ id: "new-1", status: "Posted", version: 2, entryNumber: 9 });
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
});
afterEach(cleanup);

it("estado de carga oficial mientras se obtienen las cuentas", () => {
  mocks.listAccounts.mockReturnValue(new Promise(() => {}));
  const { container } = show();
  expect(container.querySelector(".loading-state")).toBeTruthy();
});

it("alta: valores iniciales, tres campos en una fila, dos líneas vacías y aviso de publicación", async () => {
  const { container } = show();
  await ready();
  expect(screen.getByRole("heading", { name: "Nuevo asiento manual" })).toBeTruthy();
  const grid = container.querySelector(".zh-grid--3")!;
  expect([...grid.querySelectorAll(".zh-field-label")].map((l) => l.textContent?.replace("*", ""))).toEqual([
    "Fecha contable", "Referencia", "Concepto",
  ]);
  expect(input("Fecha contable").value).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  for (const label of ["Debe línea 1", "Haber línea 1", "Debe línea 2", "Haber línea 2"]) expect(input(label).value).toBe("");
  expect(screen.getByText(/Puedes guardar un borrador aunque no esté cuadrado/)).toBeTruthy();
  // Sin importes no se muestra aviso de cuadre.
  expect(screen.queryByText("Asiento cuadrado")).toBeNull();
});

it("guardar borrador: payload con importes vacíos como 0, clave de idempotencia y navegación al detalle", async () => {
  show();
  await fillBalancedDraft();
  fireEvent.change(input("Debe línea 2"), { target: { value: "" } });
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await screen.findByText("detalle abierto");
  expect(mocks.confirm).not.toHaveBeenCalled();
  expect(mocks.postManualJournal).not.toHaveBeenCalled();
  const payload = mocks.createManualJournal.mock.calls[0]![0];
  expect(payload).toMatchObject({
    concept: "Ajuste de caja",
    reference: null,
    lines: [
      { accountId: CASH.id, description: null, debit: 10, credit: 0 },
      { accountId: BANK.id, description: null, debit: 0, credit: 10 },
    ],
  });
  expect(typeof payload.idempotencyKey).toBe("string");
});

it("borrador descuadrado: aviso informativo y se permite guardar", async () => {
  show();
  await fillBalancedDraft();
  fireEvent.change(input("Haber línea 2"), { target: { value: "7.5" } });
  const notice = screen.getByText("Asiento descuadrado").closest(".zh-page-notice")!;
  expect(notice.querySelector(".zh-form-alert--warning")).toBeTruthy();
  expect(notice.textContent).toContain("Diferencia: $2.50");
  expect(notice.textContent).toContain("Puede guardarse como borrador");
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await screen.findByText("detalle abierto");
  expect(mocks.createManualJournal).toHaveBeenCalledOnce();
});

it("totales y diferencia en el pie de la tabla (decimal.js, sin error de coma flotante)", async () => {
  show();
  await ready();
  fireEvent.change(input("Debe línea 1"), { target: { value: "0.1" } });
  fireEvent.change(input("Debe línea 2"), { target: { value: "0.2" } });
  fireEvent.change(input("Haber línea 2"), { target: { value: "0.3" } });
  const foot = screen.getByRole("table").querySelector("tfoot")!;
  expect(within(foot).getByLabelText("Debe").textContent).toContain("$0.30");
  expect(within(foot).getByLabelText("Haber").textContent).toContain("$0.30");
  expect(within(foot).getByLabelText("Diferencia (Debe − Haber)").textContent).toContain("$0.00");
  expect(screen.getByText("Asiento cuadrado").closest(".zh-page-notice")!.textContent).toContain("Diferencia: $0.00");
});

it("importes negativos: positiveOnly bloquea el signo al teclear y lo neutraliza al salir del campo", async () => {
  show();
  await ready();
  const debit = input("Debe línea 1");
  // keyDown devuelve false cuando ZhDecimalInput hace preventDefault del «-».
  expect(fireEvent.keyDown(debit, { key: "-" })).toBe(false);
  fireEvent.focus(debit);
  fireEvent.change(debit, { target: { value: "-5" } });
  fireEvent.blur(debit);
  expect(debit.value).toBe("0.00");
});

it("selector de cuentas: busca por código y nombre, marca cuentas no habilitadas y conserva la selección", async () => {
  show();
  await ready();
  const box = screen.getByRole("combobox", { name: "Cuenta línea 1" });
  fireEvent.focus(box);
  fireEvent.change(box, { target: { value: "1.1" } });
  const options = screen.getAllByRole("option").map((o) => o.textContent);
  expect(options.some((o) => o?.includes("1.1 — Activo corriente") && o.includes("No habilitada para publicar"))).toBe(true);
  fireEvent.change(box, { target: { value: "bancos" } });
  expect(screen.getAllByRole("option").map((o) => o.textContent)).toEqual([expect.stringContaining("1.1.02.001 — Bancos")]);
  fireEvent.click(screen.getByRole("option"));
  expect(screen.getByText("1.1.02.001 — Bancos")).toBeTruthy();
  expect(screen.queryByRole("combobox", { name: "Cuenta línea 1" })).toBeNull();
});

it("validaciones Zod: concepto y cuentas obligatorios, error por línea visible en su campo", async () => {
  show();
  await ready();
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  expect(await screen.findByText("El concepto es obligatorio")).toBeTruthy();
  const accountErrors = screen.getAllByText("Seleccione una cuenta");
  expect(accountErrors).toHaveLength(2);
  expect(accountErrors[0]!.closest(".zh-field--compact")).toBeTruthy();
  expect(mocks.createManualJournal).not.toHaveBeenCalled();
});

it("agregar y eliminar líneas (se permite quitar todas, como antes)", async () => {
  show();
  await ready();
  const addLine = screen.getByRole("button", { name: /Agregar línea/ });
  expect(addLine.closest(".card-header")).toBeTruthy();
  fireEvent.click(addLine);
  expect(screen.getByLabelText("Cuenta línea 3")).toBeTruthy();
  for (let i = 0; i < 3; i++) fireEvent.click(screen.getAllByRole("button", { name: "Eliminar línea" })[0]!);
  expect(screen.queryByLabelText("Cuenta línea 1")).toBeNull();
});

it("guardar y publicar: pide confirmación; si se cancela no guarda", async () => {
  mocks.confirm.mockResolvedValue(false);
  show();
  await fillBalancedDraft();
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  await waitFor(() => expect(mocks.confirm).toHaveBeenCalledOnce());
  expect(mocks.confirm.mock.calls[0]![0]).toMatchObject({ confirmLabel: "Publicar" });
  expect(mocks.createManualJournal).not.toHaveBeenCalled();
});

it("guardar y publicar confirmado: guarda, publica con la versión devuelta y navega al detalle", async () => {
  show();
  await fillBalancedDraft();
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  await screen.findByText("detalle abierto");
  expect(mocks.postManualJournal).toHaveBeenCalledWith("new-1", 1, expect.any(String));
  expect(mocks.error).not.toHaveBeenCalled();
});

it("publicación fallida: el borrador queda guardado, mensaje visible y mismo destino (detalle con intentos)", async () => {
  mocks.postManualJournal.mockRejectedValue(new Error("x"));
  show();
  await fillBalancedDraft();
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  await screen.findByText("detalle abierto");
  expect(mocks.error).toHaveBeenCalledOnce();
  expect(mocks.error.mock.calls[0]![0]).toContain("El borrador se guardó, pero no se pudo publicar");
});

it("edición: recupera el borrador (cuentas, importes, referencia) y envía la versión", async () => {
  mocks.getJournalEntryById.mockResolvedValue(DRAFT);
  show("/accounting/journal-entries/draft-1/edit");
  expect(await screen.findByRole("heading", { name: "Editar asiento manual" })).toBeTruthy();
  expect(input(concept).value).toBe("Ajuste de caja");
  expect(input("Referencia").value).toBe("AJ-1");
  expect(screen.getByText("1.1.01.001 — Caja general")).toBeTruthy();
  expect(screen.getByText("1.1.02.001 — Bancos")).toBeTruthy();
  expect(input("Debe línea 1").value).toBe("25.5");
  // Importes en cero se presentan vacíos (mismo significado numérico).
  expect(input("Haber línea 1").value).toBe("");
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await screen.findByText("detalle abierto");
  expect(mocks.editManualJournal).toHaveBeenCalledWith("draft-1", 4, expect.objectContaining({ reference: "AJ-1" }));
});

it("edición de un asiento que no es borrador: aviso y formulario bloqueado", async () => {
  mocks.getJournalEntryById.mockResolvedValue({ ...DRAFT, status: "Posted" });
  const { container } = show("/accounting/journal-entries/draft-1/edit");
  await screen.findByRole("heading", { name: "Editar asiento manual" });
  expect(container.querySelector(".zh-form-alert--error")).toBeTruthy();
  expect(input(concept).disabled).toBe(true);
  expect(input("Debe línea 1").disabled).toBe(true);
  expect((screen.getByRole("button", { name: /Agregar línea/ }) as HTMLButtonElement).disabled).toBe(true);
  expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(true);
});

it("permisos: sin crear → aviso y guardado deshabilitado; sin publicar → no aparece «Guardar y publicar»; Cancelar siempre disponible", async () => {
  mocks.canShow.mockImplementation((p: string) => p !== "accounting.manual.create" && p !== "accounting.manual.post");
  show();
  await ready();
  expect(screen.getByText("No tiene permiso para guardar este asiento.")).toBeTruthy();
  expect((screen.getByRole("button", { name: "Guardar borrador" }) as HTMLButtonElement).disabled).toBe(true);
  expect(screen.queryByRole("button", { name: "Guardar y publicar" })).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
  expect(await screen.findByText("listado abierto")).toBeTruthy();
});

it("permiso de edición: en /edit se exige accounting.manual.edit", async () => {
  mocks.getJournalEntryById.mockResolvedValue(DRAFT);
  mocks.canShow.mockImplementation((p: string) => p !== "accounting.manual.edit");
  show("/accounting/journal-entries/draft-1/edit");
  await screen.findByRole("heading", { name: "Editar asiento manual" });
  expect(screen.getByText("No tiene permiso para guardar este asiento.")).toBeTruthy();
});

it("error del servidor al guardar: mensaje visible y permanece en el editor", async () => {
  mocks.createManualJournal.mockRejectedValue(new Error("fallo"));
  const { container } = show();
  await fillBalancedDraft();
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await waitFor(() => expect(container.querySelector(".zh-form-alert--error")).toBeTruthy());
  expect(screen.queryByText("detalle abierto")).toBeNull();
  expect(screen.getByRole("heading", { name: "Nuevo asiento manual" })).toBeTruthy();
});

// ── Validación previa de cuentas no imputables (POSTING_ACCOUNT_INVALID anticipado) ───────────

const hintOf = (line: number) => screen.getAllByRole("row")[line]!.querySelector(".zh-field-hint")?.textContent ?? null;

async function draftWith(line1: [string, string]) {
  await ready();
  fireEvent.change(input(concept), { target: { value: "Ajuste" } });
  pickAccount(1, line1[0], line1[1]);
  pickAccount(2, "1.1.01", "Caja general");
  fireEvent.change(input("Debe línea 1"), { target: { value: "5" } });
  fireEvent.change(input("Haber línea 2"), { target: { value: "5" } });
}

it("cuenta agrupadora seleccionada: sigue disponible en el selector y muestra aviso contextual en su línea", async () => {
  show();
  await draftWith(["Activo", "1.1 — Activo corriente"]);
  expect(hintOf(1)).toBe("Cuenta agrupadora o inactiva: no se puede publicar con ella.");
  expect(screen.getAllByRole("row")[1]!.querySelector(".zh-field-hint--warning")).toBeTruthy();
  expect(hintOf(2)).toBeNull();
});

it("cuenta inactiva seleccionada: mismo aviso contextual", async () => {
  show();
  await draftWith(["Caja chica", "Caja chica antigua"]);
  expect(hintOf(1)).toBe("Cuenta agrupadora o inactiva: no se puede publicar con ella.");
});

it("guardar y publicar con cuenta no imputable: bloquea antes de confirmar, sin llamadas al API, error por línea y aviso general", async () => {
  show();
  await draftWith(["Activo", "1.1 — Activo corriente"]);
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  expect(await screen.findByText("Seleccione una cuenta de movimiento activa para publicar.")).toBeTruthy();
  const notice = screen.getByText("No se puede publicar: hay cuentas agrupadoras o inactivas.").closest(".zh-page-notice")!;
  expect(notice.textContent).toContain("1.1 — Activo corriente");
  // El error de RHF reemplaza al aviso en la línea afectada; la otra línea no se marca.
  expect(screen.getAllByRole("row")[1]!.querySelector(".zh-field-hint--error")).toBeTruthy();
  expect(screen.getAllByRole("row")[2]!.querySelector(".zh-field-hint--error")).toBeNull();
  expect(mocks.confirm).not.toHaveBeenCalled();
  expect(mocks.createManualJournal).not.toHaveBeenCalled();
  expect(mocks.editManualJournal).not.toHaveBeenCalled();
  expect(mocks.postManualJournal).not.toHaveBeenCalled();
});

it("guardar borrador con cuenta no imputable: permitido, sin restricciones nuevas", async () => {
  show();
  await draftWith(["Caja chica", "Caja chica antigua"]);
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await screen.findByText("detalle abierto");
  expect(mocks.createManualJournal.mock.calls[0]![0].lines[0]).toMatchObject({ accountId: INACTIVE.id, debit: 5 });
  expect(screen.queryByText(/No se puede publicar/)).toBeNull();
});

it("borrador anterior con cuenta agrupadora: aviso al cargar, publicación bloqueada y borrador guardable", async () => {
  mocks.getJournalEntryById.mockResolvedValue({
    ...DRAFT,
    lines: [{ ...DRAFT.lines[0]!, accountId: GROUP.id, accountCode: GROUP.code, accountName: GROUP.name }, DRAFT.lines[1]!],
  });
  show("/accounting/journal-entries/draft-1/edit");
  await screen.findByRole("heading", { name: "Editar asiento manual" });
  expect(await screen.findByText("1.1 — Activo corriente")).toBeTruthy();
  expect(hintOf(1)).toBe("Cuenta agrupadora o inactiva: no se puede publicar con ella.");
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  expect(await screen.findByText("No se puede publicar: hay cuentas agrupadoras o inactivas.")).toBeTruthy();
  expect(mocks.editManualJournal).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
  await screen.findByText("detalle abierto");
  expect(mocks.editManualJournal).toHaveBeenCalledWith("draft-1", 4, expect.objectContaining({
    lines: expect.arrayContaining([expect.objectContaining({ accountId: GROUP.id })]),
  }));
});

it("publicación normal con cuentas imputables: sin avisos, confirma, guarda y publica", async () => {
  show();
  await fillBalancedDraft();
  expect(hintOf(1)).toBeNull();
  expect(hintOf(2)).toBeNull();
  fireEvent.click(screen.getByRole("button", { name: "Guardar y publicar" }));
  await screen.findByText("detalle abierto");
  expect(mocks.confirm).toHaveBeenCalledOnce();
  expect(mocks.createManualJournal).toHaveBeenCalledOnce();
  expect(mocks.postManualJournal).toHaveBeenCalledOnce();
});
