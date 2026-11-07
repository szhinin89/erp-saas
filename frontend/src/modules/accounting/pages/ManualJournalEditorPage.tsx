import Decimal from "decimal.js";
import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { Controller, useFieldArray, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoadingState, PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions, ZHGrid } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDateInput, ZhDecimalInput, ZhSearchSelect, ZhTextInput } from "../../../components/zh/inputs";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import { formatMoneyWithSymbol, parseDecimal } from "../../../lib/sanitizers";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { accountingApi, type AccountDto } from "../api/accountingApi";
import { manualJournalSchema, type ManualJournalValues } from "../schemas/manualJournalSchema";

// Importes vacíos en pantalla: `parseDecimal("")` = 0, mismo significado numérico que el "0" anterior.
const emptyLine = { accountId: "", description: "", debit: "", credit: "" };

const amountText = (value: number) => (value === 0 ? "" : String(value));
const accountKey = (a: AccountDto) => a.id;
const accountLabel = (a: AccountDto) => `${a.code} — ${a.name}`;
const accountSearchText = (a: AccountDto) => `${a.code} ${a.name}`;

/**
 * ACCOUNTING-08: alta y edición de asientos manuales (`/accounting/journal-entries/new` y
 * `/:id/edit`). ACCOUNTING-JOURNAL-UX-01: reutiliza el patrón de tabla editable de
 * `OpeningJournalPublishDrawer` (excepción documentada «tabla editable con useFieldArray» — necesita
 * `<tfoot>` y celdas RHF, que `ZHDataTable` no expone); cuentas con `ZhSearchSelect` (catálogo grande,
 * frontend.md) vía `Controller`; totales con `decimal.js` solo para presentación; acciones con
 * `ZHFormActions`. El aviso de cuadre es informativo: el borrador puede guardarse descuadrado y el
 * backend sigue siendo la autoridad al publicar (`JournalEntry.Post` → `EnsureBalanced`).
 */
