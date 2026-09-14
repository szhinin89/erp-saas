type TParams = Record<string, string | number>;
export type TFunction = (key: string, fallbackOrParams?: string | TParams) => string;

// ACCOUNTING-JOURNAL-LABELS-UX-01: lista verificada contra los `SourceModuleName` reales de cada
// *PostingTranslator.cs backend (grep exhaustivo, no inventado) — Sales/Purchases/Finance/
// Expenses/Retentions/Payables cubren cualquier SourceModule que un PostingFact puede llevar hoy, más
// "Accounting" que usa el motor de REVERSOS (ver más abajo, FactType "Reversal") para cualquier
// anulación/cancelación de cualquier módulo — no existe un SourceModule "Inventory" real pese a
// aparecer como opción de filtro en Libro Diario (ese filtro cubre un caso futuro, no uno actual).
const SOURCE_MODULE_KEYS: Record<string, string> = {
  Sales: "accounting.labels.sourceModule.Sales",
  Purchases: "accounting.labels.sourceModule.Purchases",
  Finance: "accounting.labels.sourceModule.Finance",
  Expenses: "accounting.labels.sourceModule.Expenses",
  Retentions: "accounting.labels.sourceModule.Retentions",
  Payables: "accounting.labels.sourceModule.Payables",
  Accounting: "accounting.labels.sourceModule.Accounting",
};

// ACCOUNTING-JOURNAL-LABELS-UX-01: verificado contra el `FactTypeName` real de cada
// *PostingTranslator.cs — varios FactType "Cancelled"/"Anulación" que el negocio esperaría (venta
// anulada, compra anulada, gasto anulado, NC de compra anulada, factura de compra anulada) NO
// existen como FactType propio: esos flujos REVERSAN el asiento original (SourceModule=
// "Accounting", FactType="Reversal", ver JournalEntrySourceResolution.cs/GetJournalEntriesUseCases.cs)
// en vez de crear un hecho contable nuevo — ya señalizado en la UI con el badge "Es reverso de
// otro asiento"/"Tiene un asiento reverso" (JournalEntryDetailPage.tsx). Por eso "Reversal" cubre
// TODAS las anulaciones de cualquier módulo con una sola entrada, en vez de una por módulo.
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
  SupplierPaymentConfirmed: "accounting.labels.factType.SupplierPaymentConfirmed",
  SupplierPaymentReversed: "accounting.labels.factType.SupplierPaymentReversed",
  // Expenses es hoy el único módulo que emite este FactType genérico — si otro módulo lo
  // reutilizara en el futuro, este mapeo por-FactType (sin SourceModule) dejaría de ser exacto.
  DocumentConfirmed: "accounting.labels.factType.Expenses.DocumentConfirmed",
  // Retentions es hoy el único módulo que emite este FactType genérico — mismo riesgo que arriba.
  DocumentIssued: "accounting.labels.factType.Retentions.DocumentIssued",
  Reversal: "accounting.labels.factType.Reversal",
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

// ACCOUNTING-JOURNAL-SALES-LABELS-UX-01: descripción corta opcional, solo para los FactType donde
// el usuario confunde el nombre técnico con otro asiento relacionado (Sales InvoiceIssued vs
// CostOfGoodsSold en una venta de contado). Deliberadamente NO se agrega una entrada por cada
// FactType — a diferencia de FACT_TYPE_KEYS (que sí cubre todos y cae al valor crudo si falta),
// esta función devuelve "" para cualquier FactType sin descripción explícita, nunca un texto
// inventado.
const FACT_TYPE_DESCRIPTION_KEYS: Record<string, string> = {
  InvoiceIssued: "accounting.labels.factType.InvoiceIssued.description",
  CostOfGoodsSold: "accounting.labels.factType.CostOfGoodsSold.description",
};

export function factTypeDescription(t: TFunction, value: string): string {
  const key = FACT_TYPE_DESCRIPTION_KEYS[value];
  return key ? t(key, "") : "";
}

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

// ACCOUNTING-JOURNAL-LABELS-UX-01: el campo "Description" del asiento (y, por defecto, el de cada
// línea fija de PostingRule.Lines) lo compone SIEMPRE JournalFactory.cs backend con el mismo
// formato exacto: "{SourceModule} — {FactType} — {SourceEventId}" (único punto de composición en
// todo el backend, ver JournalFactory.Create) — nunca texto libre de negocio para esos casos. Las
// líneas dinámicas de PostingAllocation SÍ pueden traer una descripción de negocio genuina (ej.
// "serv nube") que jamás sigue ese formato. Por eso esta función solo reescribe la cadena cuando
// el primer segmento coincide con un SourceModule real conocido — cualquier otro texto (incluida
// una descripción de negocio real que por coincidencia use " — ") se devuelve intacto, nunca se
// inventa una traducción. No requiere sourceEventType/sourceEventId por separado: los extrae del
// propio texto, así funciona también en superficies cuyo DTO no expone esos campos (ej. Libro
// Mayor, GeneralLedgerMovementDto, que solo tiene SourceModule).
const KNOWN_SOURCE_MODULES = new Set(Object.keys(SOURCE_MODULE_KEYS));

export function friendlyDescription(t: TFunction, rawDescription: string | null | undefined): string {
  if (!rawDescription) return rawDescription ?? "—";
  const separatorIndex = rawDescription.indexOf(" — ");
  if (separatorIndex === -1) return rawDescription;
  const module = rawDescription.slice(0, separatorIndex);
  if (!KNOWN_SOURCE_MODULES.has(module)) return rawDescription;
  const rest = rawDescription.slice(separatorIndex + 3);
  const factTypeEnd = rest.indexOf(" — ");
  const factType = factTypeEnd === -1 ? rest : rest.slice(0, factTypeEnd);
  return `${sourceModuleLabel(t, module)} — ${factTypeLabel(t, factType)}`;
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
