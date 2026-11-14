import { Badge } from "../../../components/PageShell";
import { useState } from "react";
import { ZHFieldHelp } from "../../../components/zh/help";
import { HELP_KEYS } from "../../../help";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";
import { useI18n } from "../../../i18n/i18n";
import type { SriVatRateLookup } from "../../items/facades/sriLookupFacade";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import {
  calculateExpenseLineTotals,
  newExpenseDraftLine,
  type ExpenseAccountRef,
  type ExpenseDraftLineScales,
  type VatRateByCode,
} from "../utils/expenseDocumentDraftModel";
import { ExpenseSubcategorySelector } from "./ExpenseSubcategorySelector";

export interface ExpenseDraftLineState {
  key: string;
  expenseSubcategoryId: string;
  description: string;
  quantity: string;
  unitPrice: string;
  discountValue: string;
  vatCode: string;
  notes: string;
}

export type ExpenseLineFieldErrors = Record<
  string,
  Partial<Record<keyof ExpenseDraftLineState, string>>
>;

interface Props {
  lines: ExpenseDraftLineState[];
  tree: ExpenseCategoryTreeNodeDto[];
  accountsById: Map<string, ExpenseAccountRef>;
  vatRates: SriVatRateLookup[];
  vatRateByCode: VatRateByCode;
  disabled?: boolean;
  errors?: ExpenseLineFieldErrors;
  /**
   * EXPENSE-UI-02 — el documento nace de un comprobante XML recibido: la clasificación contable de
   * cada línea (subcategoría) no viene del XML y se marca como manual hasta que se elija.
   * Solo presentación; no cambia la importación.
   */
  fromXml?: boolean;
  onChange: (lines: ExpenseDraftLineState[]) => void;
}

/**
 * Observación opcional de la línea: plegada mientras esté vacía (compacta la altura) y abierta
 * automáticamente cuando ya tiene texto, para no ocultar nunca un dato existente.
 */
