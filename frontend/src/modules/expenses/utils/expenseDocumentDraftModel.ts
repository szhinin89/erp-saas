import Decimal from "decimal.js";
import { getPrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import { formatDecimalDisplay, normalizeOptionalCode } from "../../../lib/sanitizers";
import type { SupplierPickerRow } from "../../masterData/facades/supplierPickerFacade";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type {
  CreateExpenseDraftPayload,
  ExpenseDocumentDetailDto,
} from "../api/expenseDocumentService";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";
import { fromDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";

export interface ExpenseLineTotals {
  subtotal: number;
  discount: number;
  taxableBase: number;
  vat: number;
  total: number;
}

export type VatRateByCode = Map<string, number>;

/**
 * EXPENSE-BACKEND-01 — cuenta contable destino de una subcategoría, tal como la entrega el árbol de
 * categorías (`expenses.catalog.view`). Reemplaza al listado del plan de cuentas, que exige
 * `accounting.view`, y solo contiene las cuentas vinculadas a subcategorías.
 */
export interface ExpenseAccountRef {
  id: string;
  code: string;
  name: string;
}

export function buildExpenseAccountsById(
  tree: ExpenseCategoryTreeNodeDto[],
): Map<string, ExpenseAccountRef> {
  const accounts = new Map<string, ExpenseAccountRef>();
  const visit = (nodes: ExpenseCategoryTreeNodeDto[]) => {
    for (const node of nodes) {
      if (node.accountingAccountId && node.accountingAccountCode) {
        accounts.set(node.accountingAccountId, {
          id: node.accountingAccountId,
          code: node.accountingAccountCode,
          name: node.accountingAccountName ?? "",
        });
      }
      visit(node.children ?? []);
    }
  };
  visit(tree);
  return accounts;
}

/**
 * ZH-DESIGN-SYSTEM-PRECISION-04G — escalas del MODELO TEXTUAL del borrador (los inputs de la línea
 * son controlados con `value` string: el texto es el dato que se edita y se envía). Utilidad pura:
 * no consulta la PrecisionPolicy; el caller React las resuelve con `usePrecisionDecimals`
 * (unitPrice → "purchaseUnitPrice", discountValue → "money", las mismas `precision` de sus inputs).
 */
export interface ExpenseDraftLineScales {
  unitPriceDecimals: number;
  moneyDecimals: number;
}

export function newExpenseDraftLine(scales: ExpenseDraftLineScales): ExpenseDraftLineState {
  return {
    key: globalThis.crypto?.randomUUID?.() ?? `line-${Date.now()}-${Math.random()}`,
    expenseSubcategoryId: "",
    description: "",
    quantity: "1",
    unitPrice: formatDecimalDisplay(0, scales.unitPriceDecimals),
    discountValue: formatDecimalDisplay(0, scales.moneyDecimals),
    vatCode: "0",
    notes: "",
  };
}

export function parseExpenseNumber(value: string): number {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

// Mismo criterio fiscal que el backend (ExpenseLine: decimal exacto, redondeo AwayFromZero a
// FiscalPrecision.TaxAmount = 2): Decimal con ROUND_HALF_UP. El redondeo con floats (`Math.round(x * 100)`)
// diverge en empates binarios (p. ej. 1.005 -> 1.00 en JS vs 1.01 en el servidor).
const ExpenseDecimal = Decimal.clone({ precision: 64 });

function toDecimal(value: string): Decimal {
  const parsed = Number.parseFloat(value);
  if (!Number.isFinite(parsed)) return new ExpenseDecimal(0);
  try {
    return new ExpenseDecimal(value.trim());
  } catch {
    return new ExpenseDecimal(parsed);
  }
}

function roundDecimal(value: Decimal): Decimal {
  return value.toDecimalPlaces(
    getPrecisionPolicy().moneyDecimals,
    ExpenseDecimal.ROUND_HALF_UP,
  );
}

export function calculateExpenseLineTotals(
  line: ExpenseDraftLineState,
  vatRateByCode: VatRateByCode,
): ExpenseLineTotals {
  const subtotal = roundDecimal(toDecimal(line.quantity).mul(toDecimal(line.unitPrice)));
  const discount = ExpenseDecimal.min(roundDecimal(toDecimal(line.discountValue)), subtotal);
  const taxableBase = roundDecimal(ExpenseDecimal.max(0, subtotal.minus(discount)));
  const vatRate = new ExpenseDecimal(vatRateByCode.get(line.vatCode.trim()) ?? 0);
  const vat = roundDecimal(taxableBase.mul(vatRate).div(100));
  return {
    subtotal: subtotal.toNumber(),
    discount: discount.toNumber(),
    taxableBase: taxableBase.toNumber(),
    vat: vat.toNumber(),
    total: roundDecimal(taxableBase.plus(vat)).toNumber(),
  };
}

export function calculateExpenseDocumentTotals(
  lines: ExpenseDraftLineState[],
  vatRateByCode: VatRateByCode,
) {
  // Igual que ExpenseDocument: suma de los importes ya redondeados por línea.
  const sum = lines.reduce(
    (acc, line) => {
      const totals = calculateExpenseLineTotals(line, vatRateByCode);
      acc.subtotal = acc.subtotal.plus(totals.subtotal);
      acc.totalDiscount = acc.totalDiscount.plus(totals.discount);
      acc.totalTax = acc.totalTax.plus(totals.vat);
      acc.grandTotal = acc.grandTotal.plus(totals.total);
      return acc;
    },
    {
      subtotal: new ExpenseDecimal(0),
      totalDiscount: new ExpenseDecimal(0),
      totalTax: new ExpenseDecimal(0),
      grandTotal: new ExpenseDecimal(0),
    },
  );
  return {
    subtotal: roundDecimal(sum.subtotal).toNumber(),
    totalDiscount: roundDecimal(sum.totalDiscount).toNumber(),
    totalTax: roundDecimal(sum.totalTax).toNumber(),
    grandTotal: roundDecimal(sum.grandTotal).toNumber(),
  };
}

export function findVatCodeForRate(
  vatRates: { code: string; percentage: number }[],
  impliedRatePercent: number,
  tolerance = 0.05,
): string | null {
  const match = vatRates.find(
    (rate) => Math.abs(rate.percentage - impliedRatePercent) <= tolerance,
  );
  return match ? match.code : null;
}

export function documentToSupplier(
  document: ExpenseDocumentDetailDto,
): SupplierPickerRow {
  return {
    id: document.supplierId,
    fullName: document.supplierName,
    identificationNumber: document.supplierTaxId,
    isActive: true,
    hasSupplierRole: true,
    supplierConfig: null,
  };
}

export function documentToHeader(
  document: ExpenseDocumentDetailDto,
  toDateTimeLocalInputValue: (value: string | null | undefined) => string,
): ExpenseDocumentHeaderState {
  return {
    supplierId: document.supplierId,
    issueDate: document.issueDate,
    accountingDate: document.accountingDate,
    documentType: document.documentType,
    documentNumber: document.documentNumber,
    paymentTermId: document.paymentTermId,
    dueDate: document.dueDate ?? "",
    authorizationNumber: document.authorizationNumber ?? "",
    authorizationDate: toDateTimeLocalInputValue(document.authorizationDate),
    notes: document.notes ?? "",
    taxSupportCode: document.taxSupportCode ?? "",
  };
}

export function documentToLines(
  document: ExpenseDocumentDetailDto,
  scales: ExpenseDraftLineScales,
): ExpenseDraftLineState[] {
  return document.lines.length > 0
    ? document.lines.map((line) => ({
        key: line.id,
        expenseSubcategoryId: line.expenseSubcategoryId,
        description: line.description,
        quantity: String(line.quantity),
        unitPrice: formatDecimalDisplay(line.unitAmount, scales.unitPriceDecimals),
        discountValue: formatDecimalDisplay(line.discountAmount, scales.moneyDecimals),
        vatCode: line.vatCode,
        notes: line.notes ?? "",
      }))
    : [newExpenseDraftLine(scales)];
}

export function flattenExpenseSubcategories(
  nodes: ExpenseCategoryTreeNodeDto[],
) {
  return nodes.flatMap((type) =>
    type.children.flatMap((category) => category.children),
  );
}

export function hasConfiguredExpenseSubcategory(
  tree: ExpenseCategoryTreeNodeDto[],
  accountsById: Map<string, ExpenseAccountRef>,
): boolean {
  return flattenExpenseSubcategories(tree).some(
    (node) =>
      node.isActive &&
      node.accountingAccountId &&
      accountsById.has(node.accountingAccountId),
  );
}

export function buildExpenseDraftPayload(
  header: ExpenseDocumentHeaderState,
  lines: ExpenseDraftLineState[],
): CreateExpenseDraftPayload {
  return {
    supplierId: header.supplierId,
    issueDate: header.issueDate,
    accountingDate: header.accountingDate,
    documentType: header.documentType.trim(),
    documentNumber: header.documentNumber.trim(),
    paymentTermId: header.paymentTermId || null,
    dueDate: header.dueDate || null,
    authorizationNumber: header.authorizationNumber.trim() || null,
    // DATE-02A: datetime-local = hora de Company.Timezone → UTC una sola vez. Antes
    // `new Date(local).toISOString()` convertía con la zona del NAVEGADOR y al recargar se
    // leía el UTC como hora local: +5h de drift por cada edición/guardado.
    authorizationDate: fromDateTimeLocalInputValue(header.authorizationDate),
    notes: header.notes.trim() || null,
    taxSupportCode: normalizeOptionalCode(header.taxSupportCode),
    lines: lines.map((line) => ({
      expenseSubcategoryId: line.expenseSubcategoryId,
      description: line.description.trim() || null,
      quantity: parseExpenseNumber(line.quantity),
      unitPrice: parseExpenseNumber(line.unitPrice),
      discountValue: parseExpenseNumber(line.discountValue),
      vatCode: line.vatCode.trim() || "0",
      notes: line.notes.trim() || null,
    })),
  };
}
