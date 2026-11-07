import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useCallback, useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDateInput, ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import {
  journalExtraDescription,
  journalModuleChip,
  journalOperation,
  journalStatusBadge,
} from "../labels/accountingLabels";
import { accountingApi, type JournalEntryListItemDto } from "../api/accountingApi";

const PAGE_SIZE = 20;
const STATUSES = ["Draft", "Posted", "Discarded"] as const;

/** Badge oficial con Material Symbol al inicio (la etiqueta de `Badge` admite ReactNode). */
function IconBadge({ icon, label, variant }: { icon: string; label: string; variant: "success" | "warning" | "info" | "neutral" | "error" }) {
  return (
    <Badge
      variant={variant}
      label={
        <>
          <span className="material-symbols-outlined zh-icon-sm" aria-hidden="true">{icon}</span> {label}
        </>
      }
    />
  );
}

/**
 * Listado de asientos contables (ACCOUNTING-LEDGER-VISIBILITY-01) — consume exclusivamente
 * `GET /api/v1/accounting/journal-entries`. Solo lectura: el Posting Engine y el flujo de manuales
 * siguen siendo las únicas vías de escritura.
 * ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: sin botones cruzados hacia otras pantallas; el único
 * link saliente es el detalle `/accounting/journal-entries/:id` (botón o clic en la fila).
 * ACCOUNTING-JOURNAL-UX-01: barra de filtros `ZHFilterBar` (origen, estado y rango de fechas —
 * parámetros ya soportados por el endpoint) con campos de ancho equilibrado, y 7 columnas compuestas
 * en una sola línea por fila. El descuadre se infiere de `totalDebit !== totalCredit`: el DTO del
 * listado no expone `isBalanced` y ambos totales salen de la misma suma de líneas en servidor.
 */
