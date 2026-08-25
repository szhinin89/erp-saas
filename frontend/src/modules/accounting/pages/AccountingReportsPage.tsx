import { useState } from "react";
import { PageShell } from "../../../components/PageShell";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { GeneralJournalReportTab } from "./reports/GeneralJournalReportTab";
import { GeneralLedgerReportTab } from "./reports/GeneralLedgerReportTab";
import { TrialBalanceReportTab } from "./reports/TrialBalanceReportTab";
import { IncomeStatementReportTab } from "./reports/IncomeStatementReportTab";
import { BalanceSheetReportTab } from "./reports/BalanceSheetReportTab";
// ACCOUNTING-REPORTS-DS-QA-FIX-10E: sin este import, ZHTabBar (prd-tabs/prd-tab-btn) llega sin
// estilos cuando /accounting/reports es la primera pantalla del módulo que el usuario visita
// (ruta con code-splitting propio) — mismo CSS que ChartOfAccountsPage/BranchDetailPage ya
// cargan por su cuenta. Root cause real de "tabs con apariencia HTML nativa" detectado en QA.
import "../../../styles/shared/items-catalog.css";

type ReportTabId =
  | "general-journal"
  | "general-ledger"
  | "trial-balance"
  | "income-statement"
  | "balance-sheet";

const TABS: ZHTab<ReportTabId>[] = [
  { id: "general-journal", label: "Libro Diario" },
  { id: "general-ledger", label: "Libro Mayor" },
  { id: "trial-balance", label: "Balance de Comprobación" },
  { id: "income-statement", label: "Estado de Resultados" },
  { id: "balance-sheet", label: "Balance General" },
];

/**
 * Contabilidad → Reportes (ACCOUNTING-REPORTS-09, extendido en ACCOUNTING-FINANCIAL-
 * STATEMENTS-10 con Estado de Resultados/Balance General; ajustes visuales de
 * ACCOUNTING-REPORTS-DS-QA-FIX-10E). Auditoría de reutilización: revisadas `JournalEntriesPage.tsx`
 * (filtros + ZHDataTable paginado, mismo módulo), `BranchDetailPage.tsx` (uso de `ZHTabBar` para
 * pestañas de contenido dentro de una misma página, distinto de `ConfigTabsLayout` que resuelve
 * el patrón fijo Lista/Editor) y `ElectronicDocumentsFilters.tsx` (referencia real de
 * `ZHFilterBar`+`ZHField density="compact"` para una barra de filtros con 5-6 campos, en vez del
 * slot `actions` de `ZHCard` que solo alcanza para 2-3 campos simples como en `ChartOfAccountsPage`).
 * Reutiliza PageShell/ZHTabBar/ZHCard/ZHFilterBar/ZHField/ZHBtn/ZhSelect/ZhDateInput/ZHDataTable/
 * ZHMoneyValue/ZHPageNotice/Badge — sin componentes nuevos. Solo lectura: los cinco reportes leen
 * exclusivamente JournalEntry/JournalEntryLine ya Posted, sin recalcular nada de Ventas/Compras/
 * Inventario/Finanzas. ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: pantalla principal, accesible
 * solo desde el menú — sin botones cruzados hacia Asientos contables/Plan de cuentas.
 */
export function AccountingReportsPage() {
  const [tab, setTab] = useState<ReportTabId>("general-journal");

  return (
    <PageShell
      kicker="Contabilidad"
      title="Reportes contables"
      subtitle="Libro Diario, Libro Mayor, Balance de Comprobación, Estado de Resultados y Balance General — solo lectura, a partir de asientos ya contabilizados"
    >
      <ZHTabBar tabs={TABS} activeTab={tab} onChange={setTab} ariaLabel="Reportes contables" />
      {tab === "general-journal" && <GeneralJournalReportTab />}
      {tab === "general-ledger" && <GeneralLedgerReportTab />}
      {tab === "trial-balance" && <TrialBalanceReportTab />}
      {tab === "income-statement" && <IncomeStatementReportTab />}
      {tab === "balance-sheet" && <BalanceSheetReportTab />}
    </PageShell>
  );
}
