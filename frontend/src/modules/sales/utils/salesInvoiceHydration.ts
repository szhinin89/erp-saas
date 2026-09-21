import type { SalesInvoiceDto } from "../api/salesService";
import type { SalesLineFormValues } from "../schemas/salesInvoiceSchema";

/**
 * Hidratación de las líneas de una factura guardada hacia el formulario (loadForEdit), extraída de
 * useSalesPage.ts para poder testearla directamente. Solo copia snapshots persistidos — nunca
 * recalcula ni consulta configuración actual.
 */
export function mapInvoiceLinesToFormValues(inv: SalesInvoiceDto): SalesLineFormValues[] {
  return inv.lines.map((l, i) => ({
    _key: i + 1,
    itemId: l.itemId,
    warehouseId: l.warehouseId,
    description: l.description,
    quantity: l.quantity,
    unitPrice: l.unitPrice,
    vatCode: l.vatCode,
    discountPct: l.discountPct,
    iceCode: l.iceCode,
    notes: l.notes,
    packagingLevelId: l.packagingLevelId,
    uomCode: l.uomCode,
    baseUomCode: l.baseUomCode,
    conversionFactor: l.conversionFactor,
    _sku: l.snapshotSku ?? undefined,
    _name: l.snapshotItemName ?? undefined,
    // El backend solo persiste warehouseId para ítems que controlan stock
    // (SalesLineBuilder) — su presencia es una señal segura de _tracksStock.
    _tracksStock: l.warehouseId != null,
    // SALES-HISTORICAL-PRICING-SNAPSHOT-01: snapshot histórico tal como quedó persistido en
    // el Draft — nunca recalculado aquí. Null cuando el backend no tuvo el dato disponible
    // al momento de vender (factura anterior a esta fase, o sin costo Kardex resuelto);
    // SalesInvoiceLineGridRow (modo readOnly) debe mostrar eso como "no disponible", nunca
    // fabricar un valor.
    _listPriceAtSale: l.listPriceAtSale,
    _priceListNameAtSale: l.priceListName,
    _pricingSourceAtSale: l.pricingSource,
    _discountDescriptionAtSale: l.discountDescription,
    _discountSourceAtSale: l.discountSource,
    // SALES-PRICING-UX-TRACEABILITY-07C — trazabilidad de selección persistida (07B/07B1).
    _priceListIdAtSale: l.priceListId,
    _selectionSourceAtSale: l.selectionSource ?? null,
    _traceabilityVersionAtSale: inv.pricingTraceabilityVersion ?? null,
    _warehouseNameAtSale: l.warehouseName,
    _unitCostAtSale: l.unitCostAtSale,
  }));
}
