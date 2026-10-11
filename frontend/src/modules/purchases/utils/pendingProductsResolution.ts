import { calcPriceForMargin } from "../../../lib/margin";
import { roundToDecimals } from "../../../lib/sanitizers";
import type {
  ItemMatchStatus,
  ResolveReceptionLinesRequest,
  ResolveReceptionRowError,
} from "../api/purchaseReceptionService";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import type {
  PendingProductMode,
  PendingProductRow,
} from "../schemas/pendingProductsSchema";
import type { PurchaseLineReadiness } from "./purchaseLineReadiness";

/**
 * COMPRAS-METODO-ZH-01B — lógica pura (sin React) de la resolución masiva de productos pendientes.
 * La fuente de cada fila es el snapshot XML ya cargado en la línea de compra: nunca se reescribe la
 * descripción del proveedor; el nombre ERP es un campo aparte, editable.
 */
export interface PendingProductSource {
  rowId: string;
  lineKey: number;
  supplierCode: string;
  supplierAuxCode: string;
  xmlDescription: string;
  quantity: number;
  unitPrice: number;
  discountPct: number;
  vatCode: string;
  iceCode: string;
  matchStatus: ItemMatchStatus | undefined;
}

/** Líneas XML (con origen de recepción) todavía sin producto vinculado. */
export function selectPendingProductSources(
  lines: PurchaseLineFormValues[],
): PendingProductSource[] {
  return lines
    .filter((l) => !!l.purchaseReceptionLineId && !l.itemId)
    .map((l) => ({
      rowId: l.purchaseReceptionLineId as string,
      lineKey: l._key,
      supplierCode: l.xmlSupplierCode ?? "",
      supplierAuxCode: l.xmlSupplierAuxCode ?? "",
      xmlDescription: l.xmlDescription ?? l.description,
      quantity: l.xmlQuantity ?? l.quantity,
      unitPrice: l.xmlUnitPrice ?? l.unitPrice,
      discountPct: l.xmlDiscount != null && (l.xmlQuantity ?? l.quantity) * (l.xmlUnitPrice ?? l.unitPrice) > 0
        ? l.xmlDiscount / ((l.xmlQuantity ?? l.quantity) * (l.xmlUnitPrice ?? l.unitPrice)) * 100
        : l.discountPct ?? 0,
      vatCode: l.vatCode ?? "",
      iceCode: l.iceCode ?? "",
      matchStatus: l.itemMatchStatus,
    }));
}

const SKU_INVALID = /[^A-Za-z0-9\-_.]+/g;

/** SKU sugerido desde el código del proveedor: solo caracteres válidos, máx. 50. */
export function suggestSku(code: string): string {
  return code.trim().replace(SKU_INVALID, "-").replace(/^-+|-+$/g, "").slice(0, 50);
}

/**
 * Código de barras real (GTIN-8/12/13/14 con dígito verificador válido). Un código del proveedor
 * que no lo es sirve solo para el matching: nunca se precarga como código de barras.
 */
export function isRealBarcode(code: string): boolean {
  const value = code.trim();
  if (!/^(\d{8}|\d{12,14})$/.test(value)) return false;
  const digits = [...value].map(Number);
  const check = digits.pop() as number;
  // Pesos 3,1,3… desde el dígito más cercano al verificador.
  const sum = digits.reduceRight(
    (acc, digit, i) => acc + digit * ((digits.length - 1 - i) % 2 === 0 ? 3 : 1),
    0,
  );
  return (10 - (sum % 10)) % 10 === check;
}

/** Tipo sugerido por forma (EAN-13/EAN-8) — solo si existe en el catálogo; nunca "Interno" implícito. */
export function suggestBarcodeType(code: string, availableTypes: string[]): string {
  const available = new Set(availableTypes);
  const candidate = /^\d{13}$/.test(code) ? "EAN13" : /^\d{8}$/.test(code) ? "EAN8" : "";
  return available.has(candidate) ? candidate : "";
}

/** Presentación de la unidad base (factor 1): su código de barras va al Item y requiere tipo. */
export function isBasePresentation(row: Pick<PendingProductRow, "presentationFactor">): boolean {
  return row.presentationFactor == null || row.presentationFactor === 1;
}

