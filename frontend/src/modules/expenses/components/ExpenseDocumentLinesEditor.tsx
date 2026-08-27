import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextarea } from "../../../components/zh/inputs/ZhTextarea";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import type { AccountDto } from "../../accounting/api/accountingApi";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import {
  calculateExpenseLineTotals,
  newExpenseDraftLine,
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
  accountsById: Map<string, AccountDto>;
  disabled?: boolean;
  errors?: ExpenseLineFieldErrors;
  onChange: (lines: ExpenseDraftLineState[]) => void;
}

export function ExpenseDocumentLinesEditor({
  lines,
  tree,
  accountsById,
  disabled,
  errors,
  onChange,
}: Props) {
  const decimals = getDecimalConfig();

  const updateLine = (
    key: string,
    patch: Partial<ExpenseDraftLineState>,
  ) => onChange(lines.map((line) => (line.key === key ? { ...line, ...patch } : line)));

  const removeLine = (key: string) => {
    if (lines.length === 1) return;
    onChange(lines.filter((line) => line.key !== key));
  };

  return (
    <section className="exp-doc-section" aria-label="Lineas del gasto">
      <div className="exp-doc-section__heading exp-doc-section__heading--actions">
        <div>
          <h2>Detalle</h2>
          <p>Seleccione una subcategoria para asignar la cuenta contable destino.</p>
        </div>
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={disabled}
          onClick={() => onChange([...lines, newExpenseDraftLine()])}
        >
          <span className="material-symbols-outlined" aria-hidden="true">
            add
          </span>
          Agregar linea
        </ZHBtn>
      </div>

      <div className="exp-doc-lines">
        {lines.map((line, index) => {
          const lineTotals = calculateExpenseLineTotals(line);
          const lineErrors = errors?.[line.key] ?? {};
          return (
            <div className="exp-doc-line" key={line.key}>
              <div className="exp-doc-line__top">
                <span className="exp-doc-line__number">Linea {index + 1}</span>
                <ZHIconButton
                  icon="delete"
                  title="Quitar linea"
                  ariaLabel={`Quitar linea ${index + 1}`}
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

              <div className="exp-doc-line__grid">
                <ZHField
                  label="Descripcion"
                  required
                  density="compact"
                  fieldError={lineErrors.description}
                >
                  <ZhTextInput
                    density="compact"
                    value={line.description}
                    disabled={disabled}
                    maxLength={300}
                    onChange={(event) =>
                      updateLine(line.key, { description: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label="Cantidad"
                  required
                  density="compact"
                  fieldError={lineErrors.quantity}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    decimals={decimals.quantity}
                    value={line.quantity}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { quantity: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label="Valor unitario"
                  required
                  density="compact"
                  fieldError={lineErrors.unitPrice}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    decimals={decimals.purchaseUnitPrice}
                    value={line.unitPrice}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { unitPrice: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label="Descuento"
                  density="compact"
                  fieldError={lineErrors.discountValue}
                >
                  <ZhDecimalInput
                    density="compact"
                    positiveOnly
                    decimals={decimals.totalAmount}
                    value={line.discountValue}
                    disabled={disabled}
                    className="exp-doc-number-input"
                    onChange={(event) =>
                      updateLine(line.key, { discountValue: event.target.value })
                    }
                  />
                </ZHField>

                <ZHField
                  label="Codigo IVA"
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
                    <option value="0">0 - IVA 0%</option>
                    <option value="2">2 - IVA vigente</option>
                    <option value="10">10 - IVA vigente</option>
                    <option value="20">20 - IVA 5%</option>
                    <option value="6">6 - No objeto</option>
                    <option value="7">7 - Exento</option>
                  </ZhSelect>
                </ZHField>
              </div>

              <ZHField label="Observacion de linea" density="compact">
                <ZhTextarea
                  density="compact"
                  value={line.notes}
                  rows={2}
                  maxLength={300}
                  disabled={disabled}
                  onChange={(event) =>
                    updateLine(line.key, { notes: event.target.value })
                  }
                />
              </ZHField>

              <div className="exp-doc-line__totals">
                <span>
                  Base{" "}
                  <ZHMoneyValue
                    value={lineTotals.taxableBase}
                    decimals={decimals.totalAmount}
                    currencySymbol=""
                  />
                </span>
                <span>
                  IVA{" "}
                  <ZHMoneyValue
                    value={lineTotals.vat}
                    decimals={decimals.totalAmount}
                    currencySymbol=""
                  />
                </span>
                <span>
                  Total{" "}
                  <ZHMoneyValue
                    value={lineTotals.total}
                    decimals={decimals.totalAmount}
                    currencySymbol=""
                    emphasis="strong"
                  />
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
