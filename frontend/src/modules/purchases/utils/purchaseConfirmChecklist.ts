import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import type { PrecisionPolicy } from "../../../lib/config/precisionPolicy.config";
import type { useI18n } from "../../../i18n/i18n";
import type { PurchaseLineDto } from "../api/purchaseService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import { lineNet, resolveLineTaxes, roundToTotalAmount } from "./purchaseCalc";
import { buildPurchaseLinePresentation } from "./purchaseLinePresentation";
import { buildPurchaseLineIssues } from "./purchaseLineIssues";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";

type Translate = ReturnType<typeof useI18n>["t"];

export type ConfirmChecklistIssue = {
  key: string;
  label: string;
  detail: string;
  tone: "danger" | "warning";
};

/**
 * Presentation model of the confirmation checklist. Same checks as before; only the output is
 * grouped: blockers first, then warnings, and every passed check summarized in one line.
 */
export type ConfirmChecklistSummary = {
  hasXmlPurchase: boolean;
  blockerCount: number;
  issues: ConfirmChecklistIssue[];
  passed: string[];
};

export function buildConfirmChecklist(
  lines: PurchaseLineFormValues[],
  persistedLines: Pick<PurchaseLineDto, "purchaseReceptionLineId" | "taxes">[],
  vatRatesMap: Record<string, number>,
  iceRatesMap: Record<string, number>,
  t: Translate,
  moneyDecimals: number,
): ConfirmChecklistSummary {
  const xmlLines = lines.filter((line) => line.purchaseReceptionLineId);
  if (xmlLines.length === 0) return { hasXmlPurchase: false, blockerCount: 0, issues: [], passed: [] };

  const persistedByReceptionLine = new Map(
    persistedLines
      .filter((line) => line.purchaseReceptionLineId)
      .map((line) => [line.purchaseReceptionLineId, line]),
  );
  const linkedItems = xmlLines.filter((line) => !!line.itemId).length;
  const missingItems = xmlLines.length - linkedItems;
  const linesWithoutPresentation = xmlLines.filter((line) => line.itemId && !line.packagingLevelId);
  const stockLinesWithoutPresentation = linesWithoutPresentation.filter(
    (line) => line.context?.participatesInInventory === true,
  );
  const unknownPresentationRisk = linesWithoutPresentation.filter(
    (line) => line.context?.participatesInInventory === undefined,
  );
  const taxesRecognized = xmlLines.filter((line) => {
    const persisted = line.purchaseReceptionLineId
      ? persistedByReceptionLine.get(line.purchaseReceptionLineId)
      : undefined;
    if (persisted?.taxes?.length) return persisted.taxes.every((tax) => tax.taxRateCode && tax.taxName);
    return !!line.vatCode || !!line.xmlTaxCode || line.xmlTaxValue !== undefined;
  }).length;
  const xmlTotalsAvailable = xmlLines.every((line) => typeof line.xmlTotalLine === "number");
  // Split components share one XML total: it is subtracted once per origin line.
  const totalDifference = xmlTotalsAvailable
    ? roundToTotalAmount(
        xmlLines.reduce((sum, line) => {
          const taxes = resolveLineTaxes(line, vatRatesMap, iceRatesMap);
          const isFirstOfOrigin =
            xmlLines.find((l) => l.purchaseReceptionLineId === line.purchaseReceptionLineId) === line;
          return sum + lineNet(line) + taxes.vat + taxes.ice + taxes.irbpnr
            - (isFirstOfOrigin ? (line.xmlTotalLine ?? 0) : 0);
        }, 0),
      )
    : null;

  const issues: ConfirmChecklistIssue[] = [];
  const passed: string[] = [];
  const itemsLabel = t("purchases.confirm.checklist.linkedItems");
  if (missingItems > 0)
    issues.push({ key: "linked-items", tone: "danger", label: itemsLabel,
      detail: t("purchases.confirm.checklist.linkedItemsMissing", { count: missingItems }) });
  else passed.push(`${itemsLabel} (${linkedItems})`);

  // One check for presentations: linked lines without presentation, with their inventory risk.
  const presentationsLabel = t("purchases.confirm.checklist.linkedPresentations");
  if (linesWithoutPresentation.length === 0) passed.push(presentationsLabel);
  else
    issues.push({
      key: "presentations",
      tone: stockLinesWithoutPresentation.length > 0 ? "danger" : "warning",
      label: presentationsLabel,
      detail: stockLinesWithoutPresentation.length > 0
        ? t("purchases.confirm.checklist.linkedPresentationsMissing", { count: linesWithoutPresentation.length })
        : unknownPresentationRisk.length > 0
          ? t("purchases.confirm.checklist.linesWithoutPresentationUnknown", { count: linesWithoutPresentation.length })
          : t("purchases.confirm.checklist.linesWithoutPresentationWarn", { count: linesWithoutPresentation.length }),
    });

  const taxesLabel = t("purchases.confirm.checklist.recognizedTaxes");
  if (taxesRecognized === xmlLines.length) passed.push(`${taxesLabel} (${taxesRecognized})`);
  else
    issues.push({ key: "taxes", tone: "warning", label: taxesLabel,
      detail: t("purchases.confirm.checklist.recognizedTaxesWarn", { count: xmlLines.length - taxesRecognized }) });

  const differenceLabel = t("purchases.confirm.checklist.totalDifference");
  if (totalDifference !== null && Math.abs(totalDifference) <= 0.01)
    passed.push(`${differenceLabel} ${formatMoneyWithSymbol(Math.abs(totalDifference), moneyDecimals)}`);
  else
    issues.push({ key: "total-difference", tone: "warning", label: differenceLabel,
      detail: totalDifference === null
        ? t("purchases.confirm.checklist.totalDifferenceUnavailable")
        : t("purchases.confirm.checklist.totalDifferenceValue", {
            amount: formatMoneyWithSymbol(Math.abs(totalDifference), moneyDecimals),
          }) });

  issues.sort((a, b) => (a.tone === b.tone ? 0 : a.tone === "danger" ? -1 : 1));
  return { hasXmlPurchase: true, blockerCount: missingItems + stockLinesWithoutPresentation.length, issues, passed };
}

