import { useMemo, useState } from "react";
import Decimal from "decimal.js";
import { ReportCompletenessNotice } from "./ReportCompletenessNotice";
import { ReportScopeTitle } from "./reportScope";
import { dateRangeError } from "./reportDateRange";
import { ZHCard } from "../../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../../components/zh/ZHFilterBar";
import { ZhDateInput, ZhSelect, ZhTextInput } from "../../../../components/zh/inputs";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../../components/zh/ZHMoneyValue";
import { ZHFieldHelp } from "../../../../components/zh/help";
import { EmptyState, ErrorState, LoadingState } from "../../../../components/PageShell";
import { HELP_KEYS } from "../../../../help";
import { useCompanyScopedAsync } from "../../../../hooks/useCompanyScopedAsync";
import { formatDate, firstDayOfMonthIso, todayIso } from "../../../../lib/formatters/dateFormatters";
import { useI18n } from "../../../../i18n/i18n";
import { journalExtraDescription, journalOperation } from "../../labels/accountingLabels";
import { accountingApi, type GeneralJournalLineDto } from "../../api/accountingApi";

const PAGE_SIZE = 50;
const SOURCE_MODULES = ["Sales", "Purchases", "Finance", "Inventory", "Accounting"] as const;

type Filters = { fromDate: string; toDate: string; sourceModule: string; search: string; page: number };

/** Líneas consecutivas de un mismo asiento (el backend las entrega por asiento, en orden). */
type EntryLines = { key: string; first: GeneralJournalLineDto; lines: GeneralJournalLineDto[] };
type EntryGroup = EntryLines & { debit: number; credit: number };

/**
 * El backend pagina por ASIENTOS y entrega todas las líneas de cada asiento de la página, así que
 * el total por asiento se obtiene de líneas completas; se suma con Decimal (sin aritmética float).
 */
function groupByEntry(lines: GeneralJournalLineDto[]): EntryGroup[] {
  const groups: EntryLines[] = [];
  for (const line of lines) {
    const last = groups.at(-1);
    if (last && last.first.journalEntryId === line.journalEntryId) last.lines.push(line);
    else groups.push({ key: `${line.journalEntryId}-${groups.length}`, first: line, lines: [line] });
  }
  return groups.map((g) => ({
    ...g,
    debit: g.lines.reduce((sum, l) => sum.plus(l.debit), new Decimal(0)).toNumber(),
    credit: g.lines.reduce((sum, l) => sum.plus(l.credit), new Decimal(0)).toNumber(),
  }));
}

/**
 * Libro Diario (ACCOUNTING-REPORTS-09) — `GET /accounting/reports/general-journal`, solo lectura.
 * ACCOUNTING-REPORTS-UX-13/14: un `<tbody>` por asiento; su primera fila es una banda de encabezado
 * con `th scope="rowgroup"` (estilo oficial de `.table th`: N°, fecha, operación traducida,
 * documento de origen y descripción solo si aporta) y debajo sus líneas (Código, Cuenta, Debe,
 * Haber) y el total del asiento — sin `rowSpan` ni CSS propio. ACCOUNTING-REPORTS-PRO-15: el pie
 * identifica la página y el rango de asientos cuyos totales (API) muestra. El tipo de documento se muestra
 * solo con las etiquetas traducidas oficiales (`journalOperation`), nunca con el texto
 * `sourceDocumentType` del backend (no traducible). Consulta
 * solo con «Consultar» y rango validado; paginación por ASIENTOS (el backend pagina asientos:
 * `totalCount` cuenta asientos). Tabla nativa por excepción documentada en frontend.md (filas de
 * encabezado por asiento y `<tfoot>` que `ZHDataTable` no expone); el pie de paginación reutiliza
 * las clases oficiales del de `ZHDataTable`.
 */
