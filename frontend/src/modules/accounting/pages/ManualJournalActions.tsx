import { useState } from "react";
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
import { accountingApi, FISCAL_YEAR_CLOSING_FACT, type JournalEntryDetailDto } from "../api/accountingApi";
import { manualJournalReasonSchema } from "../schemas/manualJournalSchema";

/** Extends the existing detail, reusing AccountingPeriodsPage's confirmation/modal pattern. */
export function ManualJournalActions({ entry, onChanged }: { entry: JournalEntryDetailDto; onChanged: () => void }) {
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [attempts, setAttempts] = useState(entry.manual?.attempts ?? []);
  const [mode, setMode] = useState<"discard" | "reverse" | null>(null);
  const { register, handleSubmit, reset, formState: { errors } } = useForm({
    resolver: zodResolver(manualJournalReasonSchema), defaultValues: { reason: "", effectiveDate: todayIso() },
  });
  if (!entry.manual) return null;
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
    <ZHCard title={yearEnd ? "Asiento de cierre anual" : "Asiento manual"}>
      {error && <ZHPageNotice variant="error" message={error} />}
      {yearEnd && draft && <ZHPageNotice variant="info" message="Generado desde Contabilidad › Períodos con los saldos contabilizados del ejercicio. Si los saldos cambian antes de publicarlo, la publicación se rechaza: descártelo y genere uno nuevo." />}
      <p>Creado por: {manual.createdBy} · {formatDateTime(entry.createdAt)}</p>
      <p>Referencia: {manual.reference || "—"}</p>
      {manual.discardedAtUtc && <p>Descartado por {manual.discardedBy} · {formatDateTime(manual.discardedAtUtc)} · {manual.discardReason}</p>}
      {draft && !yearEnd && canShow("accounting.manual.edit") && <ZHBtn variant="ghost" disabled={busy} onClick={() => navigate(`/accounting/journal-entries/${entry.id}/edit`)}>Editar borrador</ZHBtn>}
      {draft && canShow("accounting.manual.post") && <ZHBtn disabled={busy} onClick={() => void post()}>Publicar</ZHBtn>}
      {draft && canShow("accounting.manual.discard") && <ZHBtn variant="ghost" disabled={busy} onClick={() => { reset({ reason: "", effectiveDate: todayIso() }); setMode("discard"); }}>Descartar</ZHBtn>}
      {/* reverseJournalEntryId viene de la relación OriginalJournalEntryId del reverso: reversado → sin acción. */}
      {entry.status === "Posted" && !entry.reverseJournalEntryId && canShow("accounting.manual.reverse") &&
        <ZHBtn variant="ghost" disabled={busy} onClick={() => { reset({ reason: "", effectiveDate: yearEnd ? entry.entryDate : todayIso() }); setMode("reverse"); }}>Reversar</ZHBtn>}
    </ZHCard>
    <ZHCard title="Intentos de publicación">
      <ZHDataTable rows={attempts} rowKey={row => row.id} emptyMessage="Sin intentos registrados."
        columns={[
          { key: "date", header: "Fecha", render: row => formatDateTime(row.attemptedAtUtc) },
          { key: "user", header: "Usuario", render: row => row.attemptedBy },
          { key: "result", header: "Resultado", render: row => row.success ? "Publicado" : "Rechazado" },
          { key: "error", header: "Detalle", render: row => row.errorMessage ?? "—" },
        ]} />
    </ZHCard>
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
