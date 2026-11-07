import { useMemo, useState } from "react";
import Decimal from "decimal.js";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ReportScopeTitle } from "./reportScope";
import { AccountLabel, FinancialStatementSection, StatementHead, type StatementRow } from "./FinancialStatementSection";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { Badge, ErrorState, LoadingState } from "../../../../components/PageShell";
import { useCompanyScopedAsync } from "../../../../hooks/useCompanyScopedAsync";
import { HELP_KEYS } from "../../../../help";
import { formatDate, todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";
import { accountingApi, type AccountDto, type FinancialStatementLineDto } from "../../api/accountingApi";

/**
 * Cuentas de una sección precedidas por el encabezado de su grupo (cuenta padre del Plan de
 * Cuentas) cada vez que el grupo cambia. El encabezado no lleva importe (D1: sin subtotales por
 * grupo; nunca el saldo propio de una agrupadora). Se omite cuando el padre es la raíz de la
 * sección (sin padre propio: repetiría la banda Activo/Pasivo/Patrimonio) o si el Plan de
 * Cuentas no está disponible — las cuentas se muestran igual, sin clasificación inventada.
 */
function groupedRows(lines: FinancialStatementLineDto[], chart: Map<string, AccountDto>): StatementRow[] {
  const rows: StatementRow[] = [];
  let currentParent: string | null = null;
  for (const l of lines) {
    const parentId = chart.get(l.accountId)?.parentAccountId ?? null;
    const parent = parentId ? chart.get(parentId) : undefined;
    const groupId = parent && parent.parentAccountId !== null ? parent.id : null;
    if (groupId && groupId !== currentParent && parent) {
      rows.push({ kind: "group", id: `group-${groupId}`, label: <AccountLabel code={parent.code} name={parent.name} /> });
    }
    currentParent = groupId;
    rows.push({ kind: "account", id: l.accountId, account: <AccountLabel code={l.accountCode} name={l.accountName} />, amount: l.amount });
  }
  return rows;
}

/**
 * Balance General (ACCOUNTING-FINANCIAL-STATEMENTS-10) — `GET /accounting/reports/balance-sheet`,
 * solo lectura, saldo acumulado hasta la fecha de corte. ACCOUNTING-11: Patrimonio incluye las dos
 * filas calculadas por el backend (marcadas "Calculado"). ACCOUNTING-REPORTS-PRO-15: estado de
 * situación clasificado en dos columnas (Detalle | Total): Activo, Pasivo y Patrimonio con los
 * grupos disponibles del Plan de Cuentas (encabezados sin importe), Total Pasivo + Patrimonio
 * (`grand`) y la comprobación de la ecuación contable dentro del reporte con `isBalanced`/
 * `difference` del backend como autoridad. Total Pasivo + Patrimonio es la suma de presentación de
 * dos totales del backend (Decimal.js), el único importe que el backend no entrega.
 */
export function BalanceSheetReportTab() {
  const { t } = useI18n();
  const [draft, setDraft] = useState(() => todayIso());
  const [applied, setApplied] = useState(draft);
  const [showError, setShowError] = useState(false);
  const asOfError = draft ? null : t("accounting.reports.filter.asOfRequired");

  const chartState = useCompanyScopedAsync(() => accountingApi.listAccounts());
  const chart = useMemo(() => new Map((chartState.data ?? []).map((a) => [a.id, a])), [chartState.data]);

  const { data, loading, error } = useCompanyScopedAsync(
    () => accountingApi.getBalanceSheetReport({ asOfDate: applied }).then((report) => ({ asOf: applied, report })),
    true,
    [applied],
  );
  const report = data && data.asOf === applied && !error ? data.report : null;

  const apply = () => {
    setShowError(true);
    if (asOfError) return;
    setApplied(draft);
  };

  const calculated = (id: string, nameKey: string, amount: number): StatementRow => ({
    kind: "account",
    id,
    account: t(nameKey),
    amount,
  });
  const equityRows: StatementRow[] = report
    ? [
        ...groupedRows(report.equityLines, chart),
        // Filas calculadas por el backend (no son cuentas del Plan): encabezado propio para que no
        // aparezcan dentro del último grupo de Patrimonio.
        { kind: "group", id: "calculated-results", label: t("accounting.reports.balanceSheet.calculatedGroup") },
        calculated("calculated-current-year-result", "accounting.reports.balanceSheet.currentYearResult", report.currentYearResult),
        calculated(
          "calculated-prior-years-unclosed-result",
          "accounting.reports.balanceSheet.priorYearsUnclosedResult",
          report.priorYearsUnclosedResult,
        ),
      ]
    : [];
  const liabilitiesAndEquity = report
    ? new Decimal(report.totalLiabilities).plus(report.totalEquity).toNumber()
    : 0;

  return (
    <>
      <ZHFilterBar disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField
            label={t("accounting.reports.filter.asOfDate")}
            density="compact"
            fieldError={showError ? asOfError : null}
          >
            <ZhDateInput className="zh-input" value={draft} onChange={(e) => setDraft(e.target.value)} />
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={apply} disabled={loading}>
          {t("accounting.reports.filter.apply")}
        </ZHBtn>
      </ZHFilterBar>

      <ZHCard
        title={<ReportScopeTitle parts={[`${t("accounting.reports.scope.asOf")}: ${formatDate(applied)}`]} />}
        actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_REPORTS_BALANCE_SHEET} />}
      >
        {error && !loading && <ErrorState message={`${t("accounting.reports.balanceSheet.loadError")} ${error}`} />}
        {!report && !error && <LoadingState />}
        {report && (
          <>
            <ReportCompletenessNotice completeness={report.completeness} />
            {!report.isBalanced && (
              <ZHPageNotice variant="error" message={t("accounting.reports.balanceSheet.unbalanced")} />
            )}
            <div className="table-scroll">
              <table className="table table--compact">
                <StatementHead
                  first={t("accounting.reports.balanceSheet.col.account")}
                  detail={t("accounting.reports.statement.col.detail")}
                  total={t("accounting.reports.statement.col.total")}
                />
                <FinancialStatementSection
                  title={t("accounting.reports.balanceSheet.section.assets")}
                  rows={groupedRows(report.assetLines, chart)}
                  emptyMessage={t("accounting.reports.balanceSheet.empty.assets")}
                  totalLabel={t("accounting.reports.balanceSheet.total.assets")}
                  total={report.totalAssets}
                />
                <FinancialStatementSection
                  title={t("accounting.reports.balanceSheet.section.liabilities")}
                  rows={groupedRows(report.liabilityLines, chart)}
                  emptyMessage={t("accounting.reports.balanceSheet.empty.liabilities")}
                  totalLabel={t("accounting.reports.balanceSheet.total.liabilities")}
                  total={report.totalLiabilities}
                />
                <FinancialStatementSection
                  title={t("accounting.reports.balanceSheet.section.equity")}
                  rows={equityRows}
                  emptyMessage={t("accounting.reports.balanceSheet.empty.equity")}
                  totalLabel={t("accounting.reports.balanceSheet.total.equity")}
                  total={report.totalEquity}
                />
                <tfoot>
                  <tr>
                    <td colSpan={2}>{t("accounting.reports.balanceSheet.total.liabilitiesEquity")}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={liabilitiesAndEquity} emphasis="grand" />
                    </td>
                  </tr>
                  <tr>
                    {/* Fila completa: el Badge (no divisible) no ensancha las columnas de importe en móvil. */}
                    <td colSpan={3}>
                      {t("accounting.reports.check.row")}{" "}
                      <Badge
                        variant={report.isBalanced ? "success" : "error"}
                        label={
                          // Un solo hijo inline: el Badge es flex y colapsaría el espacio antes del importe.
                          <span>
                            {t(
                              report.isBalanced
                                ? "accounting.reports.balanceSheet.balanced"
                                : "accounting.reports.balanceSheet.unbalancedBadge",
                            )}
                            {" · "}
                            {t("accounting.reports.balanceSheet.difference")}{" "}
                            <ZHMoneyValue precision="accounting" value={report.difference} />
                          </span>
                        }
                      />
                    </td>
                  </tr>
                </tfoot>
              </table>
            </div>
          </>
        )}
      </ZHCard>
    </>
  );
}
