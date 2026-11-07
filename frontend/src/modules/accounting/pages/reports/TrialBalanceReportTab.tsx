import { useState } from "react";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ReportScopeTitle } from "./reportScope";
import { dateRangeError } from "./reportDateRange";
import { AccountLabel } from "./FinancialStatementSection";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput } from "../../../../components/zh/inputs";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { Badge, ErrorState, LoadingState } from "../../../../components/PageShell";
import { HELP_KEYS } from "../../../../help";
import { useCompanyScopedAsync } from "../../../../hooks/useCompanyScopedAsync";
import { formatDate, firstDayOfMonthIso, todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";
import { accountTypeLabel } from "../../labels/accountingLabels";
import { accountingApi, type TrialBalanceLineDto } from "../../api/accountingApi";

type Filters = { fromDate: string; toDate: string; includeZeroMovementAccounts: boolean };

const AMOUNT_KEYS = [
  "openingDebit",
  "openingCredit",
  "periodDebit",
  "periodCredit",
  "closingDebit",
  "closingCredit",
] as const satisfies readonly (keyof TrialBalanceLineDto)[];

/** Orden contable de los grupos; un tipo no previsto se agrega al final sin perderse. */
const TYPE_ORDER = ["Asset", "Liability", "Equity", "Income", "Cost", "Expense"];

/** Agrupa por tipo de cuenta conservando el orden de código que entrega el backend dentro de cada grupo. */
function groupByType(lines: TrialBalanceLineDto[]): { type: string; lines: TrialBalanceLineDto[] }[] {
  const types = [...new Set(lines.map((l) => l.accountType))].sort(
    (a, b) => (TYPE_ORDER.indexOf(a) + 1 || 99) - (TYPE_ORDER.indexOf(b) + 1 || 99),
  );
  return types.map((type) => ({ type, lines: lines.filter((l) => l.accountType === type) }));
}

/**
 * Balance de Comprobación (ACCOUNTING-REPORTS-09) — `GET /accounting/reports/trial-balance`,
 * solo lectura. ACCOUNTING-REPORTS-PRO-15: matriz de comprobación — cuentas agrupadas por tipo
 * (banda `th scope="rowgroup"`, sin subtotales: D1), seis columnas monetarias bajo un encabezado
 * de dos niveles y, en `<tfoot>`, los seis totales del backend más la fila «Comprobación» bajo
 * cada par de columnas: el cuadre de MOVIMIENTOS es `isBalanced` del backend; saldo inicial y final
 * son una COMPARACIÓN de los totales Deudor/Acreedor recibidos (igualdad, sin aritmética: D2). La
 * columna Cuenta queda fija al desplazar en móvil (`table--sticky-column` oficial). Tabla nativa por
 * excepción documentada en frontend.md. Sin cambios de signos ni fórmulas.
 */
export function TrialBalanceReportTab() {
  const { t } = useI18n();
  const [draft, setDraft] = useState<Filters>(() => ({
    fromDate: firstDayOfMonthIso(),
    toDate: todayIso(),
    includeZeroMovementAccounts: false,
  }));
  const [applied, setApplied] = useState<Filters>(draft);
  const [showRangeError, setShowRangeError] = useState(false);
  const rangeError = dateRangeError(t, draft.fromDate, draft.toDate);

  const { data, loading, error } = useCompanyScopedAsync(
    () => accountingApi.getTrialBalanceReport(applied).then((report) => ({ filters: applied, report })),
    true,
    [applied],
  );
  const report = data && data.filters === applied && !error ? data.report : null;

  const apply = () => {
    setShowRangeError(true);
    if (rangeError) return;
    setApplied({ ...draft });
  };

  // D2: saldo inicial y final se COMPARAN (igualdad de los totales del backend, sin aritmética);
  // el cuadre de movimientos es `isBalanced` del backend. Rótulos distintos para no confundirlos.
  const totalsCheck = (debit: number, credit: number) =>
    t(debit === credit ? "accounting.reports.trialBalance.check.totalsEqual" : "accounting.reports.trialBalance.check.totalsDiffer");
  const checks = report
    ? [
        { key: "opening", ok: report.totalOpeningDebit === report.totalOpeningCredit, label: totalsCheck(report.totalOpeningDebit, report.totalOpeningCredit) },
        {
          key: "period",
          ok: report.isBalanced,
          label: t(report.isBalanced ? "accounting.reports.trialBalance.balanced" : "accounting.reports.trialBalance.unbalancedBadge"),
        },
        { key: "closing", ok: report.totalClosingDebit === report.totalClosingCredit, label: totalsCheck(report.totalClosingDebit, report.totalClosingCredit) },
      ]
    : [];

  const totals = report
    ? [
        report.totalOpeningDebit,
        report.totalOpeningCredit,
        report.totalPeriodDebit,
        report.totalPeriodCredit,
        report.totalClosingDebit,
        report.totalClosingCredit,
      ]
    : [];

  return (
    <>
      {/* Opción secundaria en la fila propia del slot oficial `chips` de ZHFilterBar, fuera de la
          fila de fechas + Consultar; se aplica igual que los demás filtros, al pulsar Consultar. */}
      <ZHFilterBar
        disabled={loading}
        chips={
          <ZHToggle
            label={t("accounting.reports.trialBalance.includeZero.label")}
            description={t("accounting.reports.trialBalance.includeZero.description")}
            value={draft.includeZeroMovementAccounts}
            onChange={(next) => setDraft({ ...draft, includeZeroMovementAccounts: next })}
          />
        }
      >
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
        actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_REPORTS_TRIAL_BALANCE} />}
      >
      {error && !loading && <ErrorState message={`${t("accounting.reports.trialBalance.loadError")} ${error}`} />}
      {!report && !error && <LoadingState />}
      {report && (
        <>
          <ReportCompletenessNotice completeness={report.completeness} />
          {report.lines.length > 0 && !report.isBalanced && (
            <ZHPageNotice variant="error" message={t("accounting.reports.trialBalance.unbalanced")} />
          )}
          <div className="table-scroll">
            <table className="table table--compact table--sticky-column">
              <thead>
                <tr>
                  <th rowSpan={2} className="table__sticky-cell">{t("accounting.reports.trialBalance.col.account")}</th>
                  <th colSpan={2} scope="colgroup" className="zh-text-align-center">
                    {t("accounting.reports.trialBalance.group.opening")}
                  </th>
                  <th colSpan={2} scope="colgroup" className="zh-text-align-center">
                    {t("accounting.reports.trialBalance.group.period")}
                  </th>
                  <th colSpan={2} scope="colgroup" className="zh-text-align-center">
                    {t("accounting.reports.trialBalance.group.closing")}
                  </th>
                </tr>
                <tr>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.debtor")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.creditor")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.debit")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.credit")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.debtor")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.trialBalance.col.creditor")}</th>
                </tr>
              </thead>
              {groupByType(report.lines).map((group) => (
                <tbody key={group.type}>
                  <tr>
                    <th scope="rowgroup" colSpan={7}>{accountTypeLabel(t, group.type)}</th>
                  </tr>
                  {group.lines.map((r) => (
                    <tr key={r.accountId}>
                      <td className="table__sticky-cell"><AccountLabel code={r.accountCode} name={r.accountName} /></td>
                      {AMOUNT_KEYS.map((k) => (
                        <td key={k} className="zh-table-cell--num">
                          <ZHMoneyValue
                            precision="accounting"
                            value={r[k] > 0 ? r[k] : null}
                            emphasis={k.startsWith("closing") ? "strong" : "default"}
                          />
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              ))}
              {report.lines.length === 0 && (
                <tbody>
                  <tr>
                    <td colSpan={7} className="zh-table-empty">{t("accounting.reports.trialBalance.empty")}</td>
                  </tr>
                </tbody>
              )}
              {report.lines.length > 0 && (
                <tfoot>
                  <tr>
                    <td>{t("accounting.reports.trialBalance.totals")}</td>
                    {totals.map((value, i) => (
                      <td key={AMOUNT_KEYS[i]} className="zh-table-cell--num">
                        <ZHMoneyValue precision="accounting" value={value} emphasis="total" />
                      </td>
                    ))}
                  </tr>
                  <tr>
                    <td>{t("accounting.reports.check.row")}</td>
                    {checks.map((c) => (
                      <td key={c.key} colSpan={2} className="zh-text-align-center">
                        <Badge label={c.label} variant={c.ok ? "success" : "error"} />
                      </td>
                    ))}
                  </tr>
                </tfoot>
              )}
            </table>
          </div>
        </>
      )}
      </ZHCard>
    </>
  );
}
