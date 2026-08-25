type TParams = Record<string, string | number>;
export type TFunction = (key: string, fallbackOrParams?: string | TParams) => string;

const SOURCE_MODULE_KEYS: Record<string, string> = {
  Sales: "accounting.labels.sourceModule.Sales",
  Purchases: "accounting.labels.sourceModule.Purchases",
  Finance: "accounting.labels.sourceModule.Finance",
};

const FACT_TYPE_KEYS: Record<string, string> = {
  InvoiceIssued: "accounting.labels.factType.InvoiceIssued",
  CostOfGoodsSold: "accounting.labels.factType.CostOfGoodsSold",
  CostOfGoodsSoldReversed: "accounting.labels.factType.CostOfGoodsSoldReversed",
  SalesReturn: "accounting.labels.factType.SalesReturn",
  InvoiceReceived: "accounting.labels.factType.InvoiceReceived",
  PurchaseCreditNoteAuthorized: "accounting.labels.factType.PurchaseCreditNoteAuthorized",
  PurchaseCreditNoteCancelled: "accounting.labels.factType.PurchaseCreditNoteCancelled",
  PurchaseReturn: "accounting.labels.factType.PurchaseReturn",
  PurchaseReturnCancelled: "accounting.labels.factType.PurchaseReturnCancelled",
  SupplierCreditApplied: "accounting.labels.factType.SupplierCreditApplied",
  SupplierCreditApplicationReversed: "accounting.labels.factType.SupplierCreditApplicationReversed",
  CollectionApplied: "accounting.labels.factType.CollectionApplied",
  CollectionReversed: "accounting.labels.factType.CollectionReversed",
  SupplierPaymentApplied: "accounting.labels.factType.SupplierPaymentApplied",
  SupplierPaymentReversed: "accounting.labels.factType.SupplierPaymentReversed",
};

const ACCOUNT_TYPE_KEYS: Record<string, string> = {
  Asset: "accounting.labels.accountType.Asset",
  Liability: "accounting.labels.accountType.Liability",
  Equity: "accounting.labels.accountType.Equity",
  Income: "accounting.labels.accountType.Income",
  Cost: "accounting.labels.accountType.Cost",
  Expense: "accounting.labels.accountType.Expense",
};

const ACCOUNT_NATURE_KEYS: Record<string, string> = {
  Debit: "accounting.labels.accountNature.Debit",
  Credit: "accounting.labels.accountNature.Credit",
};

const LINE_DIRECTION_KEYS: Record<string, string> = {
  Debit: "accounting.labels.lineDirection.Debit",
  Credit: "accounting.labels.lineDirection.Credit",
};

const AMOUNT_KIND_KEYS: Record<string, string> = {
  Subtotal: "accounting.labels.amountKind.Subtotal",
  TaxVat: "accounting.labels.amountKind.TaxVat",
  TaxIce: "accounting.labels.amountKind.TaxIce",
  Discount: "accounting.labels.amountKind.Discount",
  Retention: "accounting.labels.amountKind.Retention",
  GrandTotal: "accounting.labels.amountKind.GrandTotal",
  AppliedToPayable: "accounting.labels.amountKind.AppliedToPayable",
  SupplierCredit: "accounting.labels.amountKind.SupplierCredit",
  CostVarianceDebit: "accounting.labels.amountKind.CostVarianceDebit",
  CostVarianceCredit: "accounting.labels.amountKind.CostVarianceCredit",
  HistoricalCost: "accounting.labels.amountKind.HistoricalCost",
  TaxIrbpnr: "accounting.labels.amountKind.TaxIrbpnr",
};

function labelFromMap(t: TFunction, map: Record<string, string>, value: string): string {
  const key = map[value];
  return key ? t(key, value) : value;
}

export function sourceModuleLabel(t: TFunction, value: string): string {
  return labelFromMap(t, SOURCE_MODULE_KEYS, value);
}

export function factTypeLabel(t: TFunction, value: string): string {
  const [prefix, detail] = value.split(":", 2);
  if (prefix === "SupplierCreditRefunded" && detail) {
    return t("accounting.labels.factType.SupplierCreditRefunded", { destination: detail });
  }
  if (prefix === "SupplierCreditRefundReversed" && detail) {
    return t("accounting.labels.factType.SupplierCreditRefundReversed", { destination: detail });
  }
  return labelFromMap(t, FACT_TYPE_KEYS, value);
}

export function accountTypeLabel(t: TFunction, value: string): string {
  return labelFromMap(t, ACCOUNT_TYPE_KEYS, value);
}

export function accountNatureLabel(t: TFunction, value: string): string {
  return labelFromMap(t, ACCOUNT_NATURE_KEYS, value);
}

export function lineDirectionLabel(t: TFunction, value: string): string {
  return labelFromMap(t, LINE_DIRECTION_KEYS, value);
}

export function amountKindLabel(t: TFunction, value: string): string {
  return labelFromMap(t, AMOUNT_KIND_KEYS, value);
}
