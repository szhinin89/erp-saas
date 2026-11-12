import { useState } from "react";
import { createRoot, type Root } from "react-dom/client";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../src/i18n/i18n";
import { setPrecisionPolicyForTests } from "../../src/lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../src/test/precisionPolicyFixture";
import "../../src/index.css";
import { PaymentScheduleSection } from "../../src/modules/purchases/pages/PurchasesPage";

/**
 * Fixture del cronograma de pagos de Compras (UIUX Paquete D): monta el `PaymentScheduleSection` REAL
 * con el CSS real; solo el estado del contexto es simulado. `?persisted=1` renderiza la tabla de solo lectura.
 */
setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
type Row = { number: number; dueDate: string; amount: number; notes: string };
const W = window as unknown as { __scheduleLog: string[] };
W.__scheduleLog = [];
const persisted = new URLSearchParams(location.search).get("persisted") === "1";
const TOTAL = 1001234.56;
const INITIAL: Row[] = [
  { number: 1, dueDate: "2026-12-12", amount: 333.34, notes: "" },
  { number: 2, dueDate: "2027-01-11", amount: 333.33, notes: "Segunda cuota con una observación bastante larga para probar el desbordamiento" },
  { number: 3, dueDate: "2027-02-10", amount: 1234567.89, notes: "" },
];

function Fixture() {
  const [rows, setRows] = useState<Row[]>(INITIAL);
  const sum = rows.reduce((a, r) => a + r.amount, 0);
  const ctx = {
    hasPersistedSchedule: persisted, ptLoaded: true, isDraft: !persisted, saving: false, fieldDisabled: false,
    formWatch: { issueDate: "2026-11-12" }, xmlPaymentProposal: null, scheduleConditionError: null,
    ptInstallments: rows.length, ptDaysBetween: 30, setPtInstallments: () => {}, setPtDaysBetween: () => {},
    ptMismatch: Math.abs(sum - TOTAL) > 0.005, ptRows: rows, ptRowsSum: sum, localTotal: TOTAL,
    editing: { paymentSchedules: INITIAL.map((r, i) => ({ id: String(i), installmentNumber: r.number, dueDate: r.dueDate, amount: r.amount, notes: r.notes })) },
    regenerateSchedule: () => W.__scheduleLog.push("regenerate"),
    addInstallment: () => W.__scheduleLog.push("add"),
    removeInstallment: (i: number) => { W.__scheduleLog.push(`remove:${i}`); setRows((r) => r.filter((_, k) => k !== i)); },
    updateScheduleRow: (i: number, f: string, v: unknown) => {
      W.__scheduleLog.push(`update:${i}:${f}:${v}`);
      setRows((r) => r.map((x, k) => (k === i ? { ...x, [f]: v } : x)));
    },
  } as unknown as Parameters<typeof PaymentScheduleSection>[0]["ctx"];
  return <MemoryRouter><I18nProvider><PaymentScheduleSection ctx={ctx} /></I18nProvider></MemoryRouter>;
}
// Vite puede reevaluar el fixture con Fast Refresh: conservar su raíz de React.
const mount = document.getElementById("root")! as HTMLElement & { scheduleRoot?: Root };
mount.scheduleRoot ??= createRoot(mount);
mount.scheduleRoot.render(<Fixture />);
