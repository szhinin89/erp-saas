// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { dictionaries } from "../../../i18n/dictionaries";
import { PostingRulesPage } from "./PostingRulesPage";
import type { PostingFactCatalogItemDto, PostingRuleDto, PostingRuleLineDto } from "../api/accountingApi";

/**
 * ACCOUNTING-POSTING-RULES-UX-01 — Lista → Detalle de Reglas contables: tipos Fija/Mixta/
 * Habilitadora, estados Activa/Inactiva/Sin regla/No soportada, filtros, permisos, acciones con su
 * confirmación y asiento con líneas fijas vs. aportadas por el origen.
 */

const es = dictionaries.es as Record<string, string>;
const tx = (key: string) => {
  const value = es[key];
  if (!value) throw new Error(`missing es key ${key}`);
  return value;
};

const mocks = vi.hoisted(() => ({
  listPostingRules: vi.fn(),
  getPostingFactCatalog: vi.fn(),
  listAccounts: vi.fn(),
  enablePostingRule: vi.fn(),
  disablePostingRule: vi.fn(),
  createPostingRule: vi.fn(),
  updatePostingRule: vi.fn(),
  confirm: vi.fn(),
  success: vi.fn(),
  error: vi.fn(),
  denied: new Set<string>(),
}));
vi.mock("../api/accountingApi", () => ({
  accountingApi: {
    listPostingRules: mocks.listPostingRules,
    getPostingFactCatalog: mocks.getPostingFactCatalog,
    listAccounts: mocks.listAccounts,
    enablePostingRule: mocks.enablePostingRule,
    disablePostingRule: mocks.disablePostingRule,
    createPostingRule: mocks.createPostingRule,
    updatePostingRule: mocks.updatePostingRule,
  },
}));
vi.mock("../../../lib/messages", () => ({
  message: { error: mocks.error, success: mocks.success, warning: vi.fn(), confirm: mocks.confirm },
}));
vi.mock("../../../access/usePermissionsUi", () => ({
  usePermissionsUi: () => {
    const canShow = (p: string) => !mocks.denied.has(p);
    return { canShow, has: canShow, isAdminRole: false };
  },
}));

const line = (over: Partial<PostingRuleLineDto>): PostingRuleLineDto => ({
  id: "l",
  accountId: "a",
  accountCode: "1.1",
  accountName: "Cuenta",
  accountType: "Asset",
  accountNature: "Debit",
  accountIsActive: true,
  accountAllowsPosting: true,
  nature: "Debit",
  amountKind: "GrandTotal",
  sortOrder: 1,
  ...over,
});

const fact = (over: Partial<PostingFactCatalogItemDto>): PostingFactCatalogItemDto => ({
  sourceModule: "Sales",
  factType: "InvoiceIssued",
  allowedAmountKinds: ["GrandTotal"],
  dynamicDebit: false,
  dynamicCredit: false,
  ruleId: null,
  ruleIsActive: null,
  ...over,
});

const rule = (over: Partial<PostingRuleDto>): PostingRuleDto => ({
  id: "r",
  sourceModule: "Sales",
  factType: "InvoiceIssued",
  isActive: true,
  isSupported: true,
  lines: [],
  createdAt: "2026-10-01T15:00:00Z",
  updatedAt: null,
  ...over,
});

const salesRule = rule({
  id: "r-sales",
  lines: [
    line({ id: "s1", accountCode: "1.1.01", accountName: "Caja general", nature: "Debit", amountKind: "CashApplied" }),
    line({
      id: "s2",
      accountCode: "4.1.01",
      accountName: "Ventas",
      accountType: "Income",
      accountNature: "Credit",
      nature: "Credit",
      amountKind: "Subtotal",
      sortOrder: 2,
    }),
  ],
});
const paymentRule = rule({
  id: "r-pay",
  sourceModule: "Payables",
  factType: "SupplierPaymentConfirmed",
  isActive: false,
  lines: [
    line({
      id: "p1",
      accountCode: "2.1.01",
      accountName: "Proveedores",
      accountType: "Liability",
      accountNature: "Credit",
      accountIsActive: false,
      nature: "Debit",
      amountKind: "AppliedToPayable",
    }),
  ],
});
const depositRule = rule({ id: "r-dep", sourceModule: "Caja", factType: "BankDeposit" });
const legacyRule = rule({
  id: "r-old",
  sourceModule: "Purchases",
  factType: "InvoiceCancelled",
  isSupported: false,
  lines: [line({ id: "o1" }), line({ id: "o2", nature: "Credit", sortOrder: 2 })],
});

