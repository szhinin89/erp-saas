// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { isValidElement, type ReactElement } from "react";
import { readdirSync, readFileSync } from "node:fs";
import { resolve } from "node:path";
import { I18nProvider } from "../../../i18n/i18n";
import { AccountingReportsPage } from "./AccountingReportsPage";
import { catalogRoutes } from "../../../routes";
import { useSessionStore } from "../../../store/sessionStore";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import {
  BALANCE, CHART, COMPLETE, INCOME, INCOMPLETE, JOURNAL, JOURNAL_UNBALANCED_PAGE, LEDGER_ACCOUNTS, TRIAL,
} from "./AccountingReportsPage.testFixtures";

/**
 * ACCOUNTING-REPORTS-PRO-15 — `/accounting/reports`: navegación, Libro Diario (diario de
 * operaciones), Libro Mayor (índice + mayor por cuenta) y ayudas «¿Qué es?»/«¿Cómo se lee?».
 * Los estados financieros se prueban en `AccountingReportsStatements.test.tsx`. Datos coherentes
 * en `AccountingReportsPage.testFixtures.ts`.
 */

const mocks = vi.hoisted(() => ({
  journal: vi.fn(), ledger: vi.fn(), trial: vi.fn(), income: vi.fn(), balance: vi.fn(), accounts: vi.fn(),
}));
vi.mock("../api/accountingApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("../api/accountingApi")>()),
  accountingApi: {
    getGeneralJournalReport: mocks.journal,
    getGeneralLedgerReport: mocks.ledger,
    getTrialBalanceReport: mocks.trial,
    getIncomeStatementReport: mocks.income,
    getBalanceSheetReport: mocks.balance,
    listAccounts: mocks.accounts,
  },
}));

beforeEach(() => {
  setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
  useSessionStore.setState({
    tenant: { id: "co-1", displayName: "Supermercado Demo", logo: null, timezone: "America/Guayaquil" },
  });
  mocks.journal.mockResolvedValue(JOURNAL);
  mocks.ledger.mockResolvedValue({ accounts: LEDGER_ACCOUNTS, completeness: COMPLETE });
  mocks.trial.mockResolvedValue(TRIAL);
  mocks.income.mockResolvedValue(INCOME);
  mocks.balance.mockResolvedValue(BALANCE);
  mocks.accounts.mockResolvedValue(CHART);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  vi.restoreAllMocks();
  useSessionStore.setState({ tenant: null });
});

function show(tab?: string) {
  render(
    <I18nProvider>
      <AccountingReportsPage />
    </I18nProvider>,
  );
  if (tab) fireEvent.click(screen.getByRole("tab", { name: tab }));
}

const search = () => fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
const dateInputs = () => document.querySelectorAll<HTMLInputElement>(".zh-filterbar input[type='date']");
const setDate = (input: HTMLInputElement, value: string) => fireEvent.change(input, { target: { value } });
const text = (el: Element | null | undefined) => (el?.textContent ?? "").replace(/\s+/g, " ").trim();
const cells = (row: Element | undefined) => Array.from(row?.querySelectorAll("td, th") ?? []).map(text);
const PAGE_TOTALS = "Totales de la página 1 de 3 · asientos N° 12 – 13";

describe("Reportes contables — página y navegación", () => {
  it("registra la ruta /accounting/reports y muestra las cinco pestañas con el Diario por defecto", async () => {
    const paths = (catalogRoutes as ReactElement<{ path?: string }>[])
      .filter(isValidElement)
      .map((r) => r.props.path);
    expect(paths).toContain("/accounting/reports");

    show();
    for (const name of ["Libro Diario", "Libro Mayor", "Balance de Comprobación", "Estado de Resultados", "Balance General"]) {
      expect(screen.getByRole("tab", { name })).toBeTruthy();
    }
    await screen.findByText(PAGE_TOTALS);
    expect(mocks.journal).toHaveBeenCalledTimes(1);
  });
});

