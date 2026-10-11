import type { CreatePurchasePayload } from "../api/purchaseService";
import type { PurchaseInvoiceFormValues } from "../schemas/purchaseInvoiceSchema";
import { fromDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";
import { normalizeOptionalCode } from "../../../lib/sanitizers";

/**
 * Payload create/update de la compra a partir del formulario — única proyección de lo que
 * "Guardar borrador" persiste. Los campos runtime de la línea (context, xml*, itemMatchStatus…)
 * nunca viajan al API.
 */
export function buildPurchaseDraftPayload(data: PurchaseInvoiceFormValues): CreatePurchasePayload {
  return {
    supplierId: data.supplierId,
    docTypeCode: data.docTypeCode,
    invoiceNumber: data.invoiceNumber,
    issueDate: data.issueDate,
    lines: data.lines.map((l) => ({
      itemId: l.itemId,
      description: l.description,
      quantity: l.quantity,
      unitPrice: l.unitPrice,
      vatCode: l.vatCode,
      discountPct: l.discountPct ?? 0,
      iceCode: normalizeOptionalCode(l.iceCode),
      warehouseId: l.warehouseId,
      notes: l.notes,
      purchaseReceptionLineId: l.purchaseReceptionLineId ?? undefined,
      packagingLevelId: l.packagingLevelId ?? undefined,
      freightAllocated: l.freightAllocated ?? undefined,
      otherCostsAllocated: l.otherCostsAllocated ?? undefined,
    })),
    accessKey: normalizeOptionalCode(data.accessKey),
    authorizationNumber: normalizeOptionalCode(data.authorizationNumber),
    // ZH-TEMPORAL-CONTRACT-02: datetime-local = hora de Company.Timezone → UTC una sola vez.
    authorizationDate: fromDateTimeLocalInputValue(data.authorizationDate),
    taxSupportCode: normalizeOptionalCode(data.taxSupportCode),
    sriPaymentMethodCode: normalizeOptionalCode(data.sriPaymentMethodCode),
    globalWarehouseId: data.globalWarehouseId || null,
    freightCost: data.freightCost,
    otherCosts: data.otherCosts,
    dueDate: data.dueDate || null,
    notes: data.notes || null,
    paymentTermId: data.paymentTermId || null,
  };
}

const toNumber = (v: unknown) => (v == null || v === "" ? 0 : Number(v));

/**
 * PILOT-PURCHASE-UI-FIX-01 — huella de lo persistible del borrador. Se compara contra la huella
 * tomada al cargar el borrador para saber si hay cambios locales sin guardar. No usa
 * formState.isDirty: las líneas se editan con setValue sin shouldDirty y los campos runtime
 * (context, itemMatchStatus) cambian sin que el usuario edite nada.
 */
export function buildPurchaseDraftSnapshot(data: PurchaseInvoiceFormValues): string {
  const payload = buildPurchaseDraftPayload(data);
  return JSON.stringify({
    ...payload,
    freightCost: toNumber(payload.freightCost),
    otherCosts: toNumber(payload.otherCosts),
    lines: payload.lines.map((l) => ({
      ...l,
      itemId: l.itemId ?? null,
      warehouseId: l.warehouseId || null,
      notes: l.notes || null,
      quantity: toNumber(l.quantity),
      unitPrice: toNumber(l.unitPrice),
      discountPct: toNumber(l.discountPct),
      freightAllocated: toNumber(l.freightAllocated),
      otherCostsAllocated: toNumber(l.otherCostsAllocated),
    })),
  });
}
