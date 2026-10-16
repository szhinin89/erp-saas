import { useForm } from "react-hook-form";
import { z } from "zod";
import { zodResolver } from "@hookform/resolvers/zod";
import { applyServerErrors } from "../../lib/validationErrors";
import { useCallback, useEffect, useMemo, useState } from "react";
import {
  PageShell,
  Badge,
  type BadgeVariant,
} from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZhSelect, ZhTextarea } from "../../../components/zh/inputs";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountingPostingBatchItemDto,
  type AccountingPostingAttemptDto,
  type AccountingPostingDetailDto,
  type AccountingPostingDto,
  type AccountingPostingStatus,
} from "../api/accountingApi";
import { factTypeLabel, sourceModuleLabel } from "../labels/accountingLabels";

import "../../../styles/shared/items-catalog.css";

const REPROCESS_PERMISSION = "accounting.postings.reprocess";
const STATUSES: AccountingPostingStatus[] = [
  "Pending",
  "Failed",
  "Posted",
  "Canceled",
];
// Orígenes que hoy generan obligación durable (ACCOUNTING-CONTINUITY-01).
const SOURCE_MODULES = ["Purchases", "Expenses", "Retentions"];

const STATUS_VARIANT: Record<AccountingPostingStatus, BadgeVariant> = {
  Pending: "orange",
  Failed: "red",
  Posted: "green",
  Canceled: "gray",
};

/**
 * Contabilidad → Asientos → Contabilizaciones (ACCOUNTING-CONTINUITY-01). Auditoría de
 * reutilización: revisadas `PostingRulesPage.tsx` (filtros ZHFilterBar+ZHField compact+ZhSelect) y
 * `JournalEntriesPage.tsx` (listado ZHDataTable con Badge de estado); historial en `ZHModal` y
 * confirmation via message.confirm; RHF/Zod + ZhTextarea for the reason and checkbox selection.
 * Extends this page with a manual batch and ZHDataTable results. No new components.
 */
