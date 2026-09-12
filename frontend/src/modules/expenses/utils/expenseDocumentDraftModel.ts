import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import type { AccountDto } from "../../accounting/api/accountingApi";
import type { SupplierPickerRow } from "../../masterData/types/businessPartner.types";
import type { ExpenseCategoryTreeNodeDto } from "../api/expenseCategoryService";
import type {
  CreateExpenseDraftPayload,
  ExpenseDocumentDetailDto,
} from "../api/expenseDocumentService";
import type { ExpenseDocumentHeaderState } from "../components/ExpenseDocumentHeader";
import type { ExpenseDraftLineState } from "../components/ExpenseDocumentLinesEditor";

export interface ExpenseLineTotals {
  subtotal: number;
  discount: number;
  taxableBase: number;
  vat: number;
  total: number;
}

export type VatRateByCode = Map<string, number>;

export function newExpenseDraftLine(): ExpenseDraftLineState {
  return {
    key: globalThis.crypto?.randomUUID?.() ?? `line-${Date.now()}-${Math.random()}`,
    expenseSubcategoryId: "",
    description: "",
    quantity: "1",
    unitPrice: "0.00",
    discountValue: "0.00",
    vatCode: "0",
    notes: "",
  };
}

export function parseExpenseNumber(value: string): number {
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : 0;
}

function roundMoney(value: number): number {
  const decimals = getDecimalConfig().totalAmount;
  const factor = 10 ** decimals;
  return Math.round(value * factor) / factor;
}

export function calculateExpenseLineTotals(
  line: ExpenseDraftLineState,
  vatRateByCode: VatRateByCode,
): ExpenseLineTotals {
  const subtotal = roundMoney(
    parseExpenseNumber(line.quantity) * parseExpenseNumber(line.unitPrice),
  );
  const discount = Math.min(
    roundMoney(parseExpenseNumber(line.discountValue)),
    subtotal,
  );
  const taxableBase = roundMoney(Math.max(0, subtotal - discount));
  const vatRate = vatRateByCode.get(line.vatCode.trim()) ?? 0;
  const vat = roundMoney((taxableBase * vatRate) / 100);
  return {
    subtotal,
    discount,
    taxableBase,
    vat,
    total: roundMoney(taxableBase + vat),
  };
}

export function calculateExpenseDocumentTotals(
  lines: ExpenseDraftLineState[],
  vatRateByCode: VatRateByCode,
) {
  return lines.reduce(
    (acc, line) => {
      const totals = calculateExpenseLineTotals(line, vatRateByCode);
      acc.subtotal = roundMoney(acc.subtotal + totals.subtotal);
      acc.totalDiscount = roundMoney(acc.totalDiscount + totals.discount);
      acc.totalTax = roundMoney(acc.totalTax + totals.vat);
      acc.grandTotal = roundMoney(acc.grandTotal + totals.total);
      return acc;
    },
    { subtotal: 0, totalDiscount: 0, totalTax: 0, grandTotal: 0 },
  );
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
): ExpenseDraftLineState[] {
  return document.lines.length > 0
    ? document.lines.map((line) => ({
        key: line.id,
        expenseSubcategoryId: line.expenseSubcategoryId,
        description: line.description,
        quantity: String(line.quantity),
        unitPrice: line.unitAmount.toFixed(getDecimalConfig().purchaseUnitPrice),
        discountValue: line.discountAmount.toFixed(getDecimalConfig().totalAmount),
        vatCode: line.vatCode,
        notes: line.notes ?? "",
      }))
    : [newExpenseDraftLine()];
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
  accountsById: Map<string, AccountDto>,
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
    authorizationDate: header.authorizationDate
      ? new Date(header.authorizationDate).toISOString()
      : null,
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
