import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell, Badge, type BadgeVariant } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDateInput, ZhNumberInput } from "../../../components/zh/inputs";
import { useI18n } from "../../../i18n/i18n";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import {
  addDaysIso,
  formatDate,
  formatDateTime,
  firstDayOfMonthIso,
  isValidIsoDate,
  todayIso,
} from "../../../lib/formatters/dateFormatters";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountingPeriodClosureReadinessDto,
  type AccountingPeriodDto,
  type AccountingPeriodStatus,
} from "../api/accountingApi";
import { FiscalYearClosingCard } from "./FiscalYearClosingCard";
import { completenessReasonLabel } from "../labels/completenessReasonLabels";
import {
  accountingPeriodSchema,
  type AccountingPeriodFormValues,
} from "../schemas/accountingPeriodSchema";

import "../../../styles/shared/items-catalog.css";

const CREATE_PERMISSION = "accounting.periods.create";
const CLOSE_PERMISSION = "accounting.periods.close";
const PENDING_COSTS_PERMISSION = "inventory.pending-costs.view";

const STATUS_VARIANT: Record<AccountingPeriodStatus, BadgeVariant> = {
  Open: "green",
  Closed: "orange",
  Locked: "gray",
};

// ACCOUNTING-07: el ejercicio es el año calendario (accounting.md §22) — los límites del ejercicio y
// del mes se arman desde los números del período (servidor) y se validan con los helpers oficiales;
// nunca se interpreta una fecha de negocio con Date ni se recorta "YYYY-MM-DD".
const fiscalYearEnd = (fiscalYear: number) => `${fiscalYear}-12-31`;
const pad2 = (n: number) => String(n).padStart(2, "0");

/** Último día del mes natural `month` del ejercicio (períodos mensuales: PeriodNumber = mes). */
function monthEnd(fiscalYear: number, month: number): string | undefined {
  return [31, 30, 29, 28]
    .map((day) => `${fiscalYear}-${pad2(month)}-${day}`)
    .find(isValidIsoDate);
}

/** Siguiente período contiguo al último existente (prellenado de la creación manual). */
function nextContiguous(periods: AccountingPeriodDto[]): Partial<AccountingPeriodFormValues> {
  const last = periods.reduce<AccountingPeriodDto | null>(
    (acc, p) => (acc === null || p.endDate > acc.endDate ? p : acc),
    null,
  );
  if (!last) return { startDate: firstDayOfMonthIso() };
  const startDate = addDaysIso(last.endDate, 1);
  const rollsOver = last.endDate === fiscalYearEnd(last.fiscalYear);
  const fiscalYear = rollsOver ? last.fiscalYear + 1 : last.fiscalYear;
  const periodNumber = rollsOver ? 1 : last.periodNumber + 1;
  const monthly = startDate === `${fiscalYear}-${pad2(periodNumber)}-01`;
  return {
    fiscalYear,
    periodNumber,
    startDate,
    endDate: (monthly && monthEnd(fiscalYear, periodNumber)) || startDate,
  };
}

type ReasonTarget =
  | { to: string; permission?: string }
  | { previousPeriod: true }
  | { explain: true };

/** Destino para resolver cada motivo de bloqueo; los dos últimos solo se explican (sin detalle en A07). */
function reasonTarget(code: string): ReasonTarget {
  if (code === "POSTINGS_UNRESOLVED") return { to: "/accounting/postings" };
  if (code.startsWith("JOURNAL_ENTRIES_") || code === "REVERSALS_INCOMPLETE")
    return { to: "/accounting/journal-entries" };
  if (code === "PREVIOUS_PERIOD_OPEN") return { previousPeriod: true };
  if (code.startsWith("OPENING_")) return { to: "/initial-load" };
  if (code === "SALE_COST_PENDING")
    return { to: "/inventory/pending-costs", permission: PENDING_COSTS_PERMISSION };
  return { explain: true };
}

/**
 * Contabilidad → Configuración → Períodos contables (ACCOUNTING-07). Auditoría de reutilización:
 * revisadas `AccountingOperationalModePage.tsx` (PageShell/ZHCard/ZHDataTable/Badge/ZHPageNotice +
 * formulario RHF+Zod+applyServerErrors) y `AccountingPostingsPage.tsx` (ZHModal + message.confirm
 * por permiso); etiquetas de motivos compartidas con los reportes (`completenessReasonLabels`). Sin
 * componentes nuevos. La elegibilidad de cierre (`canClose`: abierto, terminado según la fecha de la
 * empresa y completo) viene íntegra del backend; aquí solo se presenta.
 */
