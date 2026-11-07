import { useState } from "react";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ReportScopeTitle } from "./reportScope";
import { dateRangeError } from "./reportDateRange";
import { AccountLabel, FinancialStatementSection, StatementHead, type StatementRow } from "./FinancialStatementSection";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { ErrorState, LoadingState } from "../../../../components/PageShell";
import { useCompanyScopedAsync } from "../../../../hooks/useCompanyScopedAsync";
import { HELP_KEYS } from "../../../../help";
import { formatDate, firstDayOfMonthIso, todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";
import { accountingApi, type FinancialStatementLineDto } from "../../api/accountingApi";

type Filters = { fromDate: string; toDate: string };

const toRows = (lines: FinancialStatementLineDto[]): StatementRow[] =>
  lines.map((l) => ({
    kind: "account",
    id: l.accountId,
    account: <AccountLabel code={l.accountCode} name={l.accountName} />,
    amount: l.amount,
  }));

/**
 * Estado de Resultados (ACCOUNTING-FINANCIAL-STATEMENTS-10) — `GET /accounting/reports/income-statement`,
 * solo lectura, solo período. ACCOUNTING-REPORTS-PRO-15: estado escalonado en dos columnas de
 * importe (Detalle por cuenta | Total por concepto): Ingresos → (−) Costos → (=) Utilidad bruta →
 * (−) Gastos → (=) Utilidad neta del período (`<tfoot>`, énfasis `grand`). Solo los conceptos del
 * modelo (Ingreso/Costo/Gasto): sin categorías inventadas. Un resultado negativo se rotula como
 * pérdida conservando importe y signo del backend; ningún importe se recalcula.
 */
export function IncomeStatementReportTab() {
  const { t } = useI18n();
  const [draft, setDraft] = useState<Filters>(() => ({ fromDate: firstDayOfMonthIso(), toDate: todayIso() }));
  const [applied, setApplied] = useState<Filters>(draft);
  const [showRangeError, setShowRangeError] = useState(false);
  const rangeError = dateRangeError(t, draft.fromDate, draft.toDate);

  const { data, loading, error } = useCompanyScopedAsync(
    () => accountingApi.getIncomeStatementReport(applied).then((report) => ({ filters: applied, report })),
    true,
    [applied],
  );
  const report = data && data.filters === applied && !error ? data.report : null;

  const apply = () => {
    setShowRangeError(true);
    if (rangeError) return;
    setApplied({ ...draft });
  };

  const grossLabel = report && report.grossProfit < 0
    ? t("accounting.reports.incomeStatement.grossLoss")
    : t("accounting.reports.incomeStatement.grossProfit");
  const netLabel = report && report.netProfit < 0
    ? t("accounting.reports.incomeStatement.netLoss")
    : t("accounting.reports.incomeStatement.netProfit");

  return (
    <>
      <ZHFilterBar disabled={loading}>
        <div className="zh-filterbar__field">
          <ZHField label={t("accounting.reports.filter.fromDate")} density="compact">
            <ZhDateInput
              className="zh-input"
              value={draft.fromDate}
              onChange={(e) => setDraft({ ...draft, fromDate: e.target.value })}
            />
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField
            label={t("accounting.reports.filter.toDate")}
            density="compact"
            fieldError={showRangeError ? rangeError : null}
          >
            <ZhDateInput
              className="zh-input"
              value={draft.toDate}
              onChange={(e) => setDraft({ ...draft, toDate: e.target.value })}
            />
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={apply} disabled={loading}>
          {t("accounting.reports.filter.apply")}
        </ZHBtn>
      </ZHFilterBar>

      <ZHCard
        title={
          <ReportScopeTitle
            parts={[
              t("accounting.reports.scope.periodValue", {
                from: formatDate(applied.fromDate),
                to: formatDate(applied.toDate),
              }),
            ]}
          />
        }
        actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_REPORTS_INCOME_STATEMENT} />}
      >
        {error && !loading && <ErrorState message={`${t("accounting.reports.incomeStatement.loadError")} ${error}`} />}
        {!report && !error && <LoadingState />}
        {report && (
          <>
            <ReportCompletenessNotice completeness={report.completeness} />
            <div className="table-scroll">
              <table className="table table--compact">
                <StatementHead
                  first={t("accounting.reports.statement.col.concept")}
                  detail={t("accounting.reports.statement.col.detail")}
                  total={t("accounting.reports.statement.col.total")}
                />
                <FinancialStatementSection
                  title={t("accounting.reports.incomeStatement.section.income")}
                  rows={toRows(report.incomeLines)}
                  emptyMessage={t("accounting.reports.incomeStatement.empty.income")}
                  totalLabel={t("accounting.reports.incomeStatement.total.income")}
                  total={report.totalIncome}
                />
                <FinancialStatementSection
                  title={`(−) ${t("accounting.reports.incomeStatement.section.cost")}`}
                  rows={toRows(report.costLines)}
                  emptyMessage={t("accounting.reports.incomeStatement.empty.cost")}
                  totalLabel={t("accounting.reports.incomeStatement.total.cost")}
                  total={report.totalCost}
                />
                {/* Resultado intermedio: banda oficial `th` con el importe en la columna Total. */}
                <tbody>
                  <tr>
                    <th scope="row" colSpan={2}>(=) {grossLabel}</th>
                    <th className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={report.grossProfit} emphasis="total" />
                    </th>
                  </tr>
                </tbody>
                <FinancialStatementSection
                  title={`(−) ${t("accounting.reports.incomeStatement.section.expense")}`}
                  rows={toRows(report.expenseLines)}
                  emptyMessage={t("accounting.reports.incomeStatement.empty.expense")}
                  totalLabel={t("accounting.reports.incomeStatement.total.expense")}
                  total={report.totalExpense}
                />
                <tfoot>
                  <tr>
                    <td colSpan={2}>(=) {netLabel}</td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={report.netProfit} emphasis="grand" />
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
