import { useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHDataTable } from "../../../components/zh/ZHDataTable";
import { ZhDateInput, ZhTextInput } from "../../../components/zh/inputs";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatDateTime, todayIso } from "../../../lib/formatters/dateFormatters";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { useI18n } from "../../../i18n/i18n";
import { accountingApi, FISCAL_YEAR_CLOSING_FACT, type JournalEntryDetailDto } from "../api/accountingApi";
import { manualJournalReasonSchema } from "../schemas/manualJournalSchema";

/**
 * Extends the existing detail, reusing AccountingPeriodsPage's confirmation/modal pattern.
 * ACCOUNTING-JOURNAL-UX-01: `children` es el contenido del asiento (resumen y líneas) — se ubica
 * entre la card de acciones/metadatos y los intentos de publicación, que así quedan después de las
 * líneas sin separar el estado `attempts` (refrescado localmente tras un fallo de publicación).
 */
export function ManualJournalActions({ entry, onChanged, children }: { entry: JournalEntryDetailDto; onChanged: () => void; children?: ReactNode }) {
  const navigate = useNavigate();
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attempts, setAttempts] = useState(entry.manual?.attempts ?? []);
  const [mode, setMode] = useState<"discard" | "reverse" | null>(null);
  const { register, handleSubmit, reset, formState: { errors } } = useForm({
    resolver: zodResolver(manualJournalReasonSchema), defaultValues: { reason: "", effectiveDate: todayIso() },
  });
  if (!entry.manual) return <>{children}</>;
  const manual = entry.manual;
  const draft = entry.status === "Draft";
  // ACCOUNTING-12D: el cierre anual usa este mismo flujo, pero sus líneas las calcula el sistema (no se edita).
  const yearEnd = entry.sourceEventType === FISCAL_YEAR_CLOSING_FACT;

  async function post() {
    if (!await message.confirm({ title: "Publicar asiento", message: "El asiento recibirá número definitivo y sus importes serán inmutables.", confirmLabel: "Publicar" })) return;
    setBusy(true);
    setError(null);
    try {
      await accountingApi.postManualJournal(entry.id, manual.version, crypto.randomUUID());
      onChanged();
    } catch (err: unknown) {
      setError(formatApiRequestError(err, { generic: "No se pudo publicar. Consulte los intentos y recargue el asiento antes de continuar." }));
      // Keep the failure visible; refreshing the whole parent would unmount this notice.
      try {
        const refreshed = await accountingApi.getJournalEntryById(entry.id);
        setAttempts(refreshed.manual?.attempts ?? []);
        if (refreshed.status !== "Draft" || refreshed.manual?.version !== manual.version) onChanged();
      } catch { /* Preserve the publication error if the follow-up read also failed. */ }
    } finally { setBusy(false); }
  }

  const submitReason = handleSubmit(async values => {
    setBusy(true);
    setError(null);
    try {
      if (mode === "discard") {
        await accountingApi.discardManualJournal(entry.id, manual.version, values.reason);
        setMode(null);
        onChanged();
      } else {
        const result = await accountingApi.reverseManualJournal(entry.id, values.effectiveDate, values.reason);
        setMode(null);
        navigate(`/accounting/journal-entries/${result.id}`);
      }
    } catch (err: unknown) {
      setError(formatApiRequestError(err, { generic: "No se pudo completar la acción. Recargue si el asiento cambió." }));
    } finally { setBusy(false); }
  });

  return <>
    <ZHCard
      className="zh-mb-16"
      title={t(yearEnd ? "accounting.journal.operation.fiscalYearClosing" : "accounting.journal.operation.manual")}
      actions={<div className="zh-form-actions-row zh-form-actions-row--flush">
        {draft && !yearEnd && canShow("accounting.manual.edit") && <ZHBtn variant="ghost" size="sm" disabled={busy} onClick={() => navigate(`/accounting/journal-entries/${entry.id}/edit`)}>Editar borrador</ZHBtn>}
        {draft && canShow("accounting.manual.discard") && <ZHBtn variant="ghost" size="sm" disabled={busy} onClick={() => { reset({ reason: "", effectiveDate: todayIso() }); setMode("discard"); }}>Descartar</ZHBtn>}
        {/* reverseJournalEntryId viene de la relación OriginalJournalEntryId del reverso: reversado → sin acción. */}
        {entry.status === "Posted" && !entry.reverseJournalEntryId && canShow("accounting.manual.reverse") &&
          <ZHBtn variant="ghost" size="sm" disabled={busy} onClick={() => { reset({ reason: "", effectiveDate: yearEnd ? entry.entryDate : todayIso() }); setMode("reverse"); }}>Reversar</ZHBtn>}
        {draft && canShow("accounting.manual.post") && <ZHBtn size="sm" disabled={busy} onClick={() => void post()}>Publicar</ZHBtn>}
      </div>}
    >
      {error && <ZHPageNotice variant="error" message={error} />}
      {yearEnd && draft && <ZHPageNotice variant="info" message="Generado desde Contabilidad › Períodos con los saldos contabilizados del ejercicio. Si los saldos cambian antes de publicarlo, la publicación se rechaza: descártelo y genere uno nuevo." />}
      <div className="zh-summary-grid">
        <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.createdBy")}</span><span className="zh-summary-grid__value">{manual.createdBy}</span></div>
        <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.createdAt")}</span><span className="zh-summary-grid__value">{formatDateTime(entry.createdAt)}</span></div>
        <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.reference")}</span><span className="zh-summary-grid__value">{manual.reference || "—"}</span></div>
        {manual.discardedAtUtc && <>
          <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.discardedBy")}</span><span className="zh-summary-grid__value">{manual.discardedBy}</span></div>
          <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.discardedAt")}</span><span className="zh-summary-grid__value">{formatDateTime(manual.discardedAtUtc)}</span></div>
          <div><span className="zh-summary-grid__label">{t("accounting.journal.manual.discardReason")}</span><span className="zh-summary-grid__value">{manual.discardReason}</span></div>
        </>}
      </div>
    </ZHCard>
    {children}
    {attempts.length > 0 && <ZHCard title={t("accounting.journal.manual.attempts")}>
      <ZHDataTable rows={attempts} rowKey={row => row.id} emptyMessage="Sin intentos registrados."
        columns={[
          { key: "date", header: "Fecha", render: row => formatDateTime(row.attemptedAtUtc) },
          { key: "user", header: "Usuario", render: row => row.attemptedBy },
          { key: "result", header: "Resultado", render: row => row.success ? "Publicado" : "Rechazado" },
          { key: "error", header: "Detalle", render: row => row.errorMessage ?? "—" },
        ]} />
    </ZHCard>}
    <ZHModal open={mode !== null} onClose={() => { if (!busy) setMode(null); }} title={mode === "discard" ? "Descartar borrador" : "Reversar asiento"}>
      <form onSubmit={submitReason}>
        {error && <ZHPageNotice variant="error" message={error} />}
        {/* ACCOUNTING-FINAL-FIX: el cierre anual solo se reversa con su fecha original (31-dic); el backend lo impone. */}
        {mode === "reverse" && <ZHField label="Fecha efectiva" error={errors.effectiveDate?.message}><ZhDateInput readOnly={yearEnd} {...register("effectiveDate")} /></ZHField>}
        {mode === "reverse" && yearEnd && <ZHPageNotice variant="info" message="El cierre anual se reversa con su fecha original, dentro del ejercicio, mientras su último período esté abierto. Después genere un cierre nuevo desde Períodos." />}
        <ZHField label="Motivo" error={errors.reason?.message}><ZhTextInput maxLength={500} {...register("reason")} /></ZHField>
        <ZHPageNotice variant="info" message={mode === "discard" ? "El descarte es irreversible; conserva el borrador como histórico." : "Se creará un asiento espejo en un período Open. El original permanece intacto."} />
        <ZHBtn type="submit" disabled={busy}>Confirmar</ZHBtn>
      </form>
    </ZHModal>
  </>;
}