export function AccountingPeriodsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canCreate = canShow(CREATE_PERMISSION);
  const canClose = canShow(CLOSE_PERMISSION);

  const [periods, setPeriods] = useState<AccountingPeriodDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [createError, setCreateError] = useState("");
  const [review, setReview] = useState<AccountingPeriodDto | null>(null);
  const [readiness, setReadiness] = useState<AccountingPeriodClosureReadinessDto | null>(null);
  const [readinessLoading, setReadinessLoading] = useState(false);

  const today = todayIso();

  const form = useForm<AccountingPeriodFormValues>({
    resolver: zodResolver(accountingPeriodSchema),
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setPeriods(await accountingApi.listPeriods());
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.load") }));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void load();
  }, [load]);

  const loadReadiness = useCallback(
    async (period: AccountingPeriodDto) => {
      setReadinessLoading(true);
      try {
        setReadiness(await accountingApi.getPeriodClosureReadiness(period.id));
      } catch (err: unknown) {
        setReadiness(null);
        message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.readiness") }));
      } finally {
        setReadinessLoading(false);
      }
    },
    [t],
  );

  const openReview = (period: AccountingPeriodDto) => {
    setReview(period);
    setReadiness(null);
    void loadReadiness(period);
  };

  // Fecha Company (todayIso) frente a los rangos del servidor: comparación de strings ISO.
  const currentPeriod = periods.find((p) => p.startDate <= today && p.endDate >= today) ?? null;
  const hasPeriodToday = currentPeriod !== null;
  const nextFiscalYear = periods.length ? Math.max(...periods.map((p) => p.fiscalYear)) + 1 : null;
  // Desde el 1-dic (31-dic − 30 días) del ejercicio en curso, si el siguiente no tiene períodos.
  const upcomingFiscalYear = currentPeriod ? currentPeriod.fiscalYear + 1 : null;
  const missingNextYear =
    currentPeriod !== null &&
    today >= addDaysIso(fiscalYearEnd(currentPeriod.fiscalYear), -30) &&
    !periods.some((p) => p.fiscalYear === upcomingFiscalYear);

  const previousOpen = useMemo(
    () =>
      review
        ? periods
            .filter((p) => p.status === "Open" && p.endDate < review.startDate)
            .sort((a, b) => a.startDate.localeCompare(b.startDate))[0] ?? null
        : null,
    [periods, review],
  );

  const generateYear = async () => {
    if (nextFiscalYear === null) return;
    const confirmed = await message.confirm({
      title: t("accounting.periods.generate.confirmTitle", { year: nextFiscalYear ?? "" }),
      message: t("accounting.periods.generate.confirmMessage", { year: nextFiscalYear ?? "" }),
      confirmLabel: t("accounting.periods.generate.action", { year: nextFiscalYear ?? "" }),
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      const result = await accountingApi.generateFiscalYear(nextFiscalYear);
      message.success(
        t("accounting.periods.generate.done", { year: result.fiscalYear, count: result.createdCount }),
      );
      await load();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.generate") }));
    } finally {
      setBusy(false);
    }
  };

  const openCreate = () => {
    setCreateError("");
    form.reset(nextContiguous(periods));
    setCreateOpen(true);
  };

  const submitCreate = form.handleSubmit(async (values) => {
    setCreateError("");
    setBusy(true);
    try {
      await accountingApi.createPeriod(values);
      message.success(t("accounting.periods.create.done"));
      setCreateOpen(false);
      await load();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, form.setError, (msg) => setCreateError(msg));
      if (!applied)
        setCreateError(formatApiRequestError(err, { generic: t("accounting.periods.error.create") }));
    } finally {
      setBusy(false);
    }
  });

  const closePeriod = async () => {
    if (!review || !readiness?.canClose) return;
    const confirmed = await message.confirm({
      title: t("accounting.periods.close.confirmTitle"),
      message: t("accounting.periods.close.confirmMessage", {
        period: `${review.fiscalYear}-${review.periodNumber}`,
        from: formatDate(review.startDate),
        to: formatDate(review.endDate),
      }),
      confirmLabel: t("accounting.periods.close.action"),
      variant: "danger",
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      await accountingApi.closePeriod(review.id);
      message.success(t("accounting.periods.close.done"));
      setReview(null);
      await load();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.close") }));
      await loadReadiness(review);
    } finally {
      setBusy(false);
    }
  };

  const lockPeriod = async (period: AccountingPeriodDto) => {
    const confirmed = await message.confirm({
      title: t("accounting.periods.lock.confirmTitle"),
      message: t("accounting.periods.lock.confirmMessage", {
        period: `${period.fiscalYear}-${period.periodNumber}`,
      }),
      confirmLabel: t("accounting.periods.lock.action"),
      variant: "danger",
    });
    if (!confirmed) return;
    setBusy(true);
    try {
      await accountingApi.lockPeriod(period.id);
      message.success(t("accounting.periods.lock.done"));
      await load();
    } catch (err: unknown) {
      message.error(formatApiRequestError(err, { generic: t("accounting.periods.error.lock") }));
    } finally {
      setBusy(false);
    }
  };

  const columns: ZHDataTableColumn<AccountingPeriodDto>[] = [
    { key: "year", header: t("accounting.periods.column.year"), render: (row) => row.fiscalYear },
    { key: "number", header: t("accounting.periods.column.number"), render: (row) => row.periodNumber },
    { key: "from", header: t("accounting.periods.column.from"), render: (row) => formatDate(row.startDate) },
    { key: "to", header: t("accounting.periods.column.to"), render: (row) => formatDate(row.endDate) },
    {
      key: "status",
      header: t("accounting.periods.column.status"),
      render: (row) => (
        <Badge label={t(`accounting.periods.status.${row.status}`)} variant={STATUS_VARIANT[row.status]} />
      ),
    },
    {
      key: "closedAt",
      header: t("accounting.periods.column.closedAt"),
      render: (row) => (row.closedAtUtc ? formatDateTime(row.closedAtUtc) : "—"),
    },
    {
      key: "closedBy",
      header: t("accounting.periods.column.closedBy"),
      render: (row) => row.closedByName ?? (row.closedBy ? t("accounting.periods.unknownUser") : "—"),
    },
    {
      key: "actions",
      header: t("accounting.periods.column.actions"),
      render: (row) => (
        <div className="zh-form-actions-row">
          {row.status === "Open" && (
            <ZHBtn size="sm" type="button" onClick={() => openReview(row)}>
              {t("accounting.periods.review.action")}
            </ZHBtn>
          )}
          {row.status === "Closed" && canClose && (
            <ZHBtn size="sm" type="button" disabled={busy} onClick={() => void lockPeriod(row)}>
              {t("accounting.periods.lock.action")}
            </ZHBtn>
          )}
        </div>
      ),
    },
  ];

  const completeness = readiness?.completeness ?? null;

  return (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={t("accounting.periods.title")}
      subtitle={t("accounting.periods.subtitle")}
    >
      {!loading && !hasPeriodToday && (
        <ZHPageNotice variant="warning" message={t("accounting.periods.notice.noPeriodToday")} />
      )}
      {!loading && missingNextYear && (
        <ZHPageNotice
          variant="warning"
          message={t("accounting.periods.notice.nextYearMissing", { year: upcomingFiscalYear ?? "" })}
        />
      )}

      <ZHCard
        title={t("accounting.periods.listTitle")}
        actions={
          canCreate && (
            <div className="zh-form-actions-row">
              <ZHBtn
                type="button"
                variant="primary"
                disabled={busy || loading || nextFiscalYear === null}
                onClick={() => void generateYear()}
              >
                {t("accounting.periods.generate.action", { year: nextFiscalYear ?? "" })}
              </ZHBtn>
              <ZHBtn type="button" disabled={busy || loading} onClick={openCreate}>
                {t("accounting.periods.create.action")}
              </ZHBtn>
            </div>
          )
        }
      >
        <ZHDataTable
          columns={columns}
          rows={periods}
          rowKey={(row) => row.id}
          loading={loading}
          emptyMessage={t("accounting.periods.empty")}
        />
      </ZHCard>

      <FiscalYearClosingCard canGenerate={canClose} refreshKey={periods} />

      <ZHModal
        open={createOpen}
        onClose={() => {
          if (!busy) setCreateOpen(false);
        }}
        title={t("accounting.periods.create.title")}
      >
        <form onSubmit={(e) => void submitCreate(e)}>
          {createError && <ZHPageNotice variant="error" message={createError} />}
          <ZHGrid cols={2}>
            <ZHField
              label={t("accounting.periods.field.year")}
              required
              fieldError={form.formState.errors.fiscalYear?.message}
            >
              <ZhNumberInput positiveOnly disabled={busy} {...form.register("fiscalYear", { valueAsNumber: true })} />
            </ZHField>
            <ZHField
              label={t("accounting.periods.field.number")}
              required
              fieldError={form.formState.errors.periodNumber?.message}
            >
              <ZhNumberInput positiveOnly disabled={busy} {...form.register("periodNumber", { valueAsNumber: true })} />
            </ZHField>
            <ZHField
              label={t("accounting.periods.field.from")}
              required
              fieldError={form.formState.errors.startDate?.message}
            >
              <ZhDateInput className="zh-input" disabled={busy} {...form.register("startDate")} />
            </ZHField>
            <ZHField
              label={t("accounting.periods.field.to")}
              required
              fieldError={form.formState.errors.endDate?.message}
            >
              <ZhDateInput className="zh-input" disabled={busy} {...form.register("endDate")} />
            </ZHField>
          </ZHGrid>
          <p>{t("accounting.periods.create.hint")}</p>
          <div className="zh-form-actions-row">
            <ZHBtn type="submit" variant="primary" disabled={busy}>
              {t("accounting.periods.create.submit")}
            </ZHBtn>
          </div>
        </form>
      </ZHModal>

      <ZHModal
        open={review !== null}
        onClose={() => {
          if (!busy) setReview(null);
        }}
        title={
          review
            ? t("accounting.periods.review.title", { period: `${review.fiscalYear}-${review.periodNumber}` })
            : ""
        }
        size="lg"
      >
        {readinessLoading && <p>{t("accounting.periods.review.loading")}</p>}
        {readiness && !readiness.isEligible && (
          <ZHPageNotice
            variant="info"
            message={t("accounting.periods.review.notEligible", {
              status: t(`accounting.periods.status.${readiness.status}`),
            })}
          />
        )}
        {readiness?.isEligible && (
          <>
            <p>
              {t("accounting.periods.review.asOf", {
                asOf: completeness ? formatDate(completeness.asOf) : "—",
                today: formatDate(readiness.companyToday),
              })}
            </p>
            {!readiness.hasEnded && review && (
              <ZHPageNotice
                variant="warning"
                message={t("accounting.periods.review.notEnded", { to: formatDate(review.endDate) })}
              />
            )}
            {completeness?.isComplete ? (
              <ZHPageNotice variant="success" message={t("accounting.periods.review.complete")} />
            ) : (
              <ul>
                {completeness?.reasons.map((reason) => {
                  const target = reasonTarget(reason.code);
                  return (
                    <li key={reason.code}>
                      <strong>{completenessReasonLabel(reason.code)}</strong>: {reason.count}{" "}
                      {"to" in target && (!target.permission || canShow(target.permission)) && (
                        <Link to={target.to}>{t("accounting.periods.review.resolve")}</Link>
                      )}
                      {"previousPeriod" in target && previousOpen && (
                        <ZHBtn size="xs" type="button" variant="ghost" onClick={() => openReview(previousOpen)}>
                          {t("accounting.periods.review.previous", {
                            period: `${previousOpen.fiscalYear}-${previousOpen.periodNumber}`,
                          })}
                        </ZHBtn>
                      )}
                      {"explain" in target && (
                        <span> — {t(`accounting.periods.review.explain.${reason.code}`)}</span>
                      )}
                    </li>
                  );
                })}
              </ul>
            )}
          </>
        )}
        <div className="zh-form-actions-row">
          <ZHBtn
            type="button"
            disabled={readinessLoading || busy || !review}
            onClick={() => review && void loadReadiness(review)}
          >
            {t("accounting.periods.review.refresh")}
          </ZHBtn>
          {canClose && (
            <ZHBtn
              type="button"
              variant="destructive"
              disabled={busy || readinessLoading || !readiness?.canClose}
              onClick={() => void closePeriod()}
            >
              {t("accounting.periods.close.action")}
            </ZHBtn>
          )}
        </div>
      </ZHModal>
    </PageShell>
  );
}