export function JournalEntriesPage() {
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const [items, setItems] = useState<JournalEntryListItemDto[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [origin, setOrigin] = useState("");
  const [status, setStatus] = useState("");
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");
  const [loading, setLoading] = useState(false);

  const fetchList = useCallback(async () => {
    setLoading(true);
    try {
      const r = await accountingApi.listJournalEntries(page, PAGE_SIZE, {
        status: status || undefined,
        manual: origin === "" ? undefined : origin === "manual",
        fromDate: fromDate || undefined,
        toDate: toDate || undefined,
      });
      setItems(r.items);
      setTotal(r.totalCount);
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.journal.loadError") }));
    } finally {
      setLoading(false);
    }
  }, [page, status, origin, fromDate, toDate, t]);

  useEffect(() => {
    void fetchList();
  }, [fetchList]);

  const changeFilter = (apply: () => void) => {
    setPage(1);
    apply();
  };

  const clearFilters = () => {
    setPage(1);
    setOrigin("");
    setStatus("");
    setFromDate("");
    setToDate("");
  };

  const openDetail = (row: JournalEntryListItemDto) => navigate(`/accounting/journal-entries/${row.id}`);

  const columns: ZHDataTableColumn<JournalEntryListItemDto>[] = [
    {
      key: "entry",
      header: t("accounting.journal.column.entry"),
      // Dos líneas: dato principal (ZHDataValue default) y secundario (ZHDataValue muted).
      render: (row) => (
        <div className="zh-stack">
          <ZHDataValue>
            {row.entryNumber != null
              ? t("accounting.journal.entryNumber", { number: row.entryNumber })
              : t("accounting.journal.unnumbered")}
          </ZHDataValue>
          <ZHDataValue variant="muted">{formatDate(row.entryDate)}</ZHDataValue>
        </div>
      ),
    },
    {
      key: "operation",
      header: t("accounting.journal.column.operation"),
      render: (row) => {
        const op = journalOperation(t, row);
        const extra = journalExtraDescription(t, row);
        const chip = journalModuleChip(row.sourceModule);
        return (
          <div className="zh-stack">
            <ZHDataValue>{op.label}</ZHDataValue>
            {/* El badge va dentro de un bloque para no estirarse en el flex de .zh-stack. */}
            {op.module && (
              <div className="zh-mt-4">
                <IconBadge icon={chip.icon} label={op.module} variant={chip.variant} />
              </div>
            )}
            {extra && <ZHDataValue variant="muted">{extra}</ZHDataValue>}
          </div>
        );
      },
    },
    {
      key: "document",
      header: t("accounting.journal.column.document"),
      render: (row) => {
        if (journalOperation(t, row).module === null) return "—";
        const reference = [
          row.sourceDocumentType,
          row.sourceDocumentDate ? formatDate(row.sourceDocumentDate) : null,
        ]
          .filter(Boolean)
          .join(" · ");
        return (
          <div className="zh-stack">
            {row.sourceDocumentNumber ? (
              <span className="zh-code-value zh-code-value--document">{row.sourceDocumentNumber}</span>
            ) : (
              <span className="zh-code-value zh-code-value--inline" title={row.sourceEventId}>
                {row.sourceEventId.slice(0, 8)}…
              </span>
            )}
            {reference && <ZHDataValue variant="muted">{reference}</ZHDataValue>}
          </div>
        );
      },
    },
    {
      key: "party",
      header: t("accounting.journal.column.party"),
      render: (row) => row.sourcePartyName ?? "—",
    },
    {
      key: "amount",
      header: t("accounting.journal.column.amount"),
      align: "right",
      cellClassName: "zh-table-cell--num",
      render: (row) => (
        <>
          {row.totalDebit !== row.totalCredit && row.status !== "Discarded" && (
            <>
              <IconBadge icon="warning" label={t("accounting.journal.unbalanced")} variant="error" />{" "}
            </>
          )}
          <ZHMoneyValue precision="accounting" value={row.totalDebit} />
        </>
      ),
    },
    {
      key: "status",
      header: t("common.status"),
      render: (row) => {
        const b = journalStatusBadge(t, row.status);
        return <IconBadge icon={b.icon} label={b.label} variant={b.variant} />;
      },
    },
    {
      key: "actions",
      header: t("common.actions"),
      cellClassName: "zh-table-cell--actions",
      render: (row) => (
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          onClick={(e) => {
            e.stopPropagation();
            openDetail(row);
          }}
        >
          {t("accounting.journal.viewDetail")}
        </ZHBtn>
      ),
    },
  ];

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("app.nav.item.accounting.journalEntries")}
      subtitle={t("accounting.journal.subtitle")}
      action={
        canShow("accounting.manual.create") ? (
          <ZHBtn variant="primary" icon="add" onClick={() => navigate("/accounting/journal-entries/new")}>
            {t("accounting.journal.newManual")}
          </ZHBtn>
        ) : undefined
      }
    >
      <ZHFilterBar onClear={clearFilters} disabled={loading}>
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label={t("accounting.journal.filter.origin")} density="compact">
            <ZhSelect value={origin} onChange={(e) => changeFilter(() => setOrigin(e.target.value))}>
              <option value="">{t("accounting.journal.filter.allOrigins")}</option>
              <option value="manual">{t("accounting.journal.filter.manual")}</option>
              <option value="automatic">{t("accounting.journal.filter.automatic")}</option>
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label={t("common.status")} density="compact">
            <ZhSelect value={status} onChange={(e) => changeFilter(() => setStatus(e.target.value))}>
              <option value="">{t("common.allStatuses")}</option>
              {STATUSES.map((s) => (
                <option key={s} value={s}>
                  {journalStatusBadge(t, s).label}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        </div>
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label={t("accounting.journal.filter.fromDate")} density="compact">
            <ZhDateInput
              density="compact"
              value={fromDate}
              max={toDate || undefined}
              onChange={(e) => changeFilter(() => setFromDate(e.target.value))}
            />
          </ZHField>
        </div>
        <div className="zh-filterbar__field zh-filterbar__field--grow">
          <ZHField label={t("accounting.journal.filter.toDate")} density="compact">
            <ZhDateInput
              density="compact"
              value={toDate}
              min={fromDate || undefined}
              onChange={(e) => changeFilter(() => setToDate(e.target.value))}
            />
          </ZHField>
        </div>
        <ZHBtn variant="ghost" size="sm" type="button" icon="refresh" onClick={() => void fetchList()} disabled={loading}>
          {t("common.refresh")}
        </ZHBtn>
      </ZHFilterBar>
      <ZHCard>
        <ZHDataTable
          columns={columns}
          rows={items}
          rowKey={(row) => row.id}
          onRowClick={openDetail}
          loading={loading}
          emptyMessage={t("accounting.journal.empty")}
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          total={total}
        />
      </ZHCard>
    </PageShell>
  );
}