const catalog = [
  fact({ allowedAmountKinds: ["CashApplied", "Subtotal"], ruleId: "r-sales", ruleIsActive: true }),
  fact({
    sourceModule: "Payables",
    factType: "SupplierPaymentConfirmed",
    allowedAmountKinds: ["AppliedToPayable", "SupplierCredit"],
    dynamicCredit: true,
    ruleId: "r-pay",
    ruleIsActive: false,
  }),
  fact({
    sourceModule: "Caja",
    factType: "BankDeposit",
    allowedAmountKinds: [],
    dynamicDebit: true,
    dynamicCredit: true,
    ruleId: "r-dep",
    ruleIsActive: true,
  }),
  fact({ sourceModule: "Caja", factType: "CashCountShortage", dynamicCredit: true }),
];

function renderPage() {
  return render(
    <I18nProvider>
      <MemoryRouter>
        <PostingRulesPage />
      </MemoryRouter>
    </I18nProvider>,
  );
}

/** El nombre de la operación también aparece en las opciones del filtro: buscar solo en la tabla. */
const cellOf = (text: string) => {
  const el = screen.getAllByText(text).find((e) => e.closest("tbody"));
  if (!el) throw new Error(`no table cell with text ${text}`);
  return el;
};
const rowOf = (operation: string) => cellOf(operation).closest("tr") as HTMLElement;
const waitForCell = (text: string) => waitFor(() => cellOf(text));

async function openDetail(operation: string) {
  renderPage();
  fireEvent.click(await waitForCell(operation));
  return screen.getByRole("table");
}

beforeEach(() => {
  mocks.listPostingRules.mockResolvedValue([salesRule, paymentRule, depositRule, legacyRule]);
  mocks.getPostingFactCatalog.mockResolvedValue(catalog);
  mocks.listAccounts.mockResolvedValue([]);
  mocks.enablePostingRule.mockResolvedValue({});
  mocks.disablePostingRule.mockResolvedValue({});
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  mocks.denied.clear();
});

describe("PostingRulesPage — Lista", () => {
  it("una fila por hecho del catálogo más la regla no soportada, con tipo, estado y diagnóstico", async () => {
    renderPage();
    const sales = await waitForCell(tx("accounting.labels.factType.InvoiceIssued"));
    expect(screen.getAllByRole("row")).toHaveLength(1 + 5);

    expect(within(sales.closest("tr") as HTMLElement).getByText(tx("accounting.postingRules.ruleType.fixed"))).toBeTruthy();
    expect(within(sales.closest("tr") as HTMLElement).getByText(tx("accounting.postingRules.status.active"))).toBeTruthy();

    const payment = rowOf(tx("accounting.labels.factType.SupplierPaymentConfirmed"));
    expect(within(payment).getByText(tx("accounting.postingRules.ruleType.mixed"))).toBeTruthy();
    expect(within(payment).getByText(tx("accounting.postingRules.status.inactive"))).toBeTruthy();
    expect(within(payment).getByText("Problemas: 1")).toBeTruthy();

    const deposit = rowOf(tx("accounting.labels.factType.BankDeposit"));
    expect(within(deposit).getByText(tx("accounting.postingRules.ruleType.enabling"))).toBeTruthy();
    expect(within(deposit).queryByText(/Problemas:/)).toBeNull();

    const shortage = rowOf(tx("accounting.labels.factType.CashCountShortage"));
    expect(within(shortage).getByText(tx("accounting.postingRules.status.missing"))).toBeTruthy();
    expect(within(shortage).getByText(tx("accounting.labels.sourceModule.Caja"))).toBeTruthy();

    const legacy = rowOf("InvoiceCancelled");
    expect(within(legacy).getByText(tx("accounting.postingRules.status.unsupported"))).toBeTruthy();
    expect(within(legacy).getByText("—")).toBeTruthy();
  });

  it("filtra por estado y por módulo, y Limpiar restablece la tabla", async () => {
    renderPage();
    await waitForCell(tx("accounting.labels.factType.InvoiceIssued"));
    const [moduleSelect, , statusSelect] = screen.getAllByRole("combobox");

    fireEvent.change(statusSelect, { target: { value: "missing" } });
    expect(screen.getAllByRole("row")).toHaveLength(2);
    expect(cellOf(tx("accounting.labels.factType.CashCountShortage"))).toBeTruthy();

    fireEvent.change(statusSelect, { target: { value: "unsupported" } });
    expect(cellOf("InvoiceCancelled")).toBeTruthy();

    fireEvent.change(statusSelect, { target: { value: "" } });
    fireEvent.change(moduleSelect, { target: { value: "Caja" } });
    expect(screen.getAllByRole("row")).toHaveLength(3);

    fireEvent.change(statusSelect, { target: { value: "inactive" } });
    expect(screen.getByText(tx("accounting.postingRules.empty.noFilterResults"))).toBeTruthy();

    const clear = document.querySelector<HTMLButtonElement>(".zh-filterbar__clear");
    fireEvent.click(clear as HTMLButtonElement);
    expect(screen.getAllByRole("row")).toHaveLength(1 + 5);
  });

  it("muestra el error de carga y el estado vacío", async () => {
    mocks.listPostingRules.mockRejectedValueOnce(new Error("boom"));
    renderPage();
    expect(await screen.findByText(tx("accounting.postingRules.error.load"))).toBeTruthy();
    cleanup();

    mocks.listPostingRules.mockResolvedValue([]);
    mocks.getPostingFactCatalog.mockResolvedValue([]);
    renderPage();
    expect(await screen.findByText(tx("accounting.postingRules.empty.noRules"))).toBeTruthy();
  });

  it("Configurar en un hecho sin regla abre el editor de creación sin cambiar de pestaña", async () => {
    renderPage();
    await waitForCell(tx("accounting.labels.factType.CashCountShortage"));
    const shortage = rowOf(tx("accounting.labels.factType.CashCountShortage"));
    fireEvent.click(within(shortage).getByRole("button", { name: tx("accounting.postingRules.action.configure") }));
    expect(await screen.findByText(tx("accounting.postingRules.editor.createSubtitle"))).toBeTruthy();
    expect(screen.getByRole("tab", { name: /Lista/ }).getAttribute("aria-selected")).toBe("true");
  });
});