describe("Libro Diario", () => {
  it("presenta cada asiento con encabezado, código, cuenta, Debe/Haber y total; pie de página identificado", async () => {
    const consoleError = vi.spyOn(console, "error");
    show();
    await screen.findByText(PAGE_TOTALS);

    expect(Array.from(document.querySelectorAll("thead th")).map(text)).toEqual(["Código", "Cuenta", "Debe", "Haber"]);
    const bodies = document.querySelectorAll("tbody");
    expect(bodies).toHaveLength(2); // un <tbody> por asiento
    const rows = bodies[0].querySelectorAll("tr");
    // Encabezado: N° · fecha · operación (etiqueta oficial traducida) + documento · descripción que aporta.
    expect(text(rows[0])).toBe("Asiento N° 12 · 02/11/2026 · Asiento de venta 001-001-000000123 · Venta de contado");
    expect(text(rows[0])).not.toContain("Factura"); // sourceDocumentType del backend: no traducible
    expect(rows).toHaveLength(5); // encabezado + 3 líneas + total del asiento
    expect(cells(rows[1])).toEqual(["1.1.01", "Caja", "$50.00", "—"]);
    expect(cells(rows[3])).toEqual(["4.1.01", "Ventas", "—", "$100.00"]);
    expect(cells(rows[4])).toEqual(["Total del asiento", "$100.00", "$100.00"]); // Decimal sobre líneas completas
    // Sin número de documento: módulo / operación traducidos, nunca ocultos.
    const purchase = bodies[1].querySelectorAll("tr");
    expect(text(purchase[0])).toContain("Asiento N° 13 · 02/11/2026 · Compras / Factura de compra recibida");
    expect(cells(purchase[3])).toEqual(["Total del asiento", "$30.00", "$30.00"]);

    // Totales de página de la API, con la página y el rango de asientos que suman.
    expect(cells(document.querySelector("tfoot tr") ?? undefined)).toEqual([PAGE_TOTALS, "$130.00", "$130.00"]);
    expect(screen.queryByText(/El total Debe no coincide/)).toBeNull();
    expect(text(document.querySelector(".card-header"))).toContain("Supermercado Demo · 01/11/2026");
    expect(text(document.querySelector(".card-header"))).toContain("Todos los orígenes");
    expect(consoleError.mock.calls.some((c) => String(c[0]).includes("same key"))).toBe(false);
  });

  it("avisa cuando los totales de la página informados por la API no cuadran", async () => {
    mocks.journal.mockResolvedValue(JOURNAL_UNBALANCED_PAGE);
    show();
    expect(await screen.findByText(/El total Debe no coincide/)).toBeTruthy();
    expect(cells(document.querySelector("tfoot tr") ?? undefined)).toEqual([
      "Totales de la página 1 de 1 · asientos N° 13 – 13", "$30.00", "$0.00",
    ]);
  });

  it("pagina por asientos y conserva los filtros aplicados al cambiar de página", async () => {
    show();
    await screen.findByText("Asientos: 120 · Página 1 de 3");
    fireEvent.click(screen.getByRole("button", { name: "Página siguiente" }));
    await waitFor(() => expect(mocks.journal).toHaveBeenCalledTimes(2));
    expect(mocks.journal.mock.calls[1][0]).toMatchObject({ pageNumber: 2, pageSize: 50 });
  });

  it("consulta solo al pulsar Consultar y valida el rango antes de consultar", async () => {
    show();
    await screen.findByText(PAGE_TOTALS);
    const [from, to] = dateInputs();
    setDate(from, "2026-10-01");
    setDate(to, "2026-10-31");
    expect(mocks.journal).toHaveBeenCalledTimes(1);
    search();
    await waitFor(() => expect(mocks.journal).toHaveBeenCalledTimes(2));
    expect(mocks.journal.mock.calls[1][0]).toMatchObject({ fromDate: "2026-10-01", toDate: "2026-10-31", pageNumber: 1 });

    setDate(from, "2026-11-30");
    search();
    expect(screen.getByText("La fecha Hasta debe ser igual o posterior a la fecha Desde.")).toBeTruthy();
    expect(mocks.journal).toHaveBeenCalledTimes(2);
  });

  it("ante un error no muestra los datos de la consulta anterior", async () => {
    show();
    await screen.findByText(PAGE_TOTALS);
    mocks.journal.mockRejectedValueOnce(new Error("boom"));
    search();
    await screen.findByText(/No se pudo cargar el Libro Diario/);
    expect(screen.queryByText(/Totales de la página/)).toBeNull();
  });

  it("muestra el estado vacío y conserva el aviso de completitud", async () => {
    mocks.journal.mockResolvedValue({ ...JOURNAL, lines: [], totalDebit: 0, totalCredit: 0, totalCount: 0, completeness: INCOMPLETE });
    show();
    await screen.findByText(/No hay asientos contabilizados/);
    expect(screen.getByText(/Reporte provisional/)).toBeTruthy();
  });
});

