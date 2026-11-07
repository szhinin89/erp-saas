import type { PostingFactCatalogItemDto, PostingRuleDto, PostingRuleLineDto } from "../../api/accountingApi";
import type { TFunction } from "../../labels/accountingLabels";

/**
 * ACCOUNTING-POSTING-RULES-UX-01 — modelo de presentación puro de Reglas contables. No define reglas
 * contables: refleja `PostingFactCatalog` (vía `GET posting-rules/catalog`) y las reglas de la
 * Company (`GET posting-rules`). La validación real sigue siendo del backend.
 */

export type PostingRuleStatus = "active" | "inactive" | "missing" | "unsupported";
export type PostingRuleType = "fixed" | "mixed" | "enabling";
export type PostingSide = "Debit" | "Credit";

export interface PostingRuleRow {
  key: string;
  sourceModule: string;
  factType: string;
  /** Hecho del catálogo; ausente = regla antigua no soportada por el motor. */
  fact: PostingFactCatalogItemDto | null;
  /** Regla de la Company; ausente = hecho del catálogo sin configurar. */
  rule: PostingRuleDto | null;
  status: PostingRuleStatus;
  ruleType: PostingRuleType | null;
  problems: string[];
}

export type PostingEntryRow =
  | { key: string; side: PostingSide; origin: "fixed"; line: PostingRuleLineDto; issues: string[] }
  | { key: string; side: PostingSide; origin: "source"; description: string };

export const postingRuleKey = (sourceModule: string, factType: string) => `${sourceModule}/${factType}`;

export function postingRuleType(fact: PostingFactCatalogItemDto | null): PostingRuleType | null {
  if (!fact) return null;
  if (fact.allowedAmountKinds.length === 0) return "enabling";
  return fact.dynamicDebit || fact.dynamicCredit ? "mixed" : "fixed";
}

export function postingRuleTypeLabel(t: TFunction, type: PostingRuleType | null): string {
  return type ? t(`accounting.postingRules.ruleType.${type}`) : "—";
}

function isDynamic(fact: PostingFactCatalogItemDto, side: PostingSide): boolean {
  return side === "Debit" ? fact.dynamicDebit : fact.dynamicCredit;
}

function accountIssues(line: PostingRuleLineDto, t: TFunction): string[] {
  const issues: string[] = [];
  if (!line.accountIsActive) issues.push(t("accounting.postingRules.entry.issue.inactive"));
  if (!line.accountAllowsPosting) issues.push(t("accounting.postingRules.entry.issue.nonPostable"));
  return issues;
}

/** Misma forma que valida el backend (`PostingFactCatalog.ValidateLineShape`) más la vigencia de
 * cada cuenta: un lado dinámico lo completa el origen y una regla habilitadora no lleva líneas. */
export function findRuleProblems(
  rule: PostingRuleDto,
  fact: PostingFactCatalogItemDto | null,
  t: TFunction,
): string[] {
  if (!fact) return [t("accounting.postingRules.problem.unsupported")];
  if (fact.allowedAmountKinds.length === 0) return [];
  const problems: string[] = [];
  if (!fact.dynamicDebit && !fact.dynamicCredit && rule.lines.length < 2) {
    problems.push(t("accounting.postingRules.problem.lessThanTwoLines"));
  }
  if (!fact.dynamicDebit && !rule.lines.some((l) => l.nature === "Debit")) {
    problems.push(t("accounting.postingRules.problem.noDebitLines"));
  }
  if (!fact.dynamicCredit && !rule.lines.some((l) => l.nature === "Credit")) {
    problems.push(t("accounting.postingRules.problem.noCreditLines"));
  }
  for (const l of rule.lines) {
    if (!l.accountIsActive) {
      problems.push(t("accounting.postingRules.problem.inactiveAccount", { code: l.accountCode, name: l.accountName }));
    }
    if (!l.accountAllowsPosting) {
      problems.push(t("accounting.postingRules.problem.nonPostableAccount", { code: l.accountCode, name: l.accountName }));
    }
  }
  return problems;
}

/** Una fila por hecho del catálogo (con o sin regla) más cada regla antigua fuera del catálogo;
 * la clave (SourceModule, FactType) es única por Company, así que nunca hay duplicados. */
