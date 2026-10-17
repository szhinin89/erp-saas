import { useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { message } from "../../../lib/messages";
import {
  accountingApi,
  type AccountDto,
  type PostingFactCatalogItemDto,
  type PostingRuleDto,
} from "../api/accountingApi";
import {
  amountKindLabel,
  factTypeLabel,
  lineDirectionLabel,
  sourceModuleLabel,
  type TFunction,
} from "../labels/accountingLabels";
import { postingRuleSchema, type PostingRuleFormValues } from "../schemas/postingRuleSchema";

export type PostingRuleEditorState =
  | { mode: "create"; fact: PostingFactCatalogItemDto }
  | { mode: "edit"; fact: PostingFactCatalogItemDto; rule: PostingRuleDto };

function initialLines(state: PostingRuleEditorState): PostingRuleFormValues["lines"] {
  if (state.mode === "edit") {
    return state.rule.lines
      .slice()
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((l) => ({ accountId: l.accountId, nature: l.nature as "Debit" | "Credit", amountKind: l.amountKind }));
  }
  const kind = state.fact.allowedAmountKinds[0] ?? "";
  const sides: ("Debit" | "Credit")[] = [];
  if (!state.fact.dynamicDebit) sides.push("Debit");
  if (!state.fact.dynamicCredit) sides.push("Credit");
  return sides.map((nature) => ({ accountId: "", nature, amountKind: kind }));
}

/**
 * ACCOUNTING-11 — editor de líneas de una regla (crear faltante / reemplazar líneas). Reutiliza el
 * patrón RHF+Zod+useFieldArray+applyServerErrors de `ManualJournalEditorPage` y su tabla de celdas
 * editables (excepción documentada: ZHDataTable no ofrece editor de field-array), dentro de
 * `ZHModal`. Los tipos de monto ofrecidos son solo los que el catálogo permite para el hecho; la
 * forma final la valida el backend.
 */
export function PostingRuleEditorModal({
  state,
  accounts,
  onClose,
  onSaved,
  t,
}: {
  state: PostingRuleEditorState;
  accounts: AccountDto[];
  onClose: () => void;
  onSaved: () => void;
  t: TFunction;
}) {
  const [error, setError] = useState<string | null>(null);
  const {
    register,
    control,
    handleSubmit,
    setError: setFieldError,
    formState: { errors, isSubmitting },
  } = useForm<PostingRuleFormValues>({
    resolver: zodResolver(postingRuleSchema),
    defaultValues: { lines: initialLines(state) },
  });
  const { fields, append, remove } = useFieldArray({ control, name: "lines" });
  const postable = accounts.filter((a) => a.isActive && a.allowsPosting);
  const { fact } = state;

  async function save(values: PostingRuleFormValues) {
    setError(null);
    try {
      if (state.mode === "create") {
        await accountingApi.createPostingRule({
          sourceModule: fact.sourceModule,
          factType: fact.factType,
          lines: values.lines,
        });
      } else {
        await accountingApi.updatePostingRule(state.rule.id, values.lines);
      }
      message.success(t("accounting.postingRules.editor.saved"));
      onSaved();
    } catch (err: unknown) {
      applyServerErrors(err, setFieldError);
      setError(formatApiRequestError(err, { generic: t("accounting.postingRules.editor.error") }));
    }
  }

  return (
    <ZHModal
      open
      size="lg"
      onClose={() => {
        if (!isSubmitting) onClose();
      }}
      title={`${sourceModuleLabel(t, fact.sourceModule)} / ${factTypeLabel(t, fact.factType)}`}
      subtitle={
        state.mode === "create"
          ? t("accounting.postingRules.editor.createSubtitle")
          : t("accounting.postingRules.editor.editSubtitle")
      }
    >
      <form onSubmit={handleSubmit(save)}>
        {error && <ZHPageNotice variant="error" message={error} />}
        <ZHPageNotice variant="info" message={t("accounting.postingRules.editor.futureOnly")} />
        {fact.dynamicDebit && fact.allowedAmountKinds.length > 0 && (
          <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.debit")} />
        )}
        {fact.dynamicCredit && fact.allowedAmountKinds.length > 0 && (
          <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.credit")} />
        )}
        {fact.allowedAmountKinds.length === 0 ? (
          <ZHPageNotice variant="info" message={t("accounting.postingRules.dynamic.enabling")} />
        ) : (
          <>
            {/* Celdas editables de field-array: misma excepción de tabla documentada que ManualJournalEditorPage. */}
            <div className="table-scroll">
              <table className="table table--compact table--neutral">
                <thead>
                  <tr>
                    <th>{t("accounting.postingRules.column.account")}</th>
                    <th>{t("accounting.postingRules.column.direction")}</th>
                    <th>{t("accounting.postingRules.column.amountKind")}</th>
                    <th>{t("common.actions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {fields.map((field, index) => (
                    <tr key={field.id}>
                      <td>
                        <ZhSelect
                          aria-label={t("accounting.postingRules.column.account")}
                          {...register(`lines.${index}.accountId`)}
                        >
                          <option value="">{t("accounting.postingRules.editor.selectAccount")}</option>
                          {postable.map((a) => (
                            <option key={a.id} value={a.id}>
                              {a.code} — {a.name}
                            </option>
                          ))}
                        </ZhSelect>
                        {errors.lines?.[index]?.accountId?.message}
                      </td>
                      <td>
                        <ZhSelect
                          aria-label={t("accounting.postingRules.column.direction")}
                          {...register(`lines.${index}.nature`)}
                        >
                          <option value="Debit">{lineDirectionLabel(t, "Debit")}</option>
                          <option value="Credit">{lineDirectionLabel(t, "Credit")}</option>
                        </ZhSelect>
                      </td>
                      <td>
                        <ZhSelect
                          aria-label={t("accounting.postingRules.column.amountKind")}
                          {...register(`lines.${index}.amountKind`)}
                        >
                          {fact.allowedAmountKinds.map((k) => (
                            <option key={k} value={k}>
                              {amountKindLabel(t, k)}
                            </option>
                          ))}
                        </ZhSelect>
                        {errors.lines?.[index]?.amountKind?.message}
                      </td>
                      <td>
                        <ZHBtn type="button" variant="ghost" size="sm" onClick={() => remove(index)}>
                          {t("accounting.postingRules.editor.removeLine")}
                        </ZHBtn>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              onClick={() =>
                append({ accountId: "", nature: "Debit", amountKind: fact.allowedAmountKinds[0] ?? "" })
              }
            >
              {t("accounting.postingRules.editor.addLine")}
            </ZHBtn>
          </>
        )}
        <div className="zh-form-actions-row zh-form-actions-row--end">
          <ZHBtn type="button" variant="ghost" disabled={isSubmitting} onClick={onClose}>
            {t("common.cancel")}
          </ZHBtn>
          <ZHBtn type="submit" disabled={isSubmitting}>
            {state.mode === "create"
              ? t("accounting.postingRules.editor.create")
              : t("accounting.postingRules.editor.save")}
          </ZHBtn>
        </div>
      </form>
    </ZHModal>
  );
}