export function AccountingPostingsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canReprocess = canShow(REPROCESS_PERMISSION);
  const [postings, setPostings] = useState<AccountingPostingDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [status, setStatus] = useState<AccountingPostingStatus | "">("Failed");
  const [sourceModule, setSourceModule] = useState("");
  const [busyId, setBusyId] = useState<string | null>(null);
  const [selected, setSelected] = useState<string[]>([]);
  const [batchMode, setBatchMode] = useState(false);
  const [requestIds, setRequestIds] = useState<string[] | null>(null);
  const [results, setResults] = useState<
    AccountingPostingBatchItemDto[] | null
  >(null);
  const form = useForm<{ reason: string }>({
    resolver: zodResolver(
      z.object({
        reason: z
          .string()
          .trim()
          .min(1, t("accounting.continuity.postings.reasonRequired"))
          .max(500),
      }),
    ),
    defaultValues: { reason: "" },
  });
  const [detail, setDetail] = useState<AccountingPostingDetailDto | null>(null);

  const statusLabel = useCallback(
    (value: AccountingPostingStatus) =>
      t(`accounting.continuity.postings.status.${value}`),
    [t],
  );

  const fetchPostings = useCallback(async () => {
    setLoading(true);
    try {
      setPostings(await accountingApi.listPostings({ status, sourceModule }));
      setSelected([]);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, {
          generic: t("accounting.continuity.postings.error.load"),
        }),
      );
    } finally {
      setLoading(false);
    }
  }, [status, sourceModule, t]);

  useEffect(() => {
    void fetchPostings();
  }, [fetchPostings]);

  const openHistory = useCallback(
    async (row: AccountingPostingDto) => {
      try {
        setDetail(await accountingApi.getPosting(row.id));
      } catch (err: unknown) {
        message.error(
          formatApiRequestError(err, {
            generic: t("accounting.continuity.postings.error.load"),
          }),
        );
      }
    },
    [t],
  );

  const reprocess = async ({ reason }: { reason: string }) => {
    if (!requestIds?.length || requestIds.length > 100 || busyId) return;
    const confirmed = await message.confirm({
      title: t("accounting.continuity.postings.reprocessConfirmTitle"),
      message: t("accounting.continuity.postings.reprocessConfirmMessage"),
      confirmLabel: t("accounting.continuity.postings.reprocess"),
    });
    if (!confirmed) return;
    setBusyId(requestIds[0]);
    try {
      if (batchMode) {
        const batch = await accountingApi.reprocessPostings(requestIds, reason);
        setResults(batch.items);
      } else {
        const result = await accountingApi.reprocessPosting(
          requestIds[0],
          reason,
        );
        if (result.status === "Posted")
          message.success(t("accounting.continuity.postings.reprocessPosted"));
        else
          message.warning(
            t("accounting.continuity.postings.reprocessFailed", {
              error: result.errorMessage ?? result.errorCode ?? "",
            }),
          );
      }
      setRequestIds(null);
      await fetchPostings();
    } catch (err: unknown) {
      if (!applyServerErrors(err, form.setError, (msg) => message.error(msg)))
        message.error(
          formatApiRequestError(err, {
            generic: t("accounting.continuity.postings.error.reprocess"),
          }),
        );
    } finally {
      setBusyId(null);
    }
  };

  const columns = useMemo<ZHDataTableColumn<AccountingPostingDto>[]>(
    () => [
      ...(canReprocess
        ? [
            {
              key: "selection",
              header: t("accounting.continuity.postings.select"),
              render: (row: AccountingPostingDto) =>
                row.status === "Pending" || row.status === "Failed" ? (
                  <input
                    type="checkbox"
                    aria-label={`${t("accounting.continuity.postings.select")} ${row.id}`}
                    checked={selected.includes(row.id)}
                    disabled={
                      loading ||
                      busyId !== null ||
                      (!selected.includes(row.id) && selected.length >= 100)
                    }
                    onChange={(e) =>
                      setSelected((ids) =>
                        e.target.checked
                          ? [...ids, row.id]
                          : ids.filter((id) => id !== row.id),
                      )
                    }
                  />
                ) : null,
            },
          ]
        : []),
      {
        key: "document",
        header: t("accounting.continuity.postings.column.document"),
        render: (row) => (
          <span>
            <strong>{sourceModuleLabel(t, row.sourceModule)}</strong>{" "}
            {row.sourceDocumentNumber ?? ""}
          </span>
        ),
      },
      {
        key: "fact",
        header: t("accounting.continuity.postings.column.fact"),
        render: (row) => factTypeLabel(t, row.factType),
      },
      {
        key: "entryDate",
        header: t("accounting.continuity.postings.column.entryDate"),
        render: (row) => formatDate(row.entryDate),
      },
      {
        key: "status",
        header: t("accounting.continuity.postings.column.status"),
        align: "center",
        render: (row) => (
          <Badge
            label={statusLabel(row.status)}
            variant={STATUS_VARIANT[row.status]}
          />
        ),
      },
      {
        key: "attempts",
        header: t("accounting.continuity.postings.column.attempts"),
        align: "right",
        cellClassName: "zh-table-cell--num",
        render: (row) => row.attempts,
      },
      {
        key: "lastAttempt",
        header: t("accounting.continuity.postings.column.lastAttempt"),
        render: (row) =>
          row.lastAttemptAtUtc ? formatDateTime(row.lastAttemptAtUtc) : "—",
      },
      {
        key: "error",
        header: t("accounting.continuity.postings.column.error"),
        render: (row) => row.errorMessage ?? "—",
      },
      {
        key: "actions",
        header: t("accounting.continuity.postings.column.actions"),
        align: "right",
        render: (row) => (
          <div className="prd-row-actions">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => void openHistory(row)}
            >
              {t("accounting.continuity.postings.history")}
            </ZHBtn>
            {canReprocess &&
              (row.status === "Pending" || row.status === "Failed") && (
                <ZHBtn
                  size="sm"
                  type="button"
                  disabled={busyId !== null}
                  onClick={() => {
                    form.reset();
                    setBatchMode(false);
                    setRequestIds([row.id]);
                  }}
                >
                  {t("accounting.continuity.postings.reprocess")}
                </ZHBtn>
              )}
          </div>
        ),
      },
    ],
    [
      t,
      statusLabel,
      canReprocess,
      busyId,
      openHistory,
      selected,
      loading,
      form,
    ],
  );

  const attemptColumns: ZHDataTableColumn<AccountingPostingAttemptDto>[] = [
    {
      key: "number",
      header: t("accounting.continuity.postings.attempt.number"),
      align: "right",
      render: (row) => row.attemptNumber,
    },
    {
      key: "reason",
      header: t("accounting.continuity.postings.reason"),
      render: (row) => row.reason ?? "—",
    },
    {
      key: "trigger",
      header: t("accounting.continuity.postings.attempt.trigger"),
      render: (row) =>
        t(`accounting.continuity.postings.trigger.${row.trigger}`),
    },
    {
      key: "finishedAt",
      header: t("accounting.continuity.postings.attempt.finishedAt"),
      render: (row) => formatDateTime(row.finishedAtUtc),
    },
    {
      key: "outcome",
      header: t("accounting.continuity.postings.attempt.outcome"),
      render: (row) => (
        <Badge
          label={statusLabel(row.outcome)}
          variant={STATUS_VARIANT[row.outcome]}
        />
      ),
    },
    {
      key: "error",
      header: t("accounting.continuity.postings.column.error"),
      render: (row) => row.errorMessage ?? "—",
    },
  ];

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.continuity.postings.title")}
      subtitle={t("accounting.continuity.postings.subtitle")}
    >
      <ZHCard title={t("accounting.continuity.postings.filtersTitle")}>
        <ZHFilterBar
          onClear={() => {
            setStatus("");
            setSourceModule("");
          }}
          disabled={loading}
        >
          <div className="zh-filterbar__field">
            <ZHField
              label={t("accounting.continuity.postings.column.status")}
              density="compact"
            >
              <ZhSelect
                value={status}
                disabled={loading}
                onChange={(e) =>
                  setStatus(e.target.value as AccountingPostingStatus | "")
                }
              >
                <option value="">
                  {t("accounting.continuity.postings.filter.allStatuses")}
                </option>
                {STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {statusLabel(s)}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField
              label={t("accounting.continuity.postings.column.document")}
              density="compact"
            >
              <ZhSelect
                value={sourceModule}
                disabled={loading}
                onChange={(e) => setSourceModule(e.target.value)}
              >
                <option value="">
                  {t("accounting.continuity.postings.filter.allModules")}
                </option>
                {SOURCE_MODULES.map((m) => (
                  <option key={m} value={m}>
                    {sourceModuleLabel(t, m)}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => void fetchPostings()}
              disabled={loading}
            >
              {t("common.refresh")}
            </ZHBtn>
          </div>
        </ZHFilterBar>
      </ZHCard>

      <ZHCard>
        {canReprocess && (
          <ZHBtn
            type="button"
            disabled={!selected.length || busyId !== null || loading}
            onClick={() => {
              form.reset();
              setBatchMode(true);
              setRequestIds(selected);
            }}
          >
            {t("accounting.continuity.postings.batch")} ({selected.length}/100)
          </ZHBtn>
        )}
        <ZHDataTable
          columns={columns}
          rows={postings}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage={t("accounting.continuity.postings.empty")}
        />
      </ZHCard>

      <ZHModal
        open={requestIds !== null}
        onClose={() => {
          if (!busyId) setRequestIds(null);
        }}
        title={t("accounting.continuity.postings.reprocessConfirmTitle")}
      >
        <form onSubmit={form.handleSubmit(reprocess)}>
          <ZHField
            label={t("accounting.continuity.postings.reason")}
            error={form.formState.errors.reason?.message}
          >
            <ZhTextarea
              aria-label={t("accounting.continuity.postings.reason")}
              maxLength={500}
              disabled={busyId !== null}
              {...form.register("reason")}
            />
          </ZHField>
          <ZHBtn
            type="submit"
            disabled={
              busyId !== null ||
              form.formState.isSubmitting ||
              !form.watch("reason").trim()
            }
          >
            {t("accounting.continuity.postings.confirm")}
          </ZHBtn>
        </form>
      </ZHModal>
      <ZHModal
        open={results !== null}
        onClose={() => setResults(null)}
        title={t("accounting.continuity.postings.results")}
        size="lg"
      >
        <ZHDataTable
          rows={results ?? []}
          rowKey={(row) => row.id}
          columns={[
            {
              key: "id",
              header: t("accounting.continuity.postings.column.document"),
              render: (row) => row.posting?.sourceDocumentNumber ?? row.id,
            },
            {
              key: "outcome",
              header: t("accounting.continuity.postings.attempt.outcome"),
              render: (row) =>
                t(`accounting.continuity.postings.result.${row.outcome}`),
            },
            {
              key: "status",
              header: t("accounting.continuity.postings.column.status"),
              render: (row) =>
                row.posting ? statusLabel(row.posting.status) : "—",
            },
            {
              key: "error",
              header: t("accounting.continuity.postings.column.error"),
              render: (row) =>
                row.errorMessage ?? row.posting?.errorMessage ?? "—",
            },
          ]}
        />
      </ZHModal>

      <ZHModal
        open={detail !== null}
        onClose={() => setDetail(null)}
        size="lg"
        title={t("accounting.continuity.postings.historyTitle")}
        subtitle={
          detail
            ? `${sourceModuleLabel(t, detail.posting.sourceModule)} ${detail.posting.sourceDocumentNumber ?? ""}`
            : undefined
        }
      >
        <ZHDataTable
          columns={attemptColumns}
          rows={detail?.attemptHistory ?? []}
          rowKey={(row) => row.id}
          emptyMessage={t("accounting.continuity.postings.historyEmpty")}
        />
      </ZHModal>
    </PageShell>
  );
}
