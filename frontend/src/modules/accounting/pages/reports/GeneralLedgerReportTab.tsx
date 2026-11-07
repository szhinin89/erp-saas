import { useMemo, useState } from "react";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ReportScopeTitle } from "./reportScope";
import { dateRangeError } from "./reportDateRange";
import { AccountLabel } from "./FinancialStatementSection";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput, ZhSelect } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { Badge, ErrorState, LoadingState } from "../../../../components/PageShell";
import { HELP_KEYS } from "../../../../help";
import { useCompanyScopedAsync } from "../../../../hooks/useCompanyScopedAsync";
import { formatDate, firstDayOfMonthIso, todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";
import {
  accountNatureLabel,
  accountTypeLabel,
  friendlyDescription,
  sourceModuleLabel,
} from "../../labels/accountingLabels";
import { accountingApi, type GeneralLedgerAccountDto } from "../../api/accountingApi";

type Filters = { fromDate: string; toDate: string; accountId: string };

/** Cuenta con saldo propio o actividad: nunca se descarta una cuenta con solo saldo inicial. */
function hasBalanceOrActivity(a: GeneralLedgerAccountDto): boolean {
  return (
    a.openingBalance !== 0 ||
    a.periodDebit !== 0 ||
    a.periodCredit !== 0 ||
    a.closingBalance !== 0 ||
    a.movements.length > 0
  );
}

/**
 * Libro Mayor (ACCOUNTING-REPORTS-09) — `GET /accounting/reports/general-ledger`, solo lectura.
 * ACCOUNTING-REPORTS-PRO-15 (D3): el reporte es el MAYOR POR CUENTA dentro de la página —
 * identificación (código, nombre, tipo, naturaleza), fila de saldo inicial, movimientos en orden
 * cronológico (fecha · N° de asiento, referencia y descripción apilados en la columna Movimiento —
 * fija al desplazar en móvil con `table--sticky-column` oficial —, Debe, Haber, saldo acumulado),
 * movimientos del período y saldo final en `<tfoot>`. El resumen de cuentas con saldo o actividad
 * es el ÍNDICE: al elegir una cuenta se abre su mayor con «Índice de cuentas» para volver; si el
 * filtro ya fija una cuenta, su mayor se muestra directamente. Todos los importes (saldo inicial,
 * saldos acumulados, totales y saldo final) son los del backend, según la naturaleza de la cuenta
 * (un saldo negativo = saldo contrario a su naturaleza); nada se recalcula. Tabla nativa por
 * excepción documentada en frontend.md (bandas y filas de saldo que `ZHDataTable` no expone).
 */
export function GeneralLedgerReportTab() {
  const { t } = useI18n();
  const [draft, setDraft] = useState<Filters>(() => ({
    fromDate: firstDayOfMonthIso(),
    toDate: todayIso(),
    accountId: "",
  }));
  const [applied, setApplied] = useState<Filters>(draft);
  const [showRangeError, setShowRangeError] = useState(false);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const rangeError = dateRangeError(t, draft.fromDate, draft.toDate);

  const chart = useCompanyScopedAsync(() => accountingApi.listAccounts());
  const accounts = useMemo(() => chart.data ?? [], [chart.data]);
  const postableAccounts = useMemo(() => accounts.filter((a) => a.allowsPosting), [accounts]);
  const groupingIds = useMemo(
    () => new Set(accounts.filter((a) => !a.allowsPosting).map((a) => a.id)),
    [accounts],
  );

  const { data, loading, error } = useCompanyScopedAsync(
    () =>
      accountingApi
        .getGeneralLedgerReport({
          fromDate: applied.fromDate,
          toDate: applied.toDate,
          accountId: applied.accountId || undefined,
        })
        .then((report) => ({ filters: applied, report })),
    true,
    [applied],
  );
  const report = data && data.filters === applied && !error ? data.report : null;

  // Una cuenta elegida explícitamente en el filtro se muestra siempre, aunque no tenga saldo.
  const keep = (a: GeneralLedgerAccountDto) => !!applied.accountId || hasBalanceOrActivity(a);
  const visible = report ? report.accounts.filter(keep) : [];
  const hidden = report ? report.accounts.filter((a) => !keep(a)) : [];
  const hiddenGrouping = hidden.filter((a) => groupingIds.has(a.accountId)).length;
  // Cuenta del filtro: su mayor directo (sin índice). Sin filtro: la elegida en el índice.
  const ledger = applied.accountId
    ? (visible.find((a) => a.accountId === applied.accountId) ?? null)
    : (visible.find((a) => a.accountId === selectedId) ?? null);

  const apply = () => {
    setShowRangeError(true);
    if (rangeError) return;
    setSelectedId(null);
    setApplied({ ...draft });
  };

  const appliedAccount = postableAccounts.find((a) => a.id === applied.accountId);
  const period = {
    from: formatDate(applied.fromDate),
    to: formatDate(applied.toDate),
  };

  const indexColumns: ZHDataTableColumn<GeneralLedgerAccountDto>[] = [
    {
      key: "account",
      header: t("accounting.reports.ledger.col.account"),
      cellClassName: "table__sticky-cell",
      render: (r) => (
        <>
          <AccountLabel code={r.accountCode} name={r.accountName} />
          {groupingIds.has(r.accountId) && (
            <> <Badge label={t("accounting.reports.ledger.groupingBadge")} variant="warning" /></>
          )}
        </>
      ),
    },
    {
      key: "opening",
      header: t("accounting.reports.ledger.col.opening"),
      cellClassName: "zh-table-cell--num",
      render: (r) => <ZHMoneyValue precision="accounting" value={r.openingBalance} />,
    },
    {
      key: "debit",
      header: t("accounting.reports.ledger.col.debit"),
      cellClassName: "zh-table-cell--num",
      render: (r) => <ZHMoneyValue precision="accounting" value={r.periodDebit > 0 ? r.periodDebit : null} />,
    },
    {
      key: "credit",
      header: t("accounting.reports.ledger.col.credit"),
      cellClassName: "zh-table-cell--num",
      render: (r) => <ZHMoneyValue precision="accounting" value={r.periodCredit > 0 ? r.periodCredit : null} />,
    },
    {
      key: "closing",
      header: t("accounting.reports.ledger.col.closing"),
      cellClassName: "zh-table-cell--num",
      render: (r) => <ZHMoneyValue precision="accounting" value={r.closingBalance} emphasis="strong" />,
    },
  ];

  const scopeAccount = ledger
    ? `${ledger.accountCode} — ${ledger.accountName}`
    : appliedAccount
      ? `${appliedAccount.code} — ${appliedAccount.name}`
      : t("accounting.reports.ledger.filter.allAccounts");

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
        <div className="zh-filterbar__field">
          <ZHField label={t("accounting.reports.ledger.filter.account")} density="compact">
            <ZhSelect
              className="zh-input"
              value={draft.accountId}
              onChange={(e) => setDraft({ ...draft, accountId: e.target.value })}
            >
              <option value="">{t("accounting.reports.ledger.filter.allAccounts")}</option>
              {postableAccounts.map((a) => (
                <option key={a.id} value={a.id}>{a.code} — {a.name}</option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <ZHBtn type="button" variant="primary" onClick={apply} disabled={loading}>
          {t("accounting.reports.filter.apply")}
        </ZHBtn>
      </ZHFilterBar>

      <ZHCard
        title={
          <ReportScopeTitle parts={[t("accounting.reports.scope.periodValue", period), scopeAccount]} />
        }
        actions={
          <span className="zh-form-actions-row zh-form-actions-row--flush">
            {ledger && !applied.accountId && (
              <ZHBtn type="button" variant="ghost" size="sm" icon="arrow_back" onClick={() => setSelectedId(null)}>
                {t("accounting.reports.ledger.backToIndex")}
              </ZHBtn>
            )}
            <ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_REPORTS_GENERAL_LEDGER} />
          </span>
        }
      >
        {error && !loading && <ErrorState message={`${t("accounting.reports.ledger.loadError")} ${error}`} />}
        {!report && !error && <LoadingState />}
        {report && <ReportCompletenessNotice completeness={report.completeness} />}
        {report && ledger && (
          <div className="table-scroll">
            <table className="table table--compact table--align-top table--sticky-column">
              <thead>
                <tr>
                  <th className="table__sticky-cell">{t("accounting.reports.ledger.mov.movement")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.ledger.mov.debit")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.ledger.mov.credit")}</th>
                  <th className="zh-table-cell--num">{t("accounting.reports.ledger.mov.balance")}</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <th scope="rowgroup" colSpan={4}>
                    <AccountLabel code={ledger.accountCode} name={ledger.accountName} />
                    {" · "}
                    {accountTypeLabel(t, ledger.accountType)}
                    {" · "}
                    {t("accounting.reports.ledger.col.nature")}: {accountNatureLabel(t, ledger.nature)}
                  </th>
                </tr>
                <tr>
                  <td colSpan={3}>{t("accounting.reports.ledger.openingRow", { date: period.from })}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={ledger.openingBalance} emphasis="strong" />
                  </td>
                </tr>
                {ledger.movements.map((m, i) => (
                  <tr key={`${m.journalEntryId}-${i}`}>
                    <td className="table__sticky-cell">
                      <strong>{formatDate(m.entryDate)}</strong>
                      {" · "}
                      {t("accounting.reports.journal.entryNumber", { number: m.entryNumber === null ? "—" : String(m.entryNumber) })}
                      <div>
                      {m.sourceDocumentNumber ? (
                        <span className="zh-code-value zh-code-value--document">{m.sourceDocumentNumber}</span>
                      ) : (
                        <span className="zh-text-muted">{sourceModuleLabel(t, m.sourceModule)}</span>
                      )}
                      </div>
                      <div className="zh-text-muted">{friendlyDescription(t, m.description)}</div>
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={m.debit > 0 ? m.debit : null} />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={m.credit > 0 ? m.credit : null} />
                    </td>
                    <td className="zh-table-cell--num">
                      <ZHMoneyValue precision="accounting" value={m.runningBalance} />
                    </td>
                  </tr>
                ))}
                {ledger.movements.length === 0 && (
                  <tr>
                    <td colSpan={4} className="zh-text-muted">{t("accounting.reports.ledger.mov.empty")}</td>
                  </tr>
                )}
                <tr>
                  <td><strong>{t("accounting.reports.ledger.periodRow")}</strong></td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={ledger.periodDebit} emphasis="total" />
                  </td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={ledger.periodCredit} emphasis="total" />
                  </td>
                  <td />
                </tr>
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={3}>{t("accounting.reports.ledger.closingRow", { date: period.to })}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={ledger.closingBalance} emphasis="grand" />
                  </td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
        {report && !ledger && (
          <>
            <ZHDataTable
              columns={indexColumns}
              rows={visible}
              rowKey={(r) => r.accountId}
              onRowClick={(r) => setSelectedId(r.accountId)}
              tableClassName="table--compact table--sticky-column"
              emptyMessage={t("accounting.reports.ledger.empty")}
            />
            {(visible.length > 0 || hidden.length > 0) && (
              <p className="zh-text-muted zh-text-xs zh-mt-8">
                {visible.length > 0 && <>{t("accounting.reports.ledger.selectHint")} </>}
                {hidden.length > 0 &&
                  (hiddenGrouping > 0
                    ? t("accounting.reports.ledger.hiddenAccountsWithGrouping", {
                        count: String(hidden.length),
                        grouping: String(hiddenGrouping),
                      })
                    : t("accounting.reports.ledger.hiddenAccounts", { count: String(hidden.length) }))}
              </p>
            )}
          </>
        )}
      </ZHCard>
    </>
  );
}