export type ConfirmLineWarning = {
  key: string;
  /** "Línea N · producto" — enough to find the line in the form. */
  line: string;
  reason: string;
};

/**
 * Non-blocking warnings still active on the lines. Reuses the line-card issues (same rules and
 * priority); blockers stay with the checklist/readiness so nothing is reported twice. Commercial
 * warnings (readiness.warning) are left out: {@link buildConfirmCommercialRisk} summarizes them.
 */
export function buildConfirmLineWarnings(
  lines: PurchaseLineFormValues[],
  readinessByKey: Record<number, PurchaseLineReadiness | undefined>,
  policy: PrecisionPolicy,
  vatRatesMap: Record<string, number>,
  iceRatesMap: Record<string, number>,
  t: Translate,
): ConfirmLineWarning[] {
  return lines.flatMap((line, index) => {
    const vm = buildPurchaseLinePresentation(line, policy, t, vatRatesMap, iceRatesMap);
    const name = line.context?.shortName || line.description || t("purchases.lines.noLinkedProduct", "Sin producto vinculado");
    const label = `${t("purchases.feedback.line", { count: index + 1 })} · ${name}`;
    const readiness = readinessByKey[line._key];
    return buildPurchaseLineIssues({ vm, t, readiness: readiness && { ...readiness, warning: undefined }, lineLocked: true })
      .filter((issue) => issue.severity === "warning" && issue.intent !== "blocking")
      .map((issue, n) => ({ key: `${line._key}-${n}`, line: label, reason: issue.label }));
  });
}

export type ConfirmCommercialRisk = {
  /** Lines needing commercial attention; the detail lives on each line tag, not in the modal. */
  lineCount: number;
  detail: string;
};

/** One compact indication of the lines' commercial risk (negative/zero margin, no sale price). */
export function buildConfirmCommercialRisk(
  lines: PurchaseLineFormValues[],
  readinessByKey: Record<number, PurchaseLineReadiness | undefined>,
  t: Translate,
): ConfirmCommercialRisk | null {
  const counts = { NEGATIVE_MARGIN: 0, ZERO_MARGIN: 0, MISSING_SALE_PRICE_FOR_MARGIN: 0 };
  for (const line of lines) {
    const status = readinessByKey[line._key]?.warning?.status;
    if (status) counts[status] += 1;
  }
  const lineCount = counts.NEGATIVE_MARGIN + counts.ZERO_MARGIN + counts.MISSING_SALE_PRICE_FOR_MARGIN;
  if (lineCount === 0) return null;
  const parts = [
    counts.NEGATIVE_MARGIN > 0 && t("purchases.confirm.checklist.commercialNegative", { count: counts.NEGATIVE_MARGIN }),
    counts.ZERO_MARGIN > 0 && t("purchases.confirm.checklist.commercialZero", { count: counts.ZERO_MARGIN }),
    counts.MISSING_SALE_PRICE_FOR_MARGIN > 0 &&
      t("purchases.confirm.checklist.commercialNoSalePrice", { count: counts.MISSING_SALE_PRICE_FOR_MARGIN }),
  ].filter(Boolean);
  return { lineCount, detail: `${parts.join(" · ")}. ${t("purchases.confirm.checklist.commercialHint")}` };
}
