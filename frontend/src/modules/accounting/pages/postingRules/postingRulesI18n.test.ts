// @vitest-environment node
import { describe, expect, it } from "vitest";
import { dictionaries } from "../../../../i18n/dictionaries";
import { HELP_KEYS, resolveHelp } from "../../../../help";
import {
  amountKindLabel,
  postingOperationLabel,
  sourceModuleLabel,
  type TFunction,
} from "../../labels/accountingLabels";
import { sourceAccountDescription } from "./postingRuleView";

/**
 * ACCOUNTING-POSTING-RULES-UX-01 — paridad i18n es/en y cobertura de etiquetas. Las listas espejan
 * `PostingFactCatalog.All` y `PostingAmountKind` del backend (ERP.Application/ERP.Domain); un hecho o
 * monto nuevo allí debe agregarse aquí y en sus etiquetas.
 */

const CATALOG: Array<[string, string]> = [
  ["Sales", "InvoiceIssued"],
  ["Sales", "SalesReturn"],
  ["Sales", "CostOfGoodsSold"],
  ["Sales", "CostOfGoodsSoldReversed"],
  ["Purchases", "InvoiceReceived"],
  ["Purchases", "PurchaseCreditNoteAuthorized"],
  ["Purchases", "PurchaseReturn"],
  ["Purchases", "SupplierCreditApplied"],
  ["Purchases", "SupplierCreditRefunded"],
  ["Finance", "CollectionApplied"],
  ["Payables", "SupplierPaymentConfirmed"],
  ["Retentions", "DocumentIssued"],
  ["Expenses", "DocumentConfirmed"],
  ["Inventory", "StockAdjustmentIn"],
  ["Inventory", "StockAdjustmentOut"],
  ["Caja", "ManualIncome"],
  ["Caja", "ManualExpense"],
  ["Caja", "Withdrawal"],
  ["Caja", "BankDeposit"],
  ["Caja", "CashCountSurplus"],
  ["Caja", "CashCountShortage"],
  ["InitialLoad", "OpeningInventory"],
  ["InitialLoad", "OpeningReceivables"],
  ["InitialLoad", "OpeningPayables"],
  ["InitialLoad", "ASI"],
];

const AMOUNT_KINDS = [
  "Subtotal", "TaxVat", "TaxIce", "Discount", "Retention", "GrandTotal", "AppliedToPayable",
  "SupplierCredit", "CostVarianceDebit", "CostVarianceCredit", "HistoricalCost", "TaxIrbpnr",
  "RetentionVat", "RetentionIncome", "CashApplied", "PendingBalance",
];

const LOCALES = ["es", "en"] as const;

/** `t` que marca la clave consultada: un valor sin mapa de etiquetas vuelve crudo (sin marca) y una
 * clave mapeada pero ausente del diccionario vuelve como MISSING. "Caja" o "Sales" pueden traducirse
 * igual al valor técnico, por eso no basta comparar contra el valor crudo. */
function tFor(locale: (typeof LOCALES)[number]): TFunction {
  const dict = dictionaries[locale] as Record<string, string>;
  return (key) => (key in dict ? `✓${dict[key]}` : `MISSING:${key}`);
}

const translated = (label: string) => label.startsWith("✓") && !label.includes("{{");

describe("Reglas contables — i18n", () => {
  it("accounting.postingRules.*, accounting.labels.* y help.accountingPostingRules.* en paridad es/en", () => {
    const scoped = (dict: Record<string, string>) =>
      Object.keys(dict)
        .filter(
          (k) =>
            k.startsWith("accounting.postingRules.") ||
            k.startsWith("accounting.labels.") ||
            k.startsWith("help.accountingPostingRules."),
        )
        .sort();
    expect(scoped(dictionaries.en as Record<string, string>)).toEqual(scoped(dictionaries.es as Record<string, string>));
  });

  it.each(LOCALES)("cada hecho del catálogo, su módulo y cada base de importe tienen etiqueta (%s)", (locale) => {
    const t = tFor(locale);
    for (const [module, factType] of CATALOG) {
      expect(translated(sourceModuleLabel(t, module)), module).toBe(true);
      expect(translated(postingOperationLabel(t, module, factType)), `${module}/${factType}`).toBe(true);
    }
    for (const kind of AMOUNT_KINDS) {
      expect(translated(amountKindLabel(t, kind)), kind).toBe(true);
    }
  });

  it.each(LOCALES)("las cuentas que aporta el origen se resuelven a texto traducido (%s)", (locale) => {
    const t = tFor(locale);
    for (const side of ["Debit", "Credit"] as const) {
      for (const [module, factType] of CATALOG) {
        expect(translated(sourceAccountDescription(t, `${module}/${factType}`, side))).toBe(true);
      }
    }
  });

  it("las tres ayudas de la pantalla existen en el registro y en ambos idiomas", () => {
    for (const key of [
      HELP_KEYS.ACCOUNTING_POSTING_RULES_OVERVIEW,
      HELP_KEYS.ACCOUNTING_POSTING_RULES_ENTRY,
      HELP_KEYS.ACCOUNTING_POSTING_RULES_STATUS,
    ]) {
      const entry = resolveHelp(key);
      expect(entry?.i18nPrefix).toBeTruthy();
      for (const locale of LOCALES) {
        const dict = dictionaries[locale] as Record<string, string>;
        for (const part of ["title", "short", "long"]) {
          expect(dict[`${entry?.i18nPrefix}.${part}`], `${locale} ${key}.${part}`).toBeTruthy();
        }
      }
      expect((dictionaries.es as Record<string, string>)[`${entry?.i18nPrefix}.short`]).toBe(entry?.short);
    }
  });
});