describe("PostingRulesPage — Detalle", () => {
  it("sin selección muestra el estado vacío del detalle", async () => {
    renderPage();
    await waitForCell(tx("accounting.labels.factType.InvoiceIssued"));
    fireEvent.click(screen.getByRole("tab", { name: new RegExp(tx("accounting.postingRules.tab.detail")) }));
    expect(screen.getByText(tx("accounting.postingRules.detail.emptyTitle"))).toBeTruthy();
  });

  it("regla fija: cuentas fijas en Debe y Haber, sin filas del origen; volver a la lista", async () => {
    const table = await openDetail(tx("accounting.labels.factType.InvoiceIssued"));
    expect(screen.getByRole("tab", { name: new RegExp(tx("accounting.postingRules.tab.detail")) }).getAttribute("aria-selected")).toBe("true");
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText(tx("accounting.labels.lineDirection.Debit"))).toBeTruthy();
    expect(within(rows[0]).getByText("1.1.01")).toBeTruthy();
    expect(within(rows[0]).getByText(tx("accounting.labels.amountKind.CashApplied"))).toBeTruthy();
    expect(within(rows[1]).getByText(tx("accounting.labels.lineDirection.Credit"))).toBeTruthy();
    expect(within(table).getAllByText(tx("accounting.postingRules.entry.origin.fixed"))).toHaveLength(2);
    expect(within(table).queryByText(tx("accounting.postingRules.entry.origin.source"))).toBeNull();

    fireEvent.click(screen.getByRole("button", { name: tx("accounting.postingRules.detail.back") }));
    expect(screen.getByRole("tab", { name: /Lista/ }).getAttribute("aria-selected")).toBe("true");
    expect(rowOf(tx("accounting.labels.factType.InvoiceIssued")).className).toContain("cfg-row--selected");
  });

  it("regla habilitadora: ambos lados los aporta el origen, sin advertencias ni Editar líneas", async () => {
    const table = await openDetail(tx("accounting.labels.factType.BankDeposit"));
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText(tx("accounting.postingRules.source.bankAccount"))).toBeTruthy();
    expect(within(rows[1]).getByText(tx("accounting.postingRules.source.cashRegister"))).toBeTruthy();
    expect(within(table).getAllByText(tx("accounting.postingRules.entry.origin.source"))).toHaveLength(2);
    expect(within(table).queryByText(tx("accounting.postingRules.entry.origin.fixed"))).toBeNull();
    expect(screen.queryByText(tx("accounting.postingRules.problem.message"))).toBeNull();
    expect(screen.queryByRole("button", { name: tx("accounting.postingRules.action.edit") })).toBeNull();
    expect(screen.getByText(tx("accounting.postingRules.detail.noAmountKinds"))).toBeTruthy();
  });

  it("regla mixta inactiva: línea fija con problema de cuenta + Haber del origen; Activar sin confirmación", async () => {
    const table = await openDetail(tx("accounting.labels.factType.SupplierPaymentConfirmed"));
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(2);
    expect(within(rows[0]).getByText("2.1.01")).toBeTruthy();
    expect(within(rows[0]).getByText(tx("accounting.postingRules.entry.issue.inactive"))).toBeTruthy();
    expect(within(rows[1]).getByText(tx("accounting.postingRules.source.paymentMethods"))).toBeTruthy();
    expect(within(rows[1]).getByText(tx("accounting.postingRules.entry.sourceAmount"))).toBeTruthy();
    expect(screen.getByText(tx("accounting.postingRules.problem.message"))).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: tx("accounting.postingRules.action.enable") }));
    await waitFor(() => expect(mocks.enablePostingRule).toHaveBeenCalledWith("r-pay"));
    expect(mocks.confirm).not.toHaveBeenCalled();
    expect(mocks.success).toHaveBeenCalledWith(tx("accounting.postingRules.enable.done"));
  });

  it("Desactivar pide confirmación y solo llama a la API si se confirma", async () => {
    await openDetail(tx("accounting.labels.factType.InvoiceIssued"));
    const disable = screen.getByRole("button", { name: tx("accounting.postingRules.action.disable") });

    mocks.confirm.mockResolvedValueOnce(false);
    fireEvent.click(disable);
    await waitFor(() => expect(mocks.confirm).toHaveBeenCalledTimes(1));
    expect(mocks.disablePostingRule).not.toHaveBeenCalled();

    mocks.confirm.mockResolvedValueOnce(true);
    fireEvent.click(disable);
    await waitFor(() => expect(mocks.disablePostingRule).toHaveBeenCalledWith("r-sales"));
    expect(mocks.confirm).toHaveBeenLastCalledWith(expect.objectContaining({ variant: "danger" }));
  });

  it("Editar líneas abre el editor existente con las líneas de la regla", async () => {
    await openDetail(tx("accounting.labels.factType.InvoiceIssued"));
    fireEvent.click(screen.getByRole("button", { name: tx("accounting.postingRules.action.edit") }));
    expect(await screen.findByText(tx("accounting.postingRules.editor.editSubtitle"))).toBeTruthy();
  });

  it("sin regla: aviso, Haber del origen y Configurar; no soportada: solo Desactivar", async () => {
    const table = await openDetail(tx("accounting.labels.factType.CashCountShortage"));
    expect(screen.getByText(tx("accounting.postingRules.missing.message"))).toBeTruthy();
    expect(within(table).getByText(tx("accounting.postingRules.source.cashRegister"))).toBeTruthy();
    expect(screen.getByRole("button", { name: tx("accounting.postingRules.action.configure") })).toBeTruthy();
    expect(screen.queryByRole("button", { name: tx("accounting.postingRules.action.disable") })).toBeNull();
    cleanup();

    await openDetail("InvoiceCancelled");
    expect(screen.getByText(tx("accounting.postingRules.problem.unsupported"), { exact: false })).toBeTruthy();
    expect(screen.queryByRole("button", { name: tx("accounting.postingRules.action.edit") })).toBeNull();
    expect(screen.getByRole("button", { name: tx("accounting.postingRules.action.disable") })).toBeTruthy();
  });

  it("sin accounting.rules.manage: solo consulta, sin acciones de administración", async () => {
    mocks.denied.add("accounting.rules.manage");
    renderPage();
    await waitForCell(tx("accounting.labels.factType.CashCountShortage"));
    const shortage = rowOf(tx("accounting.labels.factType.CashCountShortage"));
    expect(within(shortage).getByRole("button", { name: tx("accounting.postingRules.action.view") })).toBeTruthy();
    expect(screen.queryByRole("button", { name: tx("accounting.postingRules.action.configure") })).toBeNull();

    fireEvent.click(cellOf(tx("accounting.labels.factType.InvoiceIssued")));
    for (const key of ["edit", "disable", "enable", "configure"]) {
      expect(screen.queryByRole("button", { name: tx(`accounting.postingRules.action.${key}`) })).toBeNull();
    }
  });
});