function ExpenseLineNotes({
  value,
  disabled,
  onChange,
}: {
  value: string;
  disabled?: boolean;
  onChange: (notes: string) => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  if (!open && value.trim() === "") {
    return (
      <div className="exp-doc-line__notes-toggle">
        <ZHBtn
          type="button"
          variant="ghost"
          size="xs"
          icon="add"
          disabled={disabled}
          onClick={() => setOpen(true)}
        >
          {t("expenses.form.field.lineNotesAdd")}
        </ZHBtn>
      </div>
    );
  }
  return (
    <ZHField label={t("expenses.form.field.lineNotes")} density="compact">
      <ZhTextarea
        density="compact"
        value={value}
        rows={1}
        maxLength={300}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
      />
    </ZHField>
  );
}

export function ExpenseDocumentLinesEditor({
  lines,
  tree,
  accountsById,
  vatRates,
  vatRateByCode,
  disabled,
  errors,
  fromXml,
  onChange,
}: Props) {
  const { t } = useI18n();
  // Escalas de una línea nueva (04G): mismas semánticas que sus inputs.
  const draftScales: ExpenseDraftLineScales = {
    unitPriceDecimals: usePrecisionDecimals("purchaseUnitPrice"),
    moneyDecimals: usePrecisionDecimals("money"),
  };

  const updateLine = (
    key: string,
    patch: Partial<ExpenseDraftLineState>,
  ) => onChange(lines.map((line) => (line.key === key ? { ...line, ...patch } : line)));

  const removeLine = (key: string) => {
    if (lines.length === 1) return;
    onChange(lines.filter((line) => line.key !== key));
  };

  return (
    <section className="exp-doc-section" aria-label={t("expenses.form.lines.aria")}>
      <div className="exp-doc-section__heading exp-doc-section__heading--actions">
        <div className="exp-doc-section__title-row">
          <h2>{t("expenses.form.lines.title")}</h2>
          <ZHFieldHelp helpKey={HELP_KEYS.EXPENSES_DOCUMENT_CONCEPTS} />
        </div>
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={disabled}
          onClick={() => onChange([...lines, newExpenseDraftLine(draftScales)])}
        >
          <span className="material-symbols-outlined" aria-hidden="true">
            add
          </span>
          {t("expenses.form.lines.add")}
        </ZHBtn>
      </div>

      <div className="exp-doc-lines">
        {lines.map((line, index) => {
          const lineTotals = calculateExpenseLineTotals(line, vatRateByCode);
          const lineErrors = errors?.[line.key] ?? {};
          const currentCode = line.vatCode.trim();
          const hasCurrentCodeInCatalog = vatRates.some(
            (rate) => rate.code === currentCode,
          );
          return (
            <div className="exp-doc-line" key={line.key}>
              <div className="exp-doc-line__top">
                <div className="exp-doc-line__title">
                  <span className="exp-doc-line__number">
                    {t("expenses.form.lines.number", { number: index + 1 })}
                  </span>
                  {fromXml && !line.expenseSubcategoryId && (
                    <Badge
                      label={t("expenses.form.origin.manual")}
                      variant="orange"
                      size="md"
                    />
                  )}
                </div>
                <div className="exp-doc-line__totals">
                  <span>
                    {t("expenses.form.lines.base")}{" "}
                    <ZHMoneyValue
                      value={lineTotals.taxableBase}
                      precision="money"
                      currencySymbol=""
                    />
                  </span>
                  <span>
                    {t("expenses.form.lines.vat")}{" "}
                    <ZHMoneyValue
                      value={lineTotals.vat}
                      precision="tax"
                      currencySymbol=""
                    />
                  </span>
                  <span>
                    {t("expenses.form.lines.total")}{" "}
                    <ZHMoneyValue
                      value={lineTotals.total}
                      precision="money"
                      currencySymbol=""
                      emphasis="strong"
                    />
                  </span>
                </div>
                <ZHIconButton
                  icon="delete"
                  title={t("expenses.form.lines.remove")}
                  ariaLabel={t("expenses.form.lines.removeAria", { number: index + 1 })}
                  variant="ghost"
                  disabled={disabled || lines.length === 1}
                  onClick={() => removeLine(line.key)}
                />
              </div>

              <ExpenseSubcategorySelector
                tree={tree}
                accountsById={accountsById}
                value={line.expenseSubcategoryId}
                disabled={disabled}
                error={lineErrors.expenseSubcategoryId}
                onChange={(expenseSubcategoryId) =>
                  updateLine(line.key, { expenseSubcategoryId })
                }
              />

              <div className="exp-doc-line__grid exp-doc-line__grid--item">
                <ZHField
                  className="exp-doc-line__description"
                  label={t("expenses.form.field.description")}
                  density="compact"
                  fieldError={lineErrors.description}
                >
                  <ZhTextInput
                    density="compact"
                    placeholder={t("expenses.form.field.descriptionPlaceholder")}
                    value={line.description}
                    disabled={disabled}
                    maxLength={300}
                    onChange={(event) =>
                      updateLine(line.key, { description: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label={t("expenses.form.field.quantity")}
                  required
                  density="compact"
                  fieldError={lineErrors.quantity}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    precision="quantity"
                    value={line.quantity}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { quantity: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label={t("expenses.form.field.unitPrice")}
                  required
                  density="compact"
                  fieldError={lineErrors.unitPrice}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    precision="purchaseUnitPrice"
                    value={line.unitPrice}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { unitPrice: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label={t("expenses.form.field.discount")}
                  density="compact"
                  fieldError={lineErrors.discountValue}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    precision="money"
                    value={line.discountValue}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { discountValue: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label={t("expenses.form.field.vatCode")}
                  required
                  density="compact"
                  fieldError={lineErrors.vatCode}
                >
                  <ZhSelect
                    density="compact"
                    value={line.vatCode}
                    disabled={disabled}
                    onChange={(event) =>
                      updateLine(line.key, { vatCode: event.target.value })
                    }
                  >
                    {vatRates.map((rate) => (
                      <option key={rate.code} value={rate.code}>
                        {rate.code} - {rate.name}
                      </option>
                    ))}
                    {currentCode && !hasCurrentCodeInCatalog && (
                      <option value={currentCode}>
                        {t("expenses.form.field.vatCodeNotCurrent", { code: currentCode })}
                      </option>
                    )}
                  </ZhSelect>
                </ZHField>
              </div>

              <ExpenseLineNotes
                value={line.notes}
                disabled={disabled}
                onChange={(notes) => updateLine(line.key, { notes })}
              />
            </div>
          );
        })}
      </div>
    </section>
  );
}