export function buildPostingRuleRows(
  catalog: PostingFactCatalogItemDto[],
  rules: PostingRuleDto[],
  t: TFunction,
): PostingRuleRow[] {
  const rulesByKey = new Map(rules.map((r) => [postingRuleKey(r.sourceModule, r.factType), r]));
  const rows: PostingRuleRow[] = catalog.map((fact) => {
    const key = postingRuleKey(fact.sourceModule, fact.factType);
    const rule = rulesByKey.get(key) ?? null;
    rulesByKey.delete(key);
    return {
      key,
      sourceModule: fact.sourceModule,
      factType: fact.factType,
      fact,
      rule,
      status: rule ? (rule.isActive ? "active" : "inactive") : "missing",
      ruleType: postingRuleType(fact),
      problems: rule ? findRuleProblems(rule, fact, t) : [],
    };
  });
  for (const [key, rule] of rulesByKey) {
    rows.push({
      key,
      sourceModule: rule.sourceModule,
      factType: rule.factType,
      fact: null,
      rule,
      status: "unsupported",
      ruleType: null,
      problems: findRuleProblems(rule, null, t),
    });
  }
  return rows.sort(
    (a, b) => a.sourceModule.localeCompare(b.sourceModule) || a.factType.localeCompare(b.factType),
  );
}

// Cuenta que aporta el origen en cada lado dinámico, verificada contra el traductor de cada hecho:
// CashPostingTranslators.cs (ACCOUNTING-12C), StockAdjustmentPostingTranslators.cs (ACCOUNTING-12A),
// SupplierCreditRefundedPostingTranslator.cs, SupplierPaymentConfirmedPostingTranslator.cs,
// ExpenseDocumentConfirmedPostingTranslator.cs y PublishOpeningJournalEntry.cs (IL-8A). Un hecho
// dinámico que no figure aquí muestra el texto genérico `dynamic.debit|credit`, nunca uno inventado.
const SOURCE_ACCOUNT_KEYS: Record<string, Partial<Record<PostingSide, string>>> = {
  "Purchases/SupplierCreditRefunded": { Debit: "refundDestination" },
  "Payables/SupplierPaymentConfirmed": { Credit: "paymentMethods" },
  "Expenses/DocumentConfirmed": { Debit: "expenseSubcategory" },
  "Inventory/StockAdjustmentIn": { Credit: "adjustmentReason" },
  "Inventory/StockAdjustmentOut": { Debit: "adjustmentReason" },
  "Caja/ManualIncome": { Debit: "cashRegister", Credit: "movementReason" },
  "Caja/ManualExpense": { Debit: "movementReason", Credit: "cashRegister" },
  "Caja/Withdrawal": { Debit: "movementReason", Credit: "cashRegister" },
  "Caja/BankDeposit": { Debit: "bankAccount", Credit: "cashRegister" },
  "Caja/CashCountSurplus": { Debit: "cashRegister" },
  "Caja/CashCountShortage": { Credit: "cashRegister" },
  "InitialLoad/ASI": { Debit: "openingEntry", Credit: "openingEntry" },
};

export function sourceAccountDescription(t: TFunction, key: string, side: PostingSide): string {
  const specific = SOURCE_ACCOUNT_KEYS[key]?.[side];
  if (specific) return t(`accounting.postingRules.source.${specific}`);
  return t(side === "Debit" ? "accounting.postingRules.dynamic.debit" : "accounting.postingRules.dynamic.credit");
}

/** Líneas del asiento que genera la regla, Debe primero: las fijas configuradas (por SortOrder) y,
 * en cada lado dinámico del hecho, una fila «Aporta el origen» — nunca una cuenta fija inventada. */
export function buildEntryRows(row: PostingRuleRow, t: TFunction): PostingEntryRow[] {
  const lines = (row.rule?.lines ?? []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
  const result: PostingEntryRow[] = [];
  for (const side of ["Debit", "Credit"] as const) {
    for (const line of lines.filter((l) => l.nature === side)) {
      result.push({ key: line.id, side, origin: "fixed", line, issues: accountIssues(line, t) });
    }
    if (row.fact && isDynamic(row.fact, side)) {
      result.push({
        key: `source-${side}`,
        side,
        origin: "source",
        description: sourceAccountDescription(t, row.key, side),
      });
    }
  }
  return result;
}
