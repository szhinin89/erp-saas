import type { SalesLineInput } from "../api/salesService";
import { getDecimalConfig } from "../../../lib/config/decimal.config";
import { normalizeOptionalCode } from "../../../lib/sanitizers";

export function lineGross(l: SalesLineInput): number {
  return l.quantity * l.unitPrice;
}

export function lineDiscountAmt(l: SalesLineInput): number {
  return lineGross(l) * ((l.discountPct ?? 0) / 100);
}

export function lineNet(l: SalesLineInput): number {
  return lineGross(l) - lineDiscountAmt(l);
}

export function calcLineTax(
  l: SalesLineInput,
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
): { vat: number; ice: number } {
  const net = lineNet(l);
  const iceRate = (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? 0;
  const ice = iceRate > 0 ? (net * iceRate) / 100 : 0;
  // La base imponible del IVA incluye el ICE cuando aplica (normativa SRI Ecuador)
  const taxableBase = net + ice;
  const vatRate = vatRates?.[l.vatCode] ?? 0;
  return { vat: (taxableBase * vatRate) / 100, ice };
}

/** SALES-PRESENTATIONS-03: cantidad ingresada por el usuario en la presentación seleccionada
 * (ej. "1" al vender 1 caja x12) convertida a unidad base (ej. "12") — la misma fórmula que
 * SalesInvoiceDetail.Create usa en backend (Quantity * ConversionFactor). Sin presentación,
 * conversionFactor es 1 y esto es un no-op (preserva el comportamiento actual). */
export function lineQuantityInBaseUom(line: {
  quantity: number;
  conversionFactor?: number;
}): number {
  return line.quantity * (line.conversionFactor ?? 1);
}

/** Advertencia preventiva de stock (UX únicamente): solo se activa cuando el frontend ya tiene
 * el dato de disponibilidad (`_stockQty`, snapshot tomado al agregar el ítem o cambiar de
 * bodega) — nunca infiere ni bloquea si ese dato no llegó a cargarse; en ese caso la única
 * fuente de verdad sigue siendo el backend (`AuthorizeSalesUseCases`), cuyo error se muestra
 * tal cual llega. No duplica la regla de stock, solo anticipa el mismo resultado en pantalla.
 * SALES-PRESENTATIONS-03: compara contra la cantidad en UNIDAD BASE (`lineQuantityInBaseUom`),
 * nunca contra `quantity` cruda — el stock disponible (`_stockQty`) siempre está en unidad base,
 * así que comparar contra "1 caja" en vez de "12 unidades" bloquearía/permitiría mal la venta. */
export function lineExceedsStock(line: {
  _tracksStock?: boolean;
  _stockQty?: number;
  quantity: number;
  conversionFactor?: number;
}): boolean {
  return (
    !!line._tracksStock &&
    line._stockQty != null &&
    lineQuantityInBaseUom(line) > line._stockQty
  );
}

/** Mensaje claro de stock insuficiente cuando la línea tiene una presentación con factor > 1
 * (ej. "1 CAJA equivale a 12 UNIT, disponible 10 UNIT") — sin presentación, cae al mensaje
 * simple de siempre ("Supera el disponible (X UDS)"). */
export function stockExceededMessage(line: {
  quantity: number;
  conversionFactor?: number;
  uomCode?: string;
  baseUomCode?: string;
  _stockQty?: number;
}): string {
  const stockQty = line._stockQty ?? 0;
  const factor = line.conversionFactor ?? 1;
  if (factor === 1 || !line.uomCode || !line.baseUomCode) {
    return `Supera el disponible (${stockQty} UDS)`;
  }
  const baseQty = lineQuantityInBaseUom(line);
  return (
    `Stock insuficiente: ${line.quantity} ${line.uomCode} equivale a ${baseQty} ${line.baseUomCode}, ` +
    `disponible ${stockQty} ${line.baseUomCode}.`
  );
}

/** SALES-PRESENTATIONS-03: "Equivale a X unidades" bajo el campo Cantidad — solo cuando la
 * presentación seleccionada realmente convierte a algo distinto de la cantidad ingresada
 * (conversionFactor != 1); con unidad base (factor 1) no aporta nada y no se muestra. */
export function presentationEquivalenceLabel(line: {
  quantity: number;
  conversionFactor?: number;
  baseUomCode?: string;
}): string | null {
  const factor = line.conversionFactor ?? 1;
  if (factor === 1 || !line.baseUomCode) return null;
  return `Equivale a ${lineQuantityInBaseUom(line)} ${line.baseUomCode}`;
}

/** SALES-PRESENTATIONS-03: precio sugerido al cambiar de presentación = precio unitario base
 * actual * factor de conversión (nunca una tabla de precios por presentación — PricingResolver
 * no se toca). El usuario puede seguir editando "Precio Facturado" libremente después. */
export function suggestedUnitPriceForPresentation(
  baseUnitPrice: number,
  conversionFactor: number,
): number {
  return baseUnitPrice * conversionFactor;
}

export type PresentationCandidate = {
  id: string;
  uomCode: string;
  baseQuantity: number;
};

/**
 * SALES-PRESENTATIONS-03 — resuelve la presentación por defecto al agregar un producto desde el
 * buscador: por defecto se vende en unidad base (comportamiento actual preservado, regla 5 de la
 * tarea), salvo que el texto buscado haya coincidido con el barcode de una presentación específica
 * (`matchedPackagingLevelId`, resuelto por el backend en InvoiceItemSearchRepository) — en ese caso
 * esa presentación se autoselecciona. IsSaleDefault deliberadamente no se usa (el backend tampoco
 * lo consume todavía — ver SalesLinePackagingResolver, SALES-PRESENTATIONS-02).
 */
export function resolveDefaultLinePresentation(item: {
  baseUomCode: string;
  packagingLevels: PresentationCandidate[];
  matchedPackagingLevelId?: string | null;
}): { packagingLevelId: string | null; uomCode: string; conversionFactor: number } {
  const matched = item.matchedPackagingLevelId
    ? item.packagingLevels.find((p) => p.id === item.matchedPackagingLevelId)
    : undefined;
  if (!matched) {
    return { packagingLevelId: null, uomCode: item.baseUomCode, conversionFactor: 1 };
  }
  return {
    packagingLevelId: matched.id,
    uomCode: matched.uomCode,
    conversionFactor: matched.baseQuantity,
  };
}

/**
 * SALES-PRESENTATIONS-03 — resuelve el cambio de presentación de una línea ya agregada (selector
 * ZhSelect en SalesProductCard): recalcula uomCode/conversionFactor y sugiere un nuevo Precio
 * Facturado (precio base * factor) a partir del precio base ya resuelto una única vez al agregar
 * el producto (`basePrice`, snapshot de `_pvp`) — nunca del `unitPrice` actual de la línea, que ya
 * puede estar escalado por una presentación anterior (evita doble multiplicación, regla 8).
 */
export function resolveLinePresentationChange(
  packagingLevelId: string,
  packagingLevels: PresentationCandidate[],
  baseUomCode: string,
  basePrice: number,
): { packagingLevelId: string | null; uomCode: string; conversionFactor: number; unitPrice: number } {
  const selected = packagingLevels.find((p) => p.id === packagingLevelId);
  const conversionFactor = selected?.baseQuantity ?? 1;
  const uomCode = selected?.uomCode ?? baseUomCode;
  return {
    packagingLevelId: packagingLevelId || null,
    uomCode,
    conversionFactor,
    unitPrice: suggestedUnitPriceForPresentation(basePrice, conversionFactor),
  };
}

export type StockBadgeVariant = "green" | "orange" | "red";
export type StockBadgeInfo = { label: string; variant: StockBadgeVariant };

/** Único punto de la clasificación visual de stock disponible (Disponible/Stock bajo/Sin
 * stock) — mismos umbrales que ya usaba la tarjeta de línea de factura (≤0 sin stock, ≤5 stock
 * bajo), reutilizados también por el buscador de productos para no duplicar el criterio. Es
 * solo presentación: no decide si se puede vender, eso lo sigue validando el backend. */
export function stockBadgeInfo(stockQty: number): StockBadgeInfo {
  if (stockQty <= 0) return { label: "Sin stock", variant: "red" };
  if (stockQty <= 5) return { label: "Stock bajo", variant: "orange" };
  return { label: "Disponible", variant: "green" };
}

/** "IVA 15%" (tal como lo entrega el backend en InvoiceItemSearchResultDto.vatDisplay) →
 * "IVA (15%)" para el desglose de precio del buscador — transformación de texto pura, no
 * reinterpreta ni recalcula la tasa. Si el texto no tiene el formato esperado (p. ej. "Sin
 * IVA"), lo devuelve sin cambios. */
export function parenthesizeRateLabel(display: string): string {
  const match = /^(.*?)\s*([\d.,]+%)$/.exec(display);
  if (!match) return display;
  return `${match[1].trim()} (${match[2]})`;
}

export type MergeCandidateLine = {
  itemId?: string | null;
  unitPrice: number;
  vatCode: string;
  iceCode?: string | null;
  warehouseId?: string | null;
  packagingLevelId?: string | null;
};

/**
 * Único punto de la condición conservadora de fusión "reescanear el mismo producto suma
 * cantidad en vez de crear otra línea": el ítem, precio, impuestos, bodega y presentación deben
 * coincidir exactamente, y la línea existente no debe tener ya un descuento manual aplicado.
 * Cualquier diferencia (p. ej. el cajero ya negoció precio/descuento distinto, o reescaneó el
 * barcode de una presentación distinta — SALES-PRESENTATIONS-03) no fusiona — crea una línea
 * separada para no perder esa intención. Devuelve -1 si no hay ninguna línea fusionable.
 */
export function findMergeableLineIndex<T extends MergeCandidateLine & { discountPct?: number | null }>(
  lines: T[],
  candidate: MergeCandidateLine,
): number {
  return lines.findIndex(
    (l) =>
      l.itemId === candidate.itemId &&
      l.unitPrice === candidate.unitPrice &&
      (l.discountPct ?? 0) === 0 &&
      l.vatCode === candidate.vatCode &&
      normalizeOptionalCode(l.iceCode ?? null) ===
        normalizeOptionalCode(candidate.iceCode ?? null) &&
      (l.warehouseId ?? null) === (candidate.warehouseId ?? null) &&
      (l.packagingLevelId ?? null) === (candidate.packagingLevelId ?? null),
  );
}

export type TaxBreakdownEntry = {
  label: string;
  rate: number;
  base: number;
  tax: number;
};

/** Único punto de formato de la etiqueta de IVA por tasa ("IVA 0%" / "IVA 15%") — usado por el
 * resumen de impuestos, el detalle de factura en modo solo-lectura y el badge por línea, para
 * no repetir la misma regla de formato en tres lugares. */
export function formatVatLabel(rate: number): string {
  return rate === 0 ? "IVA 0%" : `IVA ${rate}%`;
}

export function calcSummary(
  lines: SalesLineInput[],
  vatRates?: Record<string, number>,
  iceRates?: Record<string, number>,
) {
  const subtotal = lines.reduce((s, l) => s + lineGross(l), 0);
  const discount = lines.reduce((s, l) => s + lineDiscountAmt(l), 0);
  const netSubtotal = subtotal - discount;

  const byRate = new Map<number, { base: number; tax: number }>();
  let totalIce = 0;

  for (const l of lines) {
    const net = lineNet(l);
    const iceRate = (l.iceCode ? iceRates?.[l.iceCode] : undefined) ?? 0;
    const ice = iceRate > 0 ? (net * iceRate) / 100 : 0;
    totalIce += ice;
    const taxableBase = net + ice;
    const vatRate = vatRates?.[l.vatCode] ?? 0;
    const tax = (taxableBase * vatRate) / 100;
    const entry = byRate.get(vatRate) ?? { base: 0, tax: 0 };
    entry.base += taxableBase;
    entry.tax += tax;
    byRate.set(vatRate, entry);
  }

  const totalAmountDecimals = getDecimalConfig().totalAmount;
  const roundTotal = (v: number) => {
    const factor = 10 ** totalAmountDecimals;
    return Math.round(v * factor) / factor;
  };

  const taxBreakdown: TaxBreakdownEntry[] = Array.from(byRate.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([rate, v]) => ({
      label: formatVatLabel(rate),
      rate,
      base: roundTotal(v.base),
      tax: roundTotal(v.tax),
    }));

  const vat = taxBreakdown.reduce((s, e) => s + e.tax, 0);
  const ice = roundTotal(totalIce);
  const total = roundTotal(netSubtotal + vat + ice);

  return {
    subtotal: roundTotal(subtotal),
    discount: roundTotal(discount),
    netSubtotal: roundTotal(netSubtotal),
    vat,
    ice,
    total,
    taxBreakdown,
  };
}