export function GeneralJournalReportTab() {
  const { t } = useI18n();
  const [draft, setDraft] = useState<Filters>(() => ({
    fromDate: firstDayOfMonthIso(),
    toDate: todayIso(),
    sourceModule: "",
    search: "",
    page: 1,
  }));
  const [applied, setApplied] = useState<Filters>(draft);
  const [showRangeError, setShowRangeError] = useState(false);
  const rangeError = dateRangeError(t, draft.fromDate, draft.toDate);

  const { data, loading, error } = useCompanyScopedAsync(
    () =>
      accountingApi
        .getGeneralJournalReport({
          fromDate: applied.fromDate,
          toDate: applied.toDate,
          sourceModule: applied.sourceModule || undefined,
          search: applied.search || undefined,
          pageNumber: applied.page,
          pageSize: PAGE_SIZE,
        })
        .then((report) => ({ filters: applied, report })),
    true,
    [applied],
  );
  // Solo se muestra el resultado de la consulta aplicada vigente — nunca datos de una anterior.
  const report = data && data.filters === applied && !error ? data.report : null;
  const groups = useMemo(() => (report ? groupByEntry(report.lines) : []), [report]);

  const apply = () => {
    setShowRangeError(true);
    if (rangeError) return;
    setApplied({ ...draft, page: 1 });
  };
  const goToPage = (page: number) => setApplied({ ...applied, page });
  const totalPages = report ? Math.max(1, Math.ceil(report.totalCount / PAGE_SIZE)) : 1;
  // Totales de página identificados: página y rango de asientos que suman (importes de la API).
  const firstNumber = groups[0]?.first.entryNumber;
  const lastNumber = groups.at(-1)?.first.entryNumber;
  const pageTotalsLabel =
    firstNumber != null && lastNumber != null
      ? t("accounting.reports.journal.pageTotalsRange", {
          page: String(applied.page),
          pages: String(totalPages),
          from: String(firstNumber),
          to: String(lastNumber),
        })
      : t("accounting.reports.journal.pageTotals");
  const sourceName = (module: string) =>
    module ? t(`accounting.reports.journal.source.${module}`) : t("accounting.reports.journal.source.all");

  /** N° · fecha · operación + documento de origen; sin número se antepone el módulo (nunca se oculta). */
  const entryHeading = (line: GeneralJournalLineDto) => {
    const op = journalOperation(t, line);
    const extra = journalExtraDescription(t, line);
    return (
      <>
        {line.entryNumber !== null
          ? t("accounting.reports.journal.entryNumber", { number: String(line.entryNumber) })
          : t("accounting.reports.journal.noNumber")}
        {" · "}
        {formatDate(line.entryDate)}
        {" · "}
        {line.sourceDocumentNumber || !op.module ? op.label : `${op.module} / ${op.label}`}
        {line.sourceDocumentNumber && (
          <> <span className="zh-code-value zh-code-value--document">{line.sourceDocumentNumber}</span></>
        )}
        {extra && <>{" · "}{extra}</>}
      </>
    );
  };

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
          <ZHField label={t("accounting.reports.journal.filter.source")} density="compact">
            <ZhSelect
              className="zh-input"
              value={draft.sourceModule}
              onChange={(e) => setDraft({ ...draft, sourceModule: e.target.value })}
            >
              <option value="">{t("accounting.reports.journal.source.all")}</option>
              {SOURCE_MODULES.map((m) => (
                <option key={m} value={m}>{t(`accounting.reports.journal.source.${m}`)}</option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field">
          <ZHField label={t("accounting.reports.journal.filter.search")} density="compact">
            <ZhTextInput
              className="zh-input"
              placeholder={t("accounting.reports.journal.filter.searchPlaceholder")}
              value={draft.search}
              onChange={(e) => setDraft({ ...draft, search: e.target.value })}
              onKeyDown={(e) => { if (e.key === "Enter") apply(); }}
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
              sourceName(applied.sourceModule),
              ...(applied.search ? [`${t("accounting.reports.journal.filter.search")}: ${applied.search}`] : []),
            ]}
          />
        }
        actions={<ZHFieldHelp helpKey={HELP_KEYS.ACCOUNTING_REPORTS_GENERAL_JOURNAL} />}
      >
        {error && !loading && <ErrorState message={`${t("accounting.reports.journal.loadError")} ${error}`} />}
        {!report && !error && <LoadingState />}
        {report && (
          <>
            <ReportCompletenessNotice completeness={report.completeness} />
            {report.lines.length === 0 ? (
              <EmptyState message={t("accounting.reports.journal.empty")} />
            ) : (
              <>
                {report.totalDebit !== report.totalCredit && (
                  <ZHPageNotice variant="warning" message={t("accounting.reports.journal.pageUnbalanced")} />
                )}
                <div className="table-scroll">
                  <table className="table table--compact">
                    <thead>
                      <tr>
                        <th>{t("accounting.reports.journal.col.code")}</th>
                        <th>{t("accounting.reports.journal.col.account")}</th>
                        <th className="zh-table-cell--num">{t("accounting.reports.journal.col.debit")}</th>
                        <th className="zh-table-cell--num">{t("accounting.reports.journal.col.credit")}</th>
                      </tr>
                    </thead>
                    {groups.map((g) => (
                      <tbody key={g.key}>
                        <tr>
                          <th scope="rowgroup" colSpan={4}>{entryHeading(g.first)}</th>
                        </tr>
                        {g.lines.map((line, index) => (
                          <tr key={`${g.key}-${index}`}>
                            <td><span className="zh-code-value zh-code-value--document">{line.accountCode}</span></td>
                            <td>{line.accountName}</td>
                            <td className="zh-table-cell--num">
                              <ZHMoneyValue precision="accounting" value={line.debit > 0 ? line.debit : null} />
                            </td>
                            <td className="zh-table-cell--num">
                              <ZHMoneyValue precision="accounting" value={line.credit > 0 ? line.credit : null} />
                            </td>
                          </tr>
                        ))}
                        <tr>
                          <td colSpan={2}><strong>{t("accounting.reports.journal.entryTotal")}</strong></td>
                          <td className="zh-table-cell--num">
                            <ZHMoneyValue precision="accounting" value={g.debit} emphasis="strong" />
                          </td>
                          <td className="zh-table-cell--num">
                            <ZHMoneyValue precision="accounting" value={g.credit} emphasis="strong" />
                          </td>
                        </tr>
                      </tbody>
                    ))}
                    <tfoot>
                      <tr>
                        <td colSpan={2}>{pageTotalsLabel}</td>
                        <td className="zh-table-cell--num">
                          <ZHMoneyValue precision="accounting" value={report.totalDebit} emphasis="total" />
                        </td>
                        <td className="zh-table-cell--num">
                          <ZHMoneyValue precision="accounting" value={report.totalCredit} emphasis="total" />
                        </td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
                <div className="pg-table-footer">
                  <span className="zh-datatable-total">
                    {t("accounting.reports.journal.pagination", {
                      count: String(report.totalCount),
                      page: String(applied.page),
                      pages: String(totalPages),
                    })}
                  </span>
                  <div className="pg-pagination-controls">
                    <button
                      className="pg-pagination-btn"
                      type="button"
                      aria-label={t("accounting.reports.journal.previousPage")}
                      disabled={applied.page <= 1}
                      onClick={() => goToPage(applied.page - 1)}
                    >
                      <span className="material-symbols-outlined">chevron_left</span>
                    </button>
                    <span className="zh-datatable-page-number">{applied.page}</span>
                    <button
                      className="pg-pagination-btn"
                      type="button"
                      aria-label={t("accounting.reports.journal.nextPage")}
                      disabled={applied.page >= totalPages}
                      onClick={() => goToPage(applied.page + 1)}
                    >
                      <span className="material-symbols-outlined">chevron_right</span>
                    </button>
                  </div>
                </div>
              </>
            )}
          </>
        )}
      </ZHCard>
    </>
  );
}
