import Decimal from "decimal.js";
import { ManualJournalActions } from "./ManualJournalActions";
import { useEffect, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { PageShell, Badge, LoadingState } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { formatDate, formatDateTime } from "../../../lib/formatters/dateFormatters";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import {
  factTypeDescription,
  friendlyDescription,
  journalExtraDescription,
  journalOperation,
  journalStatusBadge,
} from "../labels/accountingLabels";
import { accountingApi, type JournalEntryDetailDto } from "../api/accountingApi";

/** Par label/valor apilado de `.zh-summary-grid` (zh-ui.css §31), con Material Symbol opcional. */
function SummaryItem({ label, icon, children }: { label: string; icon?: string; children: ReactNode }) {
  return (
    <div>
      <span className="zh-summary-grid__label">
        {icon && <span className="material-symbols-outlined zh-icon-sm" aria-hidden="true">{icon}</span>} {label}
      </span>
      <span className="zh-summary-grid__value">{children}</span>
    </div>
  );
}

/**
 * Detalle de un asiento contable (ACCOUNTING-LEDGER-VISIBILITY-01) — solo lectura.
 * ACCOUNTING-JOURNAL-UX-01: filas de `zh-grid` (patrón oficial): estado + explicación, «Datos del
 * asiento» + «Documento de origen» en dos bloques horizontales (una columna en pantallas pequeñas)
 * y, a continuación, los movimientos contables con su estado de cuadre. El cuadre lo decide
 * `isBalanced` (servidor), en un único aviso con la diferencia.
 * La tabla de movimientos es `<table>` nativa (`table table--compact`) porque necesita `<tfoot>` de
 * totales, que `ZHDataTable` no expone (excepción documentada en frontend.md).
 * ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C: solo links contextuales (listado, asiento relacionado,
 * documento origen).
 */
export function JournalEntryDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useI18n();
  const accountingDecimals = usePrecisionDecimals("accounting");

  const [entry, setEntry] = useState<JournalEntryDetailDto | null>(null);
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    setLoading(true);
    accountingApi
      .getJournalEntryById(id)
      .then((dto) => {
        if (!cancelled) setEntry(dto);
      })
      .catch((err: unknown) => {
        message.error(formatApiRequestError(err, { generic: t("accounting.journal.detail.loadError") }));
        navigate("/accounting/journal-entries");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [id, navigate, revision, t]);

  const kicker = t("app.nav.group.accounting");

  if (loading) {
    return (
      <PageShell kicker={kicker} title={t("accounting.journal.detail.titleGeneric")}>
        <ZHCard>
          <LoadingState />
        </ZHCard>
      </PageShell>
    );
  }

  if (!entry) {
    return (
      <PageShell kicker={kicker} title={t("accounting.journal.detail.titleGeneric")}>
        <ZHPageNotice variant="error" message={t("accounting.journal.detail.notFound")} />
      </PageShell>
    );
  }

  const status = journalStatusBadge(t, entry.status);
  const operation = journalOperation(t, entry);
  const explanation = factTypeDescription(t, entry.sourceEventType);
  // Descripción solo si aporta algo distinto de la operación (misma regla que el listado).
  const extraDescription = journalExtraDescription(t, entry);
  const unbalanced = !entry.isBalanced;
  // isBalanced es la autoridad: cuadrado ⇒ diferencia 0; solo se calcula cuando no cuadra.
  const difference = formatMoneyWithSymbol(
    unbalanced ? new Decimal(entry.totalDebit).minus(entry.totalCredit).abs().toNumber() : 0,
    accountingDecimals,
  );
  const showSourceLink = !entry.manual && !!entry.sourceDocumentNumber && !!entry.sourceRoute;
  const relatedId = entry.originalJournalEntryId ?? entry.reverseJournalEntryId;

  const dataCard = (
    <ZHCard title={t("accounting.journal.detail.summary")}>
      <div className="zh-summary-grid">
        <SummaryItem icon="description" label={t("accounting.journal.detail.type")}>{operation.label}</SummaryItem>
        <SummaryItem icon="calendar_today" label={t("accounting.journal.detail.date")}>{formatDate(entry.entryDate)}</SummaryItem>
        <SummaryItem icon="calendar_month" label={t("accounting.journal.detail.fiscalYear")}>{entry.fiscalYear}</SummaryItem>
        <SummaryItem icon="schedule" label={t("accounting.journal.detail.postedAt")}>
          {entry.postedAtUtc ? formatDateTime(entry.postedAtUtc) : "—"}
        </SummaryItem>
      </div>
      {extraDescription && (
        <div className="zh-summary-grid zh-mt-16">
          <SummaryItem icon="notes" label={t("accounting.journal.detail.description")}>
            {extraDescription}
          </SummaryItem>
        </div>
      )}
    </ZHCard>
  );

  const sourceCard = !entry.manual && (
    <ZHCard title={t("accounting.journal.source.title")}>
      {entry.sourceDocumentNumber ? (
        <div className="zh-summary-grid">
          <SummaryItem icon="article" label={t("accounting.journal.source.type")}>{entry.sourceDocumentType ?? "—"}</SummaryItem>
          <SummaryItem icon="tag" label={t("accounting.journal.source.number")}>
            <span className="zh-code-value zh-code-value--document">{entry.sourceDocumentNumber}</span>
          </SummaryItem>
          <SummaryItem icon="person" label={t("accounting.journal.source.party")}>{entry.sourcePartyName ?? "—"}</SummaryItem>
          <SummaryItem icon="check" label={t("accounting.journal.source.status")}>
            {entry.sourceStatus ? <Badge label={entry.sourceStatus} variant="neutral" /> : "—"}
          </SummaryItem>
        </div>
      ) : (
        <>
          <ZHPageNotice variant="info" message={t("accounting.journal.source.unresolved")} />
          <div className="zh-summary-grid">
            <SummaryItem label={t("accounting.journal.source.technicalModule")}>{entry.sourceModule}</SummaryItem>
            <SummaryItem label={t("accounting.journal.source.technicalFact")}>{entry.sourceEventType}</SummaryItem>
            <SummaryItem label={t("accounting.journal.source.technicalId")}>
              <span className="zh-code-value">{entry.sourceEventId}</span>
            </SummaryItem>
          </div>
        </>
      )}
    </ZHCard>
  );

  const content = (
    <>
      <div className={`zh-grid ${explanation ? "zh-grid--2" : "zh-grid--1"} zh-mb-16`}>
        <div>
          <ZHPageNotice variant={status.variant} icon={status.icon} message={status.label} detail={status.detail} />
        </div>
        {explanation && (
          <div>
            <ZHPageNotice variant="info" message={t("accounting.journal.explanationTitle")} detail={explanation} />
          </div>
        )}
      </div>

      <div className={`zh-grid ${sourceCard ? "zh-grid--2" : "zh-grid--1"} zh-mb-16`}>
        {dataCard}
        {sourceCard}
      </div>

      {relatedId && (
        <ZHCard
          className="zh-mb-16"
          title={t("accounting.journal.reversal.title")}
          actions={
            <div className="zh-form-actions-row zh-form-actions-row--flush">
              {entry.originalJournalEntryId ? (
                <Badge label={t("accounting.journal.reversal.isReversal")} variant="info" />
              ) : (
                <Badge label={t("accounting.journal.reversal.hasReversal")} variant="error" />
              )}
              <ZHBtn
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => navigate(`/accounting/journal-entries/${relatedId}`)}
              >
                {t("accounting.journal.reversal.viewRelated")}
              </ZHBtn>
            </div>
          }
        >
          <div className="zh-summary-grid">
            {entry.originalJournalEntryId && (
              <SummaryItem label={t("accounting.journal.reversal.original")}>
                {entry.originalJournalEntryNumber ?? "—"}
                {entry.originalJournalEntryDate ? ` — ${formatDate(entry.originalJournalEntryDate)}` : ""}
              </SummaryItem>
            )}
            {entry.reverseJournalEntryId && (
              <>
                <SummaryItem label={t("accounting.journal.reversal.reverse")}>
                  {entry.reverseJournalEntryNumber ?? "—"}
                  {entry.reverseJournalEntryDate ? ` — ${formatDate(entry.reverseJournalEntryDate)}` : ""}
                </SummaryItem>
                <SummaryItem label={t("accounting.journal.reversal.reversedAt")}>
                  {entry.reversedAtUtc ? formatDateTime(entry.reversedAtUtc) : "—"}
                </SummaryItem>
              </>
            )}
            {entry.reverseReason && (
              <SummaryItem label={t("accounting.journal.reversal.reason")}>{entry.reverseReason}</SummaryItem>
            )}
          </div>
        </ZHCard>
      )}

      <ZHCard className="zh-mb-16" title={t("accounting.journal.lines.title")}>
        <div className="table-scroll">
          <table className="table table--compact">
            <thead>
              <tr>
                <th>{t("accounting.journal.lines.account")}</th>
                <th>{t("accounting.journal.lines.description")}</th>
                <th className="zh-table-cell--num">{t("accounting.journal.lines.debit")}</th>
                <th className="zh-table-cell--num">{t("accounting.journal.lines.credit")}</th>
              </tr>
            </thead>
            <tbody>
              {entry.lines.map((l) => (
                <tr key={l.id}>
                  <td>
                    <span className="zh-code-value zh-code-value--document">{l.accountCode}</span> — {l.accountName}
                  </td>
                  <td>{l.displayDescription ?? friendlyDescription(t, l.description)}</td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={l.debit > 0 ? l.debit : null} />
                  </td>
                  <td className="zh-table-cell--num">
                    <ZHMoneyValue precision="accounting" value={l.credit > 0 ? l.credit : null} />
                  </td>
                </tr>
              ))}
              {entry.lines.length === 0 && (
                <tr>
                  <td colSpan={4} className="zh-table-empty">
                    {t("accounting.journal.lines.empty")}
                  </td>
                </tr>
              )}
            </tbody>
            <tfoot>
              <tr>
                <td colSpan={2}>{t("accounting.journal.lines.totals")}</td>
                <td className="zh-table-cell--num">
                  <ZHMoneyValue precision="accounting" value={entry.totalDebit} emphasis="total" />
                </td>
                <td className="zh-table-cell--num">
                  <ZHMoneyValue precision="accounting" value={entry.totalCredit} emphasis="total" />
                </td>
              </tr>
            </tfoot>
          </table>
        </div>
        {entry.status !== "Discarded" && (
          <div className="zh-mt-16">
            <ZHPageNotice
              variant={unbalanced ? "error" : "success"}
              message={t(unbalanced ? "accounting.journal.balance.unbalanced" : "accounting.journal.balance.balanced")}
              detail={t(
                unbalanced ? "accounting.journal.balance.unbalancedDetail" : "accounting.journal.balance.balancedDetail",
                { difference },
              )}
            />
          </div>
        )}
      </ZHCard>
    </>
  );

  return (
    <PageShell
      kicker={kicker}
      title={
        entry.entryNumber != null
          ? t("accounting.journal.detail.title", { number: entry.entryNumber })
          : t("accounting.journal.detail.titleUnnumbered")
      }
      subtitle={`${operation.label} · ${formatDate(entry.entryDate)}`}
      action={
        <div className="zh-form-actions-row zh-form-actions-row--flush">
          <ZHBtn type="button" variant="secondary" icon="chevron_left" onClick={() => navigate("/accounting/journal-entries")}>
            {t("accounting.journal.detail.back")}
          </ZHBtn>
          {showSourceLink && (
            <ZHBtn type="button" variant="secondary" icon="description" onClick={() => navigate(entry.sourceRoute!)}>
              {t("accounting.journal.detail.viewSource")}
            </ZHBtn>
          )}
        </div>
      }
    >
      {entry.manual ? (
        <ManualJournalActions entry={entry} onChanged={() => setRevision((n) => n + 1)}>
          {content}
        </ManualJournalActions>
      ) : (
        content
      )}
    </PageShell>
  );
}