export function ManualJournalEditorPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canSave = canShow(id ? "accounting.manual.edit" : "accounting.manual.create");
  const canPost = canShow("accounting.manual.post");
  const accountingDecimals = usePrecisionDecimals("accounting");
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
  const accountsById = useMemo(() => new Map(accounts.map((a) => [a.id, a])), [accounts]);

  const totalDebit = lines.reduce((n, l) => n.plus(parseDecimal(l.debit ?? "")), new Decimal(0));
  const totalCredit = lines.reduce((n, l) => n.plus(parseDecimal(l.credit ?? "")), new Decimal(0));
  const difference = totalDebit.minus(totalCredit);
  const hasAmounts = !totalDebit.isZero() || !totalCredit.isZero();
  const balanced = difference.isZero();

  const backTo = id ? `/accounting/journal-entries/${id}` : "/accounting/journal-entries";
  const locked = loading || isSubmitting || !canSave || (!!id && version === null);
  // Misma condición que PostingAccountGuard (backend, autoridad final): activa y de movimiento.
  // Solo se anticipa al publicar; el borrador admite cualquier cuenta existente (accounting.md §11).
  const isPostable = (a: AccountDto) => a.isActive && a.allowsPosting;
  const [blockedAccounts, setBlockedAccounts] = useState<string[]>([]);
  const selectedNotPostable = (accountId: string | undefined) => {
    const account = accountId ? accountsById.get(accountId) : undefined;
    return !!account && !isPostable(account);
  };

  useEffect(() => {
    let active = true;
    void Promise.all([accountingApi.listAccounts(), id ? accountingApi.getJournalEntryById(id) : Promise.resolve(null)])
      .then(([list, entry]) => {
        if (!active) return;
        setAccounts(list);
        if (entry) {
          if (!entry.manual || entry.status !== "Draft") throw new Error(t("accounting.journal.editor.onlyDraft"));
          setVersion(entry.manual.version);
          reset({ entryDate: entry.entryDate, concept: entry.description, reference: entry.manual.reference ?? "",
            lines: entry.lines.map(l => ({ accountId: l.accountId, description: l.description ?? "", debit: amountText(l.debit), credit: amountText(l.credit) })) });
        }
      }).catch((err: unknown) => {
        if (active) setError(formatApiRequestError(err, { generic: t("accounting.journal.editor.loadError") }));
      }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [id, reset, t]);

  async function save(values: ManualJournalValues, publish: boolean) {
    setBlockedAccounts([]);
    if (publish) {
      // Validación previa: con una cuenta conocida como no imputable la publicación fallaría con
      // POSTING_ACCOUNT_INVALID — se avisa antes de confirmar y sin llamar al API.
      const blocked = values.lines
        .map((line, index) => ({ index, account: accountsById.get(line.accountId) }))
        .filter((l): l is { index: number; account: AccountDto } => !!l.account && !isPostable(l.account));
      if (blocked.length > 0) {
        for (const { index } of blocked)
          setFieldError(`lines.${index}.accountId`, { type: "manual", message: t("accounting.journal.editor.notPostableLineError") });
        setBlockedAccounts([...new Set(blocked.map(({ account }) => accountLabel(account)))]);
        return;
      }
    }
    if (publish && !(await message.confirm({
      title: t("accounting.journal.editor.confirmTitle"),
      message: t("accounting.journal.editor.confirmMessage"),
      confirmLabel: t("accounting.journal.editor.confirmLabel"),
    }))) return;
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
        catch (err: unknown) {
          // El borrador ya es durable aunque la publicación falle (o se pierda la respuesta): mismo
          // destino que antes (el detalle muestra los intentos), ahora con el motivo visible.
          const reason = formatApiRequestError(err, { generic: "" });
          message.error(`${t("accounting.journal.editor.publishFailed")} ${reason}`.trim());
          navigate(`/accounting/journal-entries/${result.id}`);
          return;
        }
      }
      navigate(`/accounting/journal-entries/${result.id}`);
    } catch (err: unknown) {
      applyServerErrors(err, setFieldError);
      setError(formatApiRequestError(err, { generic: t("accounting.journal.editor.saveError") }));
    }
  }

  const title = t(id ? "accounting.journal.editor.titleEdit" : "accounting.journal.editor.titleNew");
  const shell = (children: ReactNode) => (
    <PageShell
      kicker={t("app.nav.group.accounting")}
      title={title}
      subtitle={t("accounting.journal.editor.subtitle")}
      action={<ZHBtn type="button" variant="secondary" icon="chevron_left" onClick={() => navigate(backTo)}>{t("accounting.journal.editor.back")}</ZHBtn>}
    >
      {children}
    </PageShell>
  );

  if (loading) return shell(<ZHCard><LoadingState /></ZHCard>);

  return shell(<>
    {error && <ZHPageNotice variant="error" message={error} />}
    {!canSave && <ZHPageNotice variant="error" message={t("accounting.journal.editor.noPermission")} />}
    <form onSubmit={handleSubmit(values => save(values, false))}>
        <ZHCard className="zh-mb-16" title={t("accounting.journal.editor.dataTitle")}>
          <ZHGrid cols={3}>
            <ZHField label={t("accounting.journal.editor.entryDate")} error={errors.entryDate?.message}>
              <ZhDateInput disabled={locked} {...register("entryDate")} />
            </ZHField>
            <ZHField label={t("accounting.journal.editor.reference")} error={errors.reference?.message}>
              <ZhTextInput maxLength={500} disabled={locked} {...register("reference")} />
            </ZHField>
            <ZHField label={t("accounting.journal.editor.concept")} required error={errors.concept?.message}>
              <ZhTextInput maxLength={500} disabled={locked} {...register("concept")} />
            </ZHField>
          </ZHGrid>
          <div className="zh-mt-16">
            <ZHPageNotice variant="info" message={t("accounting.journal.editor.publishRules")} />
          </div>
        </ZHCard>

        <ZHCard
          className="zh-mb-16"
          title={t("accounting.journal.lines.title")}
          actions={
            <ZHBtn type="button" variant="secondary" size="sm" icon="add" disabled={locked} onClick={() => append({ ...emptyLine })}>
              {t("accounting.journal.editor.addLine")}
            </ZHBtn>
          }
        >
          {blockedAccounts.length > 0 && (
            <ZHPageNotice
              variant="error"
              message={t("accounting.journal.editor.publishBlocked")}
              detail={t("accounting.journal.editor.publishBlockedDetail", { accounts: blockedAccounts.join(", ") })}
            />
          )}
          <div className="table-scroll">
            <table className="table table--compact">
              <thead>
                <tr>
                  <th>{t("accounting.journal.lines.account")}</th>
                  <th>{t("accounting.journal.lines.description")}</th>
                  <th className="zh-table-cell--num">{t("accounting.journal.lines.debit")}</th>
                  <th className="zh-table-cell--num">{t("accounting.journal.lines.credit")}</th>
                  <th className="zh-table-cell--actions">{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {fields.map((field, index) => {
                  const n = { n: index + 1 };
                  return (
                    <tr key={field.id}>
                      <td>
                        <ZHField
                          density="compact"
                          fieldError={errors.lines?.[index]?.accountId?.message}
                          hint={selectedNotPostable(lines[index]?.accountId) ? t("accounting.journal.editor.notPostableHint") : undefined}
                          hintType="warning"
                        >
                          <Controller
                            control={control}
                            name={`lines.${index}.accountId`}
                            render={({ field: accountField }) => (
                              <ZhSearchSelect<AccountDto>
                                aria-label={t("accounting.journal.editor.lineAccount", n)}
                                options={accounts}
                                value={accountsById.get(accountField.value) ?? null}
                                onChange={(account) => accountField.onChange(account?.id ?? "")}
                                getOptionKey={accountKey}
                                getOptionLabel={accountLabel}
                                getOptionSearchText={accountSearchText}
                                getOptionDescription={(a) =>
                                  !a.isActive || !a.allowsPosting ? t("accounting.journal.editor.notPostable") : undefined
                                }
                                placeholder={t("accounting.journal.editor.accountPlaceholder")}
                                emptyText={t("accounting.journal.editor.noAccounts")}
                                disabled={locked}
                                // La tabla vive en .table-scroll/.card (overflow): lista en portal anclado.
                                portal
                              />
                            )}
                          />
                        </ZHField>
                      </td>
                      <td>
                        <ZhTextInput
                          aria-label={t("accounting.journal.editor.lineDescription", n)}
                          maxLength={500}
                          density="compact"
                          disabled={locked}
                          {...register(`lines.${index}.description`)}
                        />
                      </td>
                      <td className="zh-table-cell--num">
                        <ZhDecimalInput
                          aria-label={t("accounting.journal.editor.lineDebit", n)}
                          precision="accounting"
                          positiveOnly
                          density="compact"
                          disabled={locked}
                          {...register(`lines.${index}.debit`)}
                        />
                      </td>
                      <td className="zh-table-cell--num">
                        <ZhDecimalInput
                          aria-label={t("accounting.journal.editor.lineCredit", n)}
                          precision="accounting"
                          positiveOnly
                          density="compact"
                          disabled={locked}
                          {...register(`lines.${index}.credit`)}
                        />
                      </td>
                      <td className="zh-table-cell--actions">
                        <ZHIconButton
                          icon="delete"
                          variant="danger"
                          title={t("accounting.journal.editor.removeLine")}
                          onClick={() => remove(index)}
                          disabled={locked}
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr>
                  <td colSpan={2}>{t("accounting.journal.lines.totals")}</td>
                  <td className="zh-table-cell--num" aria-label={t("accounting.journal.lines.debit")}>
                    <ZHMoneyValue precision="accounting" value={totalDebit.toNumber()} emphasis="total" />
                  </td>
                  <td className="zh-table-cell--num" aria-label={t("accounting.journal.lines.credit")}>
                    <ZHMoneyValue precision="accounting" value={totalCredit.toNumber()} emphasis="total" />
                  </td>
                  <td />
                </tr>
                <tr>
                  <td colSpan={2}>{t("accounting.journal.editor.difference")}</td>
                  <td colSpan={2} className="zh-table-cell--num" aria-label={t("accounting.journal.editor.difference")}>
                    <ZHMoneyValue precision="accounting" value={difference.toNumber()} />
                  </td>
                  <td />
                </tr>
              </tfoot>
            </table>
          </div>
          {hasAmounts && (
            <div className="zh-mt-16">
              <ZHPageNotice
                variant={balanced ? "success" : "warning"}
                message={t(balanced ? "accounting.journal.balance.balanced" : "accounting.journal.balance.unbalanced")}
                detail={
                  balanced
                    ? t("accounting.journal.balance.balancedDetail", { difference: formatMoneyWithSymbol(0, accountingDecimals) })
                    : `${t("accounting.journal.balance.unbalancedDetail", {
                        difference: formatMoneyWithSymbol(difference.abs().toNumber(), accountingDecimals),
                      })}. ${t("accounting.journal.editor.unbalancedHint")}`
                }
              />
            </div>
          )}
        </ZHCard>
      <ZHFormActions
        onCancel={() => navigate(backTo)}
        draftButtonType="submit"
        disableDraft={locked}
        hideSave={!canPost}
        onSave={handleSubmit(values => save(values, true))}
        disableSave={locked}
        labels={{
          draft: t("accounting.journal.editor.saveDraft"),
          save: t("accounting.journal.editor.saveAndPublish"),
        }}
      />
    </form>
  </>);
}
