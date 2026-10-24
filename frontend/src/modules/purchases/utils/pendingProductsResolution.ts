import { calcPriceForMargin } from "../../../lib/margin";
import {
  grossFromNet,
  salePriceFromGross,
  type SalePriceBreakdown,
} from "../../../lib/salePrice";
import { roundToDecimals } from "../../../lib/sanitizers";
import type {
  ItemMatchStatus,
  ResolveReceptionLinesRequest,
  ResolveReceptionRowError,
  ResolvedReceptionLine,
} from "../api/purchaseReceptionService";
import { buildPurchaseItemLabel } from "./purchaseItemProfile";
import type { PurchaseLineFormValues } from "../schemas/purchaseInvoiceSchema";
import {
  pendingProductsFormSchema,
  normalizePendingBarcode,
  type PendingProductMode,
  type PendingProductRow,
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

/**
 * SKU sugerido para el producto nuevo — misma regla que ya aplica la creación de Items desde una
 * compra (ItemEditorModal: código del proveedor, en mayúsculas), solo con caracteres válidos para SKU,
 * máx. 50. FASE 5: nunca desde el código auxiliar del XML (no es un código interno del ERP); sin
 * código del proveedor queda vacío y el usuario lo ingresa.
 */
export function suggestSku(supplierCode: string): string {
  return supplierCode
    .trim()
    .replace(SKU_INVALID, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 50)
    .toUpperCase();
}

/** Mismo límite que Items (CreateItemCommandValidator / createItemSchema). */
const MAX_BARCODE_LENGTH = 100;

/**
 * FASE 5 — el código auxiliar del XML cumple las validaciones existentes de código de barras de
 * Items (obligatorio, máx. 100). Solo entonces se propone como código de barras de la unidad base;
 * si no, queda solo como evidencia XML (nunca se convierte en SKU ni en código de barras).
 */
export function isAcceptableBarcode(code: string): boolean {
  const value = normalizePendingBarcode(code);
  return value.length > 0 && value.length <= MAX_BARCODE_LENGTH;
}

/**
 * Código de barras real (GTIN-8/12/13/14 con dígito verificador válido). Un código del proveedor
 * que no lo es sirve solo para el matching: nunca se precarga como código de barras.
 */
export function isRealBarcode(code: string): boolean {
  const value = normalizePendingBarcode(code);
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
  if (itemRows.some((r) => isBasePresentation(r) && normalizePendingBarcode(r.barcode))) return null;
  const packBarcodes = new Set(
    itemRows
      .filter((r) => !isBasePresentation(r) && normalizePendingBarcode(r.barcode))
      .map((r) => normalizePendingBarcode(r.barcode).toUpperCase()),
  );
  const candidates = [
    ...itemRows.filter((r) => isBasePresentation(r)),
    ...itemRows.filter((r) => !isBasePresentation(r)),
  ].map((r) => normalizePendingBarcode(sources.get(r.rowId)?.supplierCode ?? ""));
  return (
    candidates.find((code) => code && !packBarcodes.has(code.toUpperCase())) ?? null
  );
}

/**
 * Código de barras propuesto para la unidad base (FASE 5):
 * - código auxiliar del XML si cumple las validaciones de código de barras de Items;
 * - si no, el código principal del proveedor SOLO si es un código de barras real (GTIN) — el
 *   código del proveedor se conserva como código del proveedor (ItemSupplierCode), no como barcode;
 * - si no, vacío (el backend aplica su regla de código Interno provisional).
 */
export function proposedUnitBarcode(source: PendingProductSource): string {
  const auxCode = normalizePendingBarcode(source.supplierAuxCode);
  if (isAcceptableBarcode(auxCode)) return auxCode;
  const supplierCode = source.supplierCode.trim();
  return isRealBarcode(supplierCode) ? supplierCode : "";
}

/** Precarga de una fila: todo lo que el XML ya sabe, nada inventado. */
export function buildInitialRow(
  source: PendingProductSource,
  barcodeTypes: string[],
): PendingProductRow {
  const barcode = proposedUnitBarcode(source);
  return {
    rowId: source.rowId,
    selected: true,
    mode: "create",
    sku: suggestSku(source.supplierCode),
    shortName: source.xmlDescription.trim().slice(0, 50),
    description: source.xmlDescription.trim().slice(0, 254),
    itemTypeId: "",
    brandId: "",
    categoryNodeId: "",
    defaultUomCode: "",
    barcode,
    // Tipo sugerido solo para un GTIN real; cualquier otro código lo tipifica el usuario.
    barcodeType: isRealBarcode(barcode) ? suggestBarcodeType(barcode, barcodeTypes) : "",
    saleVatCode: source.vatCode,
    // IVA compra/ICE: datos del XML (solo lectura), no decisión del Item.
    purchaseVatCode: source.vatCode,
    exciseTaxCode: source.iceCode,
    salePriceGross: null,
    presentationFactor: 1,
    presentationName: "",
    presentationUomCode: "",
    linkItemId: "",
    linkItemLabel: "",
    linkPackagingLevelId: "",
    parentRowId: "",
  };
}

/**
 * Campos que se pueden asignar en bloque — mismo SSOT de catálogos que la creación de Items. Solo
 * decisiones válidas para cualquier fila: el tipo de código de barras depende de cada presentación
 * y el IVA compra viene del XML.
 */
export type BulkAssignableField =
  | "mode"
  | "itemTypeId"
  | "brandId"
  | "categoryNodeId"
  | "defaultUomCode"
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
 * Contexto de precio de venta: tarifa de IVA por código (catálogo SRI) y precisión de precios de
 * la empresa (política de precisión, nunca un número fijo de decimales).
 */
export interface SalePricing {
  vatPercentByCode: ReadonlyMap<string, number>;
  priceDecimals: number;
}

/**
 * Redondeo del PVP sugerido. Hoy: precisión de precios de la empresa. Punto de extensión para una
 * futura política comercial (p. ej. terminar en .05/.99) sin cambiar el cálculo ni el contrato.
 */
export type PvpRounding = (gross: number) => number;

/** Tarifa de IVA de venta de la fila; `null` si aún no se eligió o no está en el catálogo. */
function saleVatPercent(row: PendingProductRow, pricing: SalePricing): number | null {
  return pricing.vatPercentByCode.get(row.saleVatCode) ?? null;
}

/**
 * Desglose del PVP final de la fila (SSOT `lib/salePrice`): sin IVA persistible, PVP efectivo y
 * utilidad/margen/recargo contra el costo neto por unidad base del XML.
 */
export function rowSalePrice(
  row: PendingProductRow,
  source: PendingProductSource | undefined,
  pricing: SalePricing,
): SalePriceBreakdown | null {
  const vat = saleVatPercent(row, pricing);
  if (row.salePriceGross == null || vat == null) return null;
  return salePriceFromGross(
    row.salePriceGross,
    vat,
    pricing.priceDecimals,
    source ? netUnitCost(source, row.presentationFactor) : null,
  );
}

/**
 * PVP final sugerido por margen sobre PRECIO (fórmula única de `lib/margin`): el margen se aplica al
 * precio sin IVA y el resultado se expresa como PVP final con IVA, editable por fila. Solo filas
 * seleccionadas que crean producto y tienen IVA de venta; las demás no cambian.
 */
export function applyMarginToSelected(
  rows: PendingProductRow[],
  sources: ReadonlyMap<string, PendingProductSource>,
  marginPct: number,
  pricing: SalePricing,
  roundPvp: PvpRounding = (gross) => roundToDecimals(gross, pricing.priceDecimals),
): PendingProductRow[] {
  return rows.map((row) => {
    const source = sources.get(row.rowId);
    const vat = saleVatPercent(row, pricing);
    if (!row.selected || row.mode !== "create" || !source || vat == null) return row;
    const net = calcPriceForMargin(netUnitCost(source, row.presentationFactor), marginPct);
    return net == null ? row : { ...row, salePriceGross: roundPvp(grossFromNet(net, vat)) };
  });
}

/** Fila seleccionada que crea producto sin precio sin IVA persistible (> 0 con la precisión de la empresa). */
export function lacksNetSalePrice(row: PendingProductRow, pricing: SalePricing): boolean {
  if (!row.selected || row.mode !== "create") return false;
  return !((rowSalePrice(row, undefined, pricing)?.net ?? 0) > 0);
}

export interface PendingRowIssue {
  field: string;
  message: string;
}

/**
 * Datos por completar de cada fila seleccionada, derivados de las mismas validaciones del envío
 * (schema del formulario + precio sin IVA); no es una regla alternativa. Clave = rowId.
 */
export function pendingRowIssues(
  rows: PendingProductRow[],
  pricing: SalePricing,
  noNetPriceMessage: string,
): Map<string, PendingRowIssue[]> {
  const issues = new Map<string, PendingRowIssue[]>();
  const add = (rowId: string, issue: PendingRowIssue) => {
    const current = issues.get(rowId) ?? [];
    if (!current.some((i) => i.field === issue.field && i.message === issue.message))
      issues.set(rowId, [...current, issue]);
  };
  const parsed = pendingProductsFormSchema.safeParse({ rows });
  if (!parsed.success)
    for (const issue of parsed.error.issues) {
      const [root, index, field] = issue.path;
      const row = root === "rows" && typeof index === "number" ? rows[index] : undefined;
      if (row) add(row.rowId, { field: String(field ?? ""), message: issue.message });
    }
  rows.forEach((row) => {
    if (lacksNetSalePrice(row, pricing) && !issues.get(row.rowId)?.some((i) => i.field === "salePriceGross"))
      add(row.rowId, { field: "salePriceGross", message: noNetPriceMessage });
  });
  return issues;
}

const optional = (value: string) => (value.trim() ? value.trim() : null);

/** Contrato del backend: productos nuevos (clave = fila que lo crea) + resolución de cada línea. */
export function buildResolveRequest(
  rows: PendingProductRow[],
  pricing: SalePricing,
): ResolveReceptionLinesRequest {
  const selected = rows.filter((r) => r.selected);
  const presentation = (r: PendingProductRow) => {
    const barcode = normalizePendingBarcode(r.barcode) || null;
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
        // BaseSalePrice SIEMPRE sin IVA, por unidad base, desde el PVP final ingresado.
        baseSalePrice: rowSalePrice(r, undefined, pricing)?.net ?? 0,
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

/**
 * Aplica al formulario de compra el estado final de líneas XML resueltas o corregidas por el
 * backend (producto, presentación, conversión a unidad base). Si el producto cambió, descarta el
 * contexto del producto anterior para que se vuelva a cargar. Las demás líneas no cambian.
 */
export function applyResolvedReceptionLines(
  lines: PurchaseLineFormValues[],
  resolvedLines: ResolvedReceptionLine[],
): PurchaseLineFormValues[] {
  const resolved = new Map(resolvedLines.map((r) => [r.purchaseReceptionLineId, r]));
  return lines.map((l) => {
    const r = l.purchaseReceptionLineId ? resolved.get(l.purchaseReceptionLineId) : undefined;
    if (!r) return l;
    return {
      ...l,
      itemId: r.itemId,
      itemMatchStatus: r.matchStatus,
      description: buildPurchaseItemLabel({ sku: r.itemSku, name: r.itemName }),
      packagingLevelId: r.packagingLevelId,
      uomCode: r.uomCode,
      baseUomCode: r.baseUomCode,
      conversionFactor: r.conversionFactor,
      quantityInBaseUom: l.quantity * r.conversionFactor,
      // FASE 5 PURCHASE-UX — la línea sigue en la unidad del documento del proveedor (quantity sin
      // cambiar): la presentación resuelta ES la que interpreta ese documento. Sin esto el origen
      // quedaba con el de la carga (p. ej. factor 1) y "50 SIXPACK × $6 = 300 u × $1" se veía como
      // diferencia con el proveedor.
      xmlOriginPackagingLevelId: r.packagingLevelId ?? undefined,
      xmlOriginFactor: r.conversionFactor,
      baseUnitPrice: undefined,
      context: l.itemId === r.itemId ? l.context : undefined,
      _readinessIssue: undefined,
    };
  });
}
