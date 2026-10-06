import {
  parseValidationErrors,
  readApiErrorMessages,
} from "../../lib/apiError";
import {
  salesLineSchema,
  type SalesLineFormValues,
} from "../schemas/salesInvoiceSchema";
import { lineExceedsStock, lineQuantityInBaseUom } from "./salesCalc";

export type SalesLineIssueColumn =
  "product" | "stock" | "quantity" | "price" | "discount" | "total";
export interface SalesLineIssue {
  key: number;
  field: string;
  column: SalesLineIssueColumn;
  message: string;
  detail?: string;
  /** Server errors are valid only for the data that the server rejected. */
  fingerprint?: string;
}
export function lineIssueColumn(field: string): SalesLineIssueColumn {
  if (/warehouse|stock/i.test(field)) return "stock";
  if (/quantity/i.test(field)) return "quantity";
  if (/price/i.test(field)) return "price";
  if (/discount/i.test(field)) return "discount";
  if (/vat|ice|tax/i.test(field)) return "total";
  return "product";
}
export function lineIssueFingerprint(
  line: SalesLineFormValues,
  column: SalesLineIssueColumn,
): string {
  const common = [line.itemId, line.packagingLevelId];
  const fields =
    column === "stock"
      ? [line.warehouseId, line.quantity, line.conversionFactor, line._stockQty]
      : column === "quantity"
        ? [line.quantity]
        : column === "price" || column === "discount"
          ? [line.unitPrice, line.discountPct]
          : column === "total"
            ? [line.vatCode, line.iceCode]
            : [line.description, line.itemId, line.packagingLevelId];
  return JSON.stringify([...common, ...fields]);
}
export function collectSalesLineIssues(
  lines: SalesLineFormValues[],
  server: SalesLineIssue[] = [],
): SalesLineIssue[] {
  const issues: SalesLineIssue[] = [];
  for (const line of lines) {
    const parsed = salesLineSchema.safeParse(line);
    if (!parsed.success)
      for (const issue of parsed.error.issues) {
        const field = String(issue.path[0] ?? "itemId");
        issues.push({
          key: line._key,
          field,
          column: lineIssueColumn(field),
          message: issue.message,
        });
      }
    if (line._participatesInInventory && !line.warehouseId)
      issues.push({
        key: line._key,
        field: "warehouseId",
        column: "stock",
        message: "Seleccione la bodega de despacho.",
      });
    if (lineExceedsStock(line))
      issues.push({
        key: line._key,
        field: "stock",
        column: "stock",
        message: "Stock insuficiente",
        detail: `Disponible: ${line._stockQty} · Solicitado: ${lineQuantityInBaseUom(line)} ${line.baseUomCode ?? "UDS"}`,
      });
    for (const issue of server)
      if (
        issue.key === line._key &&
        issue.fingerprint === lineIssueFingerprint(line, issue.column)
      ) {
        if (
          !issues.some(
            (i) =>
              i.key === issue.key &&
              i.field === issue.field &&
              i.message === issue.message,
          )
        )
          issues.push(issue);
      }
  }
  return issues;
}
function fieldFromMessage(message: string): string {
  if (/stock|disponible|bodega/i.test(message)) return "stock";
  if (/precio/i.test(message)) return "unitPrice";
  if (/descuento/i.test(message)) return "discountPct";
  if (/cantidad/i.test(message)) return "quantity";
  if (/IVA|ICE|tribut|impuesto/i.test(message)) return "vatCode";
  return "itemId";
}
/** Use field indexes first; legacy business errors identify a line by its exact description.
 * If descriptions repeat, flag every matching row rather than silently choosing the wrong one. */
export function mapSalesServerLineIssues(
  err: unknown,
  lines: SalesLineFormValues[],
) {
  const lineIssues: SalesLineIssue[] = [];
  const globalMessages: string[] = [];
  const fields = parseValidationErrors(err);
  const entries = fields
    ? Object.entries(fields)
    : ([["_", readApiErrorMessages(err)]] as [string, string[]][]);
  for (const [path, values] of entries)
    for (const message of Array.isArray(values) ? values : []) {
      if (typeof message !== "string") continue;
      const indexed = /^lines(?:\[(\d+)\]|\.(\d+))(?:\.(\w+))?/i.exec(path);
      const prefix = /L[i\u00ed]nea\s+'(.+?)':/i.exec(message);
      const description = prefix?.[1];
      const matches = indexed
        ? [lines[Number(indexed[1] ?? indexed[2])]].filter(Boolean)
        : description
          ? lines.filter(
              (line) => line.description.trim() === description.trim(),
            )
          : [];
      if (!matches.length) {
        globalMessages.push(message);
        continue;
      }
      for (const line of matches) {
        const field = indexed?.[3] ?? fieldFromMessage(message);
        const column = lineIssueColumn(field);
        lineIssues.push({
          key: line._key,
          field,
          column,
          message: description
            ? message
                .slice((prefix?.index ?? 0) + (prefix?.[0].length ?? 0))
                .trim()
            : message,
          fingerprint: lineIssueFingerprint(line, column),
        });
      }
    }
  return { lineIssues, globalMessages };
}
export function salesLineCorrectionSummary(issues: SalesLineIssue[]): string {
  const count = new Set(issues.map((issue) => issue.key)).size;
  return count === 1
    ? "1 línea requiere corrección"
    : `${count} líneas requieren corrección`;
}
export function focusSalesLineIssue(issue: SalesLineIssue): void {
  const row = document.querySelector<HTMLElement>(
    `.sf-products [data-sales-line-key="${issue.key}"]`,
  );
  const container = row?.closest<HTMLElement>(".sf-products");
  if (!row || !container) return;
  const headerHeight =
    container.querySelector(".sfl-header")?.getBoundingClientRect().height ?? 0;
  container.scrollTop +=
    row.getBoundingClientRect().top -
    container.getBoundingClientRect().top -
    headerHeight -
    4;
  const selectors: Record<SalesLineIssueColumn, string> = {
    stock: ".zh-wh-selector__trigger, .sf-product__qty-input",
    quantity: ".sf-product__qty-input",
    price: ".sf-product__price-input",
    discount: ".sf-product__disc-input",
    product: ".sf-product__name",
    total: ".sf-product__tax-details summary",
  };
  (row.querySelector<HTMLElement>(selectors[issue.column]) ?? row).focus({
    preventScroll: true,
  });
}