/**
 * Código que el backend registrará como Interno provisional cuando el producto nuevo de `rowId`
 * no tiene código de barras real de la unidad (mismo criterio que el servidor): código del
 * proveedor de una línea de unidad primero, nunca uno que ya es el código de una caja/pack.
 * `null` si la unidad ya tiene código real o no hay código del proveedor utilizable.
 */
export function provisionalUnitBarcode(
  rows: PendingProductRow[],
  rowId: string,
  sources: ReadonlyMap<string, PendingProductSource>,
): string | null {
  const itemRows = rows.filter(
    (r) =>
      r.selected &&
      ((r.mode === "create" && r.rowId === rowId) ||
        (r.mode === "presentation" && r.parentRowId === rowId)),
  );
  if (itemRows.some((r) => isBasePresentation(r) && r.barcode.trim())) return null;
  const packBarcodes = new Set(
    itemRows
      .filter((r) => !isBasePresentation(r) && r.barcode.trim())
      .map((r) => r.barcode.trim().toUpperCase()),
  );
  const candidates = [
    ...itemRows.filter((r) => isBasePresentation(r)),
    ...itemRows.filter((r) => !isBasePresentation(r)),
  ].map((r) => sources.get(r.rowId)?.supplierCode.trim() ?? "");
  return (
    candidates.find((code) => code && !packBarcodes.has(code.toUpperCase())) ?? null
  );
}

/** Precarga de una fila: todo lo que el XML ya sabe, nada inventado. */
export function buildInitialRow(
  source: PendingProductSource,
  barcodeTypes: string[],
): PendingProductRow {
  const barcode =
    [source.supplierAuxCode, source.supplierCode].map((c) => c.trim()).find(isRealBarcode) ?? "";
  return {
    rowId: source.rowId,
    selected: true,
    mode: "create",
    sku: suggestSku(source.supplierAuxCode || source.supplierCode),
    shortName: source.xmlDescription.trim().slice(0, 50),
    description: source.xmlDescription.trim().slice(0, 254),
    itemTypeId: "",
    brandId: "",
    categoryNodeId: "",
    defaultUomCode: "",
    barcode,
    barcodeType: barcode ? suggestBarcodeType(barcode, barcodeTypes) : "",
    saleVatCode: source.vatCode,
    purchaseVatCode: source.vatCode,
    exciseTaxCode: source.iceCode,
    salePrice: null,
    presentationFactor: 1,
    presentationName: "",
    presentationUomCode: "",
    linkItemId: "",
    linkItemLabel: "",
    linkPackagingLevelId: "",
    parentRowId: "",
  };
}

/** Campos que se pueden asignar en bloque — mismo SSOT de catálogos que la creación de Items. */
export type BulkAssignableField =
  | "mode"
  | "itemTypeId"
  | "brandId"
  | "categoryNodeId"
  | "defaultUomCode"
  | "barcodeType"
  | "purchaseVatCode"
  | "saleVatCode";

export type BulkAssignment = Partial<Pick<PendingProductRow, BulkAssignableField>>;

/** Aplica solo los valores elegidos a las filas seleccionadas; cada fila sigue siendo editable. */
export function applyBulkAssignment(
  rows: PendingProductRow[],
  assignment: BulkAssignment,
): PendingProductRow[] {
  const chosen = Object.fromEntries(
    Object.entries(assignment).filter(([, value]) => value != null && value !== ""),
  ) as BulkAssignment;
  return rows.map((row) => (row.selected ? { ...row, ...chosen } : row));
}

/** Costo unitario neto (después de descuento) por unidad base de la presentación. */
export function netUnitCost(source: PendingProductSource, factor: number | null): number {
  const perPresentation = source.unitPrice * (1 - (source.discountPct ?? 0) / 100);
  return factor && factor > 0 ? perPresentation / factor : perPresentation;
}

/**
 * Precio de venta por margen sobre PRECIO (misma fórmula única de `lib/margin`) para las filas
 * seleccionadas que crean producto; las demás filas no cambian.
 */