describe("Libro Mayor", () => {
  it("el índice lista las cuentas con saldo o movimientos e informa las omitidas", async () => {
    show("Libro Mayor");
    await screen.findByText("Bancos");
    expect(screen.getByText("Caja")).toBeTruthy(); // solo saldo inicial: se conserva
    expect(screen.queryByText("Caja chica")).toBeNull();
    const omitted = screen.getByText(/2 cuentas sin saldo ni movimientos no se muestran, incluidas 1 agrupadoras/);
    expect(text(omitted)).toContain("Seleccione una cuenta para ver su Libro Mayor completo.");
    // Sin drawer: el detalle no se abre hasta elegir una cuenta.
    expect(screen.queryByRole("dialog")).toBeNull();
    const options = Array.from(document.querySelectorAll("select option")).map((o) => o.textContent);
    expect(options).toContain("1.1.02 — Bancos");
    expect(options).not.toContain("1 — Activo"); // solo cuentas imputables
  });

  it("abre el mayor completo en la página: saldo inicial, movimientos con saldo acumulado, totales y saldo final", async () => {
    show("Libro Mayor");
    fireEvent.click(await screen.findByText("Bancos"));
    expect(screen.queryByRole("dialog")).toBeNull(); // dentro de la página, sin ZHDrawer
    expect(Array.from(document.querySelectorAll("thead th")).map(text)).toEqual([
      "Fecha · Asiento / Referencia", "Debe", "Haber", "Saldo",
    ]);
    expect(document.querySelector("table")?.className).toContain("table--sticky-column");
    const rows = Array.from(document.querySelectorAll("tbody tr"));
    expect(text(rows[0])).toBe("1.1.02 Bancos · Activo · Naturaleza: Débito");
    expect(cells(rows[1])).toEqual(["Saldo inicial al 01/11/2026", "$40.00"]);
    // Movimiento apilado (fecha · asiento / referencia / descripción) en la columna fija.
    expect(rows[2].querySelector("td")?.className).toBe("table__sticky-cell");
    expect(cells(rows[2])).toEqual(["03/11/2026 · Asiento N° 9DEP-1Depósito", "$80.00", "—", "$120.00"]);
    expect(cells(rows[3])).toEqual(["03/11/2026 · Asiento N° 9DEP-1Depósito", "—", "$20.00", "$100.00"]);
    expect(cells(rows[4])).toEqual(["Movimientos del período", "$80.00", "$20.00", ""]);
    expect(cells(document.querySelector("tfoot tr") ?? undefined)).toEqual([expect.stringMatching(/^Saldo final al /), "$100.00"]);
    expect(text(document.querySelector(".card-header"))).toContain("1.1.02 — Bancos");

    // Regreso al índice.
    fireEvent.click(screen.getByRole("button", { name: "Índice de cuentas" }));
    expect(screen.queryByText(/Movimientos del período/)).toBeNull();
    expect(screen.getByText("Caja")).toBeTruthy();
  });

  it("una cuenta con solo saldo inicial muestra que su saldo final es igual al inicial", async () => {
    show("Libro Mayor");
    fireEvent.click(await screen.findByText("Caja"));
    expect(await screen.findByText(/Sin movimientos en el período/)).toBeTruthy();
    expect(cells(document.querySelector("tfoot tr") ?? undefined)[1]).toBe("$250.00");
  });

  it("una cuenta elegida en el filtro muestra su mayor directamente, aunque no tenga saldo", async () => {
    show("Libro Mayor");
    await screen.findByText("Bancos");
    mocks.ledger.mockResolvedValueOnce({ accounts: [LEDGER_ACCOUNTS[3]], completeness: COMPLETE });
    fireEvent.change(document.querySelector("select") as HTMLSelectElement, { target: { value: "a-idle" } });
    search();
    expect(await screen.findByText(/Sin movimientos en el período/)).toBeTruthy();
    expect(text(document.querySelector("tbody tr"))).toContain("1.1.09 Caja chica");
    expect(screen.queryByRole("button", { name: "Índice de cuentas" })).toBeNull();
    expect(mocks.ledger.mock.calls.at(-1)?.[0]).toMatchObject({ accountId: "a-idle" });
  });
});

