import type { CreatePurchasePayload } from "../api/purchaseService";
import type { PurchaseRetentionIntentState } from "./purchaseRetentionIntent";
import type { PurchaseInvoiceFormValues } from "../schemas/purchaseInvoiceSchema";
import { fromDateTimeLocalInputValue } from "../../../lib/formatters/dateFormatters";
import { normalizeOptionalCode } from "../../../lib/sanitizers";
import {
  hasSupplierDocumentOrigin,
  resolveLinePersistence,
} from "./purchaseLinePresentation";

/** Fila de cuota tal como la edita el formulario (ScheduleRow del hook). */
export interface PurchaseDraftScheduleRow {
  number: number;
  dueDate: string;
  amount: number;
  notes: string;
}

/**
 * PURCHASE-DRAFT-RECOVERY F2/F3 — estado del borrador que vive fuera de react-hook-form pero que
 * "Guardar borrador" también persiste. `manualSchedule` null = cuotas generadas por la condición
 * de pago (no se guardan; el backend las descarta con []).
 */
export interface PurchaseDraftExtras {
  manualSchedule: PurchaseDraftScheduleRow[] | null;
  retentionIntent: PurchaseRetentionIntentState;
}

/**
 * Payload create/update de la compra a partir del formulario — única proyección de lo que
 * "Guardar borrador" persiste. Los campos runtime de la línea (context, xml*, itemMatchStatus…)
 * nunca viajan al API. Sin `extras`, cuotas e intención de retención no viajan (el backend no
 * las toca).
 */
export function buildPurchaseDraftPayload(
  data: PurchaseInvoiceFormValues,
  extras?: PurchaseDraftExtras,
  // FASE 5 PURCHASE-UX — con la escala del precio de compra, una línea manual o XML editada con
  // presentación se persiste en la presentación solo si es exacta; si no, en unidad base (sin
  // round-trip — ver resolveLinePersistence). Sin ella (huella de cambios sin guardar) la línea
  // viaja tal cual está en el formulario.
  precision?: { purchaseUnitPriceDecimals: number },
): CreatePurchasePayload {
  return {
    supplierId: data.supplierId,
    docTypeCode: data.docTypeCode,
    invoiceNumber: data.invoiceNumber,
    issueDate: data.issueDate,
    lines: data.lines.map((l) => {
      const persisted = precision
        ? resolveLinePersistence(l, precision.purchaseUnitPriceDecimals)
        : null;
      return {
      itemId: l.itemId,
      description: l.description,
      quantity: persisted ? persisted.quantity : l.quantity,
      unitPrice: persisted ? persisted.unitPrice : l.unitPrice,
      vatCode: l.vatCode,
      discountPct: l.discountPct ?? 0,
      iceCode: normalizeOptionalCode(l.iceCode),
      warehouseId: l.warehouseId,
      notes: l.notes,
      purchaseReceptionLineId: l.purchaseReceptionLineId ?? undefined,
      packagingLevelId: persisted ? persisted.packagingLevelId : (l.packagingLevelId ?? undefined),
      // FASE 5 PURCHASE-UX — la presentación de origen XML se persiste (sobrevive reabrir sin volver
      // a depender de la recepción). Solo líneas de recepción.
      xmlOriginPackagingLevelId: hasSupplierDocumentOrigin(l)
        ? (l.xmlOriginPackagingLevelId ?? undefined)
        : undefined,
      freightAllocated: l.freightAllocated ?? undefined,
      otherCostsAllocated: l.otherCostsAllocated ?? undefined,
      };
    }),
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
    ...(extras ? buildPurchaseDraftExtrasPayload(extras) : {}),
  };
}

function buildPurchaseDraftExtrasPayload(
  extras: PurchaseDraftExtras,
): Required<Pick<CreatePurchasePayload, "schedule" | "retentionIntent">> {
  return {
    schedule: (extras.manualSchedule ?? []).map((r) => ({
      installmentNumber: r.number,
      dueDate: r.dueDate,
      amount: r.amount,
      notes: r.notes || null,
    })),
    retentionIntent: {
      appliesRetention: extras.retentionIntent.appliesRetention,
      emissionPointId: extras.retentionIntent.emissionPointId || null,
      issueDate: extras.retentionIntent.issueDate || null,
    },
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

/**
 * PURCHASE-DRAFT-RECOVERY D3 — huella de cuotas manuales + intención de retención. Va aparte de la
 * huella del formulario: ambas viajan también en el body de Confirmar (no la bloquean), pero sí
 * cuentan como cambios sin guardar al salir o al ejecutar acciones que recargan el borrador.
 */
export function buildPurchaseDraftExtrasSnapshot(extras: PurchaseDraftExtras): string {
  const payload = buildPurchaseDraftExtrasPayload(extras);
  return JSON.stringify({
    ...payload,
    schedule: payload.schedule.map((r) => ({ ...r, amount: toNumber(r.amount) })),
  });
}
