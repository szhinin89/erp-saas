import { useCallback, useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { useI18n } from "../../../i18n/i18n";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type FiscalYearClosingDto,
  type FiscalYearClosingStatus,
} from "../api/accountingApi";

const YEAR_END_VARIANT: Record<FiscalYearClosingStatus, BadgeVariant> = {
  NotGenerated: "gray",
  Draft: "orange",
  Posted: "green",
};

/**
 * ACCOUNTING-12D — Cierre anual dentro de Contabilidad › Períodos. Auditoría de reutilización: mismo
 * patrón de `AccountingPeriodsPage` (ZHCard + ZHDataTable + Badge + message.confirm) y `ZHMoneyValue`
 * del detalle de asiento; sin componentes nuevos de DS. Las precondiciones las valida el backend; el Draft
 * se revisa y publica en el detalle del asiento (flujo de manuales).
 * `refreshKey` recarga el estado cuando la página recarga los períodos (p. ej. tras cerrar uno).
 */
export function FiscalYearClosingCard({ canGenerate, refreshKey }: { canGenerate: boolean; refreshKey: unknown }) {
  const { t } = useI18n();
  const [closings, setClosings] = useState<FiscalYearClosingDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setClosings(await accountingApi.listFiscalYearClosings());
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.yearEnd") }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const generate = async (fiscalYear: number) => {
    const confirmed = await message.confirm({
      title: t("accounting.periods.yearEnd.generate.confirmTitle", { year: fiscalYear }),
      message: t("accounting.periods.yearEnd.generate.confirmMessage", { year: fiscalYear }),
      confirmLabel: t("accounting.periods.yearEnd.generate.action"),
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      const result = await accountingApi.generateFiscalYearClosing(fiscalYear);
      message.success(t("accounting.periods.yearEnd.generate.done", { year: result.fiscalYear }));
      await load();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.yearEndGenerate") }));
    } finally {
      setBusy(false);
    }
  };

  const columns: ZHDataTableColumn<FiscalYearClosingDto>[] = [
    { key: "year", header: t("accounting.periods.yearEnd.column.year"), render: (row) => row.fiscalYear },
    {
      key: "status",
      header: t("accounting.periods.yearEnd.column.status"),
      render: (row) => (
        <Badge label={t(`accounting.periods.yearEnd.status.${row.status}`)} variant={YEAR_END_VARIANT[row.status]} />
      ),
    },
    {
      key: "entry",
      header: t("accounting.periods.yearEnd.column.entry"),
      render: (row) =>
        row.journalEntryId && row.entryDate ? (
          <Link to={`/accounting/journal-entries/${row.journalEntryId}`}>
            {row.entryNumber !== null
              ? t("accounting.periods.yearEnd.entryNumber", { number: row.entryNumber, date: formatDate(row.entryDate) })
              : t("accounting.periods.yearEnd.entryDraft", { date: formatDate(row.entryDate) })}
          </Link>
        ) : (
          "—"
        ),
    },
    {
      key: "result",
      header: t("accounting.periods.yearEnd.column.result"),
      render: (row) => <ZHMoneyValue precision="accounting" value={row.result} />,
    },
    {
      key: "actions",
      header: t("accounting.periods.yearEnd.column.actions"),
      render: (row) => (
        <div className="zh-form-actions-row">
          {row.status === "NotGenerated" && row.lastPeriodStatus === "Open" && canGenerate && (
            <ZHBtn size="sm" type="button" disabled={busy} onClick={() => void generate(row.fiscalYear)}>
              {t("accounting.periods.yearEnd.generate.action")}
            </ZHBtn>
          )}
          {row.status === "Draft" && row.journalEntryId && (
            <Link to={`/accounting/journal-entries/${row.journalEntryId}`}>{t("accounting.periods.yearEnd.open")}</Link>
          )}
        </div>
      ),
    },
  ];

  return (
    <ZHCard title={t("accounting.periods.yearEnd.title")}>
      <p>{t("accounting.periods.yearEnd.hint")}</p>
      <ZHDataTable
        columns={columns}
        rows={closings}
        rowKey={(row) => String(row.fiscalYear)}
        loading={loading}
        emptyMessage={t("accounting.periods.yearEnd.empty")}
      />
    </ZHCard>
  );
}