describe("Ayuda contextual", () => {
  const REPORTS: [string, string][] = [
    ["Libro Diario", "generalJournal"],
    ["Libro Mayor", "generalLedger"],
    ["Balance de Comprobación", "trialBalance"],
    ["Estado de Resultados", "incomeStatement"],
    ["Balance General", "balanceSheet"],
  ];

  it.each(REPORTS)("%s muestra «¿Qué es?» y «¿Cómo se lee?» con el sistema oficial de ayuda", async (tab) => {
    show(tab === "Libro Diario" ? undefined : tab);
    const name = `${tab}: qué es y cómo se lee`;
    fireEvent.click(await screen.findByRole("button", { name }));
    const dialog = screen.getByRole("dialog", { name });
    expect(dialog.querySelector(".zh-help-popover__short")?.textContent).toMatch(/^¿Qué es\? /);
    expect(dialog.querySelector(".zh-help-popover__long")?.textContent).toMatch(/^¿Cómo se lee\? /);
    fireEvent.keyDown(window, { key: "Escape" });
    expect(screen.queryByRole("dialog", { name })).toBeNull();
  });

  it.each(REPORTS)("%s tiene la ayuda completa en español e inglés", (_tab, key) => {
    const load = (lang: string) =>
      JSON.parse(readFileSync(resolve(__dirname, `../../../i18n/locales/${lang}.json`), "utf-8")) as Record<string, string>;
    const es = load("es");
    const en = load("en");
    const prefix = `help.accountingReports.${key}`;
    expect(es[`${prefix}.short`]).toMatch(/^¿Qué es\? /);
    expect(es[`${prefix}.long`]).toMatch(/^¿Cómo se lee\? /);
    expect(en[`${prefix}.short`]).toMatch(/^What is it\? /);
    expect(en[`${prefix}.long`]).toMatch(/^How to read it\? /);
    expect(es[`${prefix}.title`]).toBeTruthy();
    expect(en[`${prefix}.title`]).toBeTruthy();
  });

  it("las pestañas importan la ayuda solo desde el punto de entrada oficial (carga zh-help.css)", () => {
    const dir = resolve(__dirname, "reports");
    const offenders = readdirSync(dir)
      .filter((f) => f.endsWith(".tsx"))
      .filter((f) => /components\/zh\/help\/[A-Za-z]/.test(readFileSync(resolve(dir, f), "utf-8")));
    expect(offenders).toEqual([]);
  });
});