export function applyMarginToSelected(
  rows: PendingProductRow[],
  sources: ReadonlyMap<string, PendingProductSource>,
  marginPct: number,
  priceDecimals: number,
): PendingProductRow[] {
  return rows.map((row) => {
    const source = sources.get(row.rowId);
    if (!row.selected || row.mode !== "create" || !source) return row;
    const price = calcPriceForMargin(netUnitCost(source, row.presentationFactor), marginPct);
    return price == null ? row : { ...row, salePrice: roundToDecimals(price, priceDecimals) };
  });
}

const optional = (value: string) => (value.trim() ? value.trim() : null);

/** Contrato del backend: productos nuevos (clave = fila que lo crea) + resolución de cada línea. */
export function buildResolveRequest(rows: PendingProductRow[]): ResolveReceptionLinesRequest {
  const selected = rows.filter((r) => r.selected);
  const presentation = (r: PendingProductRow) => {
    const barcode = optional(r.barcode);
    return {
      presentationFactor: r.presentationFactor ?? 1,
      presentationName: r.presentationFactor === 1 ? null : optional(r.presentationName),
      presentationUomCode: r.presentationFactor === 1 ? null : optional(r.presentationUomCode),
      barcode,
      barcodeType: barcode && isBasePresentation(r) ? optional(r.barcodeType) : null,
    };
  };
  return {
    newItems: selected
      .filter((r) => r.mode === "create")
      .map((r) => ({
        key: r.rowId,
        sku: r.sku.trim(),
        shortName: r.shortName.trim(),
        description: r.description.trim(),
        itemTypeId: r.itemTypeId,
        categoryNodeId: r.categoryNodeId,
        brandId: r.brandId,
        defaultUomCode: r.defaultUomCode,
        saleVatCode: optional(r.saleVatCode),
        purchaseVatCode: optional(r.purchaseVatCode),
        exciseTaxCode: optional(r.exciseTaxCode),
        baseSalePrice: r.salePrice ?? 0,
      })),
    lines: selected.map((r) => {
      switch (r.mode as PendingProductMode) {
        case "link":
          return {
            purchaseReceptionLineId: r.rowId,
            itemId: r.linkItemId,
            packagingLevelId: optional(r.linkPackagingLevelId),
          };
        case "presentation":
          return { purchaseReceptionLineId: r.rowId, newItemKey: r.parentRowId, ...presentation(r) };
        default:
          return { purchaseReceptionLineId: r.rowId, newItemKey: r.rowId, ...presentation(r) };
      }
    }),
  };
}

/** Errores del lote agrupados por fila (una fila = una línea XML = clave del producto que crea). */
export function groupRowErrors(errors: ResolveReceptionRowError[]): {
  byRow: Map<string, string[]>;
  general: string[];
} {
  const byRow = new Map<string, string[]>();
  const general: string[] = [];
  for (const e of errors) {
    const rowId = e.purchaseReceptionLineId ?? e.newItemKey;
    if (!rowId) {
      general.push(e.message);
      continue;
    }
    byRow.set(rowId, [...(byRow.get(rowId) ?? []), e.message]);
  }
  return { byRow, general };
}

export interface XmlLinesSummary {
  total: number;
  resolved: number;
  pending: number;
  warnings: number;
}

/**
 * Resumen orientado a excepciones de las líneas XML, derivado del readiness ya calculado por línea
 * (sin reglas nuevas): pendiente = sin producto; advertencia = con producto pero no lista.
 */
export function summarizeXmlLines(
  lines: PurchaseLineFormValues[],
  readinessByKey: Readonly<Record<number, PurchaseLineReadiness | undefined>>,
): XmlLinesSummary {
  const xml = lines.filter((l) => !!l.purchaseReceptionLineId);
  let pending = 0;
  let warnings = 0;
  for (const line of xml) {
    const status = readinessByKey[line._key]?.status;
    if (!line.itemId || status === "MISSING_ITEM") pending++;
    else if ((status && status !== "READY") || readinessByKey[line._key]?.warning) warnings++;
  }
  return { total: xml.length, resolved: xml.length - pending - warnings, pending, warnings };
}
