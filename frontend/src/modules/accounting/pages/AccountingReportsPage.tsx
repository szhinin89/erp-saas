import { useState } from "react";
import { useI18n } from "../../../i18n/i18n";
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

const TAB_LABEL_KEYS: Record<ReportTabId, string> = {
  "general-journal": "accounting.reports.tab.generalJournal",
  "general-ledger": "accounting.reports.tab.generalLedger",
  "trial-balance": "accounting.reports.tab.trialBalance",
  "income-statement": "accounting.reports.tab.incomeStatement",
  "balance-sheet": "accounting.reports.tab.balanceSheet",
};

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
 * ACCOUNTING-REPORTS-UX-13: textos vía i18n; cada pestaña consulta solo al pulsar Buscar, con
 * alcance aplicado visible (empresa, período/fecha de corte) y tablas compactas.
 */
export function AccountingReportsPage() {
  const { t } = useI18n();
  const [tab, setTab] = useState<ReportTabId>("general-journal");
  const tabs: ZHTab<ReportTabId>[] = (Object.keys(TAB_LABEL_KEYS) as ReportTabId[]).map((id) => ({
    id,
    label: t(TAB_LABEL_KEYS[id]),
  }));

  return (
    <PageShell
      kicker={t("accounting.reports.page.kicker")}
      title={t("accounting.reports.page.title")}
      subtitle={t("accounting.reports.page.subtitle")}
    >
      <ZHTabBar tabs={tabs} activeTab={tab} onChange={setTab} ariaLabel={t("accounting.reports.page.title")} />
      {tab === "general-journal" && <GeneralJournalReportTab />}
      {tab === "general-ledger" && <GeneralLedgerReportTab />}
      {tab === "trial-balance" && <TrialBalanceReportTab />}
      {tab === "income-statement" && <IncomeStatementReportTab />}
      {tab === "balance-sheet" && <BalanceSheetReportTab />}
    </PageShell>
  );
}
