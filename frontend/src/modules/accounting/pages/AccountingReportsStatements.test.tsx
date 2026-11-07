// @vitest-environment jsdom
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { I18nProvider } from "../../../i18n/i18n";
import { AccountingReportsPage } from "./AccountingReportsPage";
import { useSessionStore } from "../../../store/sessionStore";
import { setPrecisionPolicyForTests } from "../../../lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../../test/precisionPolicyFixture";
import {
  BALANCE, BALANCE_CHART, BALANCE_UNBALANCED, INCOME, INCOME_LOSS, JOURNAL, TRIAL, TRIAL_UNBALANCED,
} from "./AccountingReportsPage.testFixtures";

/**
 * ACCOUNTING-REPORTS-PRO-15 — Balance de Comprobación (matriz + comprobación), Estado de Resultados
 * (escalonado Detalle | Total) y Balance General (clasificado, grupos del Plan de Cuentas sin importe,
 * comprobación de la ecuación). Datos coherentes en `AccountingReportsPage.testFixtures.ts`.
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
  mocks.trial.mockResolvedValue(TRIAL);
  mocks.income.mockResolvedValue(INCOME);
  mocks.balance.mockResolvedValue(BALANCE);
  mocks.accounts.mockResolvedValue(BALANCE_CHART);
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
  useSessionStore.setState({ tenant: null });
});

function show(tab: string) {
  render(
    <I18nProvider>
      <AccountingReportsPage />
    </I18nProvider>,
  );
  fireEvent.click(screen.getByRole("tab", { name: tab }));
}

const text = (el: Element | null | undefined) => (el?.textContent ?? "").replace(/\s+/g, " ").trim();
const cells = (row: Element | null | undefined) => Array.from(row?.querySelectorAll("td, th") ?? []).map(text);
const rowOf = (label: string) =>
  Array.from(document.querySelectorAll("tr")).find((r) => text(r.querySelector("td, th")) === label);

describe("Balance de Comprobación", () => {
  it("matriz agrupada por tipo, seis columnas, totales del backend y comprobación diferenciada", async () => {
    show("Balance de Comprobación");
    await screen.findByText("Proveedores");
    expect(Array.from(document.querySelectorAll("tbody th[scope='rowgroup']")).map(text)).toEqual([
      "Activo", "Pasivo", "Ingreso",
    ]);
    // Cuenta fija al desplazar en móvil (variante oficial del Design System).
    expect(document.querySelector("table")?.className).toContain("table--sticky-column");
    expect(rowOf("1.1.01 Caja")?.querySelector("td")?.className).toBe("table__sticky-cell");
    expect(cells(rowOf("1.1.01 Caja"))).toEqual(["1.1.01 Caja", "$100.00", "—", "$40.00", "$10.00", "$130.00", "—"]);

    const [totals, check] = Array.from(document.querySelectorAll("tfoot tr"));
    expect(cells(totals)).toEqual(["Totales", "$100.00", "$100.00", "$75.00", "$75.00", "$155.00", "$155.00"]);
    // Saldo inicial/final: comparación de totales; movimientos: isBalanced del backend.
    expect(cells(check)).toEqual([
      "Comprobación",
      "Totales iguales: Deudor = Acreedor",
      "Movimientos cuadrados: Debe = Haber",
      "Totales iguales: Deudor = Acreedor",
    ]);
    expect(screen.getByRole("button", { name: "Balance de Comprobación: qué es y cómo se lee" })).toBeTruthy();
  });

  it("marca el descuadre de movimientos (backend) y la diferencia de totales finales sin alterar cifras", async () => {
    mocks.trial.mockResolvedValue(TRIAL_UNBALANCED);
    show("Balance de Comprobación");
    expect(await screen.findByText(/Los movimientos del período no cuadran/)).toBeTruthy();
    const [totals, check] = Array.from(document.querySelectorAll("tfoot tr"));
    expect(cells(totals)).toEqual(["Totales", "$100.00", "$100.00", "$75.00", "$70.00", "$155.00", "$150.00"]);
    expect(cells(check)).toEqual([
      "Comprobación",
      "Totales iguales: Deudor = Acreedor",
      "Movimientos descuadrados: Debe ≠ Haber",
      "Totales distintos: Deudor ≠ Acreedor",
    ]);
  });

  it("envía la opción de cuentas sin movimiento solo al pulsar Consultar", async () => {
    show("Balance de Comprobación");
    await screen.findByText("Proveedores");
    fireEvent.click(screen.getByRole("switch"));
    expect(mocks.trial).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    await waitFor(() => expect(mocks.trial).toHaveBeenCalledTimes(2));
    expect(mocks.trial.mock.calls[1][0]).toMatchObject({ includeZeroMovementAccounts: true });
  });
});

describe("Estado de Resultados", () => {
  it("escalonado Ingresos → (−) Costos → (=) Utilidad bruta → (−) Gastos → (=) Utilidad neta, Detalle | Total", async () => {
    show("Estado de Resultados");
    await screen.findByText("Costo de ventas");
    expect(Array.from(document.querySelectorAll("thead th")).map(text)).toEqual(["Concepto", "Detalle", "Total"]);
    expect(cells(rowOf("4.1.01 Ventas"))).toEqual(["4.1.01 Ventas", "$500.00", ""]);
    expect(cells(rowOf("Total Ingresos"))).toEqual(["Total Ingresos", "", "$500.00"]);
    expect(cells(rowOf("(−) Costos"))).toEqual(["(−) Costos"]);
    expect(cells(rowOf("Total Costos"))).toEqual(["Total Costos", "", "$300.00"]);
    expect(cells(rowOf("(=) Utilidad bruta"))).toEqual(["(=) Utilidad bruta", "$200.00"]);
    expect(screen.getByText("Sin gastos contabilizados en el período.")).toBeTruthy();
    expect(cells(document.querySelector("tfoot tr"))).toEqual(["(=) Utilidad neta", "$200.00"]);
  });

  it("rotula como pérdida un resultado negativo sin alterar importes ni signos", async () => {
    mocks.income.mockResolvedValue(INCOME_LOSS);
    show("Estado de Resultados");
    await screen.findByText("Sueldos");
    expect(cells(rowOf("(=) Pérdida bruta"))).toEqual(["(=) Pérdida bruta", "$-100.00"]);
    expect(cells(rowOf("Total Gastos"))).toEqual(["Total Gastos", "", "$50.00"]);
    expect(cells(document.querySelector("tfoot tr"))).toEqual(["(=) Pérdida neta", "$-150.00"]);
    expect(screen.queryByText(/Utilidad neta/)).toBeNull();
  });
});

describe("Balance General", () => {
  it("clasificado con grupos del Plan de Cuentas sin importe, totales, Pasivo + Patrimonio y ecuación", async () => {
    show("Balance General");
    await screen.findByText("Capital");
    expect(document.querySelectorAll("table")).toHaveLength(1);
    expect(Array.from(document.querySelectorAll("tbody th[scope='colgroup']")).map(text)).toEqual([
      "Activos", "Pasivos", "Patrimonio",
    ]);
    // Encabezado de grupo: una sola celda, sin importe (nunca saldo propio de una agrupadora).
    expect(cells(rowOf("1.1 Activo corriente"))).toEqual(["1.1 Activo corriente"]);
    expect(cells(rowOf("1.2 Activo no corriente"))).toEqual(["1.2 Activo no corriente"]);
    expect(cells(rowOf("2.1 Pasivo corriente"))).toEqual(["2.1 Pasivo corriente"]);
    expect(rowOf("3 Patrimonio")).toBeUndefined(); // raíz de la sección: no se repite como grupo
    expect(cells(rowOf("1.1.01 Caja"))).toEqual(["1.1.01 Caja", "$700.00", ""]);
    expect(cells(rowOf("Total Activos"))).toEqual(["Total Activos", "", "$1000.00"]);
    expect(cells(rowOf("Total Patrimonio"))).toEqual(["Total Patrimonio", "", "$600.00"]);
    // Filas calculadas por el backend bajo su propio encabezado (no dentro del último grupo).
    expect(cells(rowOf("Resultados calculados por el sistema"))).toEqual(["Resultados calculados por el sistema"]);
    expect(cells(rowOf("Resultado del ejercicio"))).toEqual(["Resultado del ejercicio", "$100.00", ""]);
    expect(cells(rowOf("Resultados no cerrados de ejercicios anteriores"))).toEqual([
      "Resultados no cerrados de ejercicios anteriores", "$0.00", "",
    ]);

    const [sum, check] = Array.from(document.querySelectorAll("tfoot tr"));
    expect(cells(sum)).toEqual(["Total Pasivos + Patrimonio", "$1000.00"]);
    expect(cells(check)).toEqual(["Comprobación Activo = Pasivo + Patrimonio · Diferencia $0.00"]);
  });

  it("usa isBalanced/difference del backend para el descuadre", async () => {
    mocks.balance.mockResolvedValue(BALANCE_UNBALANCED);
    show("Balance General");
    expect(await screen.findByText("Activo no cuadra con Pasivo + Patrimonio.")).toBeTruthy();
    expect(cells(rowOf("Total Activos"))).toEqual(["Total Activos", "", "$1025.00"]);
    expect(cells(Array.from(document.querySelectorAll("tfoot tr"))[1])).toEqual([
      "Comprobación Activo ≠ Pasivo + Patrimonio · Diferencia $25.00",
    ]);
  });

  it("sin Plan de Cuentas disponible muestra las cuentas sin grupos inventados", async () => {
    mocks.accounts.mockRejectedValue(new Error("chart down"));
    show("Balance General");
    await screen.findByText("Capital");
    expect(rowOf("1.1 Activo corriente")).toBeUndefined();
    expect(cells(rowOf("1.1.01 Caja"))).toEqual(["1.1.01 Caja", "$700.00", ""]);
  });

  it("exige la fecha de corte antes de consultar", async () => {
    show("Balance General");
    await screen.findByText("Capital");
    fireEvent.change(document.querySelector<HTMLInputElement>(".zh-filterbar input[type='date']")!, { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: "Consultar" }));
    expect(screen.getByText("Indique la fecha de corte.")).toBeTruthy();
    expect(mocks.balance).toHaveBeenCalledTimes(1);
  });
});
