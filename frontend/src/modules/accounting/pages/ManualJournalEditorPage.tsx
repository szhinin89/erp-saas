import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDateInput, ZhTextInput, ZhSelect, ZhDecimalInput } from "../../../components/zh/inputs";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { parseDecimal } from "../../../lib/sanitizers";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import { manualJournalSchema, type ManualJournalValues } from "../schemas/manualJournalSchema";

const emptyLine = { accountId: "", description: "", debit: "0", credit: "0" };

/** ACCOUNTING-08: extends JournalEntries. Reuses AccountingPeriodsPage's RHF/Zod/fields and
 * OpeningJournalPublishDrawer's editable-cell table (ZHDataTable has no field-array editor).
 * No new visual primitives. Decimal inputs and totals use the canonical accounting precision. */
export function ManualJournalEditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canSave = canShow(id ? "accounting.manual.edit" : "accounting.manual.create");
  const [version, setVersion] = useState<number | null>(null);
  const [accounts, setAccounts] = useState<AccountDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const createKey = useRef(crypto.randomUUID());
  const { register, control, reset, handleSubmit, setError: setFieldError, formState: { errors, isSubmitting } } =
    useForm<ManualJournalValues>({ resolver: zodResolver(manualJournalSchema), defaultValues: {
      entryDate: todayIso(), concept: "", reference: "", lines: [{ ...emptyLine }, { ...emptyLine }],
    } });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  const lines = useWatch({ control, name: "lines" }) ?? [];
  const debit = lines.reduce((n, l) => n + parseDecimal(l.debit ?? ""), 0);
  const credit = lines.reduce((n, l) => n + parseDecimal(l.credit ?? ""), 0);

  useEffect(() => {
    let active = true;
    void Promise.all([accountingApi.listAccounts(), id ? accountingApi.getJournalEntryById(id) : Promise.resolve(null)])
      .then(([list, entry]) => {
        if (!active) return;
        setAccounts(list);
        if (entry) {
          if (!entry.manual || entry.status !== "Draft") throw new Error("Solo puede editarse un manual Draft.");
          setVersion(entry.manual.version);
          reset({ entryDate: entry.entryDate, concept: entry.description, reference: entry.manual.reference ?? "",
            lines: entry.lines.map(l => ({ accountId: l.accountId, description: l.description ?? "", debit: String(l.debit), credit: String(l.credit) })) });
        }
      }).catch((err: unknown) => {
        if (active) setError(formatApiRequestError(err, { generic: "No se pudo cargar el editor." }));
      }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id, reset]);

  async function save(values: ManualJournalValues, publish: boolean) {
    setError(null);
    try {
      const payload = { entryDate: values.entryDate, concept: values.concept, reference: values.reference || null,
        lines: values.lines.map(l => ({ accountId: l.accountId, description: l.description || null,
          debit: parseDecimal(l.debit), credit: parseDecimal(l.credit) })) };
      const result = id
        ? await accountingApi.editManualJournal(id, version!, payload)
        : await accountingApi.createManualJournal({ ...payload, idempotencyKey: createKey.current });
      if (publish) {
        try { await accountingApi.postManualJournal(result.id, result.version, crypto.randomUUID()); }
        catch {
          // The draft is durable even when publication failed or the response was lost.
          navigate(`/accounting/journal-entries/${result.id}`);
          return;
        }
      }
      navigate(`/accounting/journal-entries/${result.id}`);
    } catch (err: unknown) {
      applyServerErrors(err, setFieldError);
      setError(formatApiRequestError(err, { generic: "No se pudo guardar. Si hay conflicto, recargue el asiento antes de continuar." }));
    }
  }

  return <PageShell kicker="Contabilidad" title={id ? "Editar asiento manual" : "Nuevo asiento manual"}
    action={<ZHBtn variant="ghost" onClick={() => navigate(id ? `/accounting/journal-entries/${id}` : "/accounting/journal-entries")}>Volver</ZHBtn>}>
    {error && <ZHPageNotice variant="error" message={error} />}
    {!canSave && <ZHPageNotice variant="error" message="No tiene permiso para guardar este asiento." />}
    <form onSubmit={handleSubmit(values => save(values, false))}>
      <fieldset disabled={loading || isSubmitting || !canSave || (!!id && version === null)}>
        <ZHCard title="Datos del asiento">
          <ZHGrid cols={2}>
            <ZHField label="Fecha contable" error={errors.entryDate?.message}><ZhDateInput {...register("entryDate")} /></ZHField>
            <ZHField label="Referencia" error={errors.reference?.message}><ZhTextInput maxLength={500} {...register("reference")} /></ZHField>
          </ZHGrid>
          <ZHField label="Concepto" error={errors.concept?.message}><ZhTextInput maxLength={500} {...register("concept")} /></ZHField>
          <ZHPageNotice variant="info" message="El borrador puede quedar desbalanceado o con fecha futura. Al publicar se exige fecha no futura, Accounting Enabled y período Open." />
        </ZHCard>
        <ZHCard title="Líneas">
          {/* Editable field-array cells and live totals: same documented table exception as the opening editor. */}
          <div className="table-scroll"><table className="table table--compact table--neutral"><thead><tr>
            <th>Cuenta</th><th>Descripción</th><th>Debe</th><th>Haber</th><th>Acciones</th>
          </tr></thead><tbody>{fields.map((field, index) => <tr key={field.id}>
            <td><ZhSelect aria-label={`Cuenta línea ${index + 1}`} {...register(`lines.${index}.accountId`)}>
              <option value="">Seleccione cuenta</option>
              {accounts.map(a => <option key={a.id} value={a.id}>{a.code} — {a.name}{!a.isActive || !a.allowsPosting ? " (no habilitada para publicar)" : ""}</option>)}
            </ZhSelect>{errors.lines?.[index]?.accountId?.message}</td>
            <td><ZhTextInput aria-label={`Descripción línea ${index + 1}`} maxLength={500} {...register(`lines.${index}.description`)} /></td>
            <td><ZhDecimalInput aria-label={`Debe línea ${index + 1}`} precision="accounting" {...register(`lines.${index}.debit`)} /></td>
            <td><ZhDecimalInput aria-label={`Haber línea ${index + 1}`} precision="accounting" {...register(`lines.${index}.credit`)} /></td>
            <td><ZHBtn type="button" variant="ghost" onClick={() => remove(index)}>Quitar</ZHBtn></td>
          </tr>)}</tbody></table></div>
          <ZHBtn type="button" variant="ghost" onClick={() => append({ ...emptyLine })}>Agregar línea</ZHBtn>
          <p>Debe: <ZHMoneyValue value={debit} precision="accounting" /> · Haber: <ZHMoneyValue value={credit} precision="accounting" /> · Diferencia: <ZHMoneyValue value={debit - credit} precision="accounting" /></p>
        </ZHCard>
        <ZHBtn type="submit" disabled={isSubmitting}>Guardar borrador</ZHBtn>
        {canShow("accounting.manual.post") && <ZHBtn type="button" disabled={isSubmitting}
          onClick={handleSubmit(values => save(values, true))}>Guardar y publicar</ZHBtn>}
      </fieldset>
    </form>
  </PageShell>;
}
