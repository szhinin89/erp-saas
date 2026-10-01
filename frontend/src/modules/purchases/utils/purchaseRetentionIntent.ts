import type { RetentionPreviewDto } from "../api/purchaseService";
import type {
  IssueRetentionLineRequest,
  RetentionIntentRequest,
} from "../../retentions/facades/purchaseRetentionFacade";

/**
 * ZH-PURCHASE-RETENTION-CONFIRM-01 — estado de formulario de la retención de una compra en
 * borrador. Las líneas NO son editables: se precargan desde la vista previa del backend
 * (elegibilidad + montos calculados), que es la fuente de verdad; el usuario solo decide si la
 * retención se emite al confirmar, con qué punto de emisión y en qué fecha. El backend revalida
 * la elegibilidad al confirmar — el frontend nunca es autoridad.
 */
export interface PurchaseRetentionIntentState {
  appliesRetention: boolean;
  emissionPointId: string;
  issueDate: string;
}

export function emptyPurchaseRetentionIntent(issueDate: string): PurchaseRetentionIntentState {
  return { appliesRetention: false, emissionPointId: "", issueDate };
}

/** "IVA"/"RENTA" (vista previa, catálogo SRI) → "Vat"/"Income" (contrato de emisión). */
function toRetentionTaxType(taxType: string): IssueRetentionLineRequest["taxType"] {
  return taxType.toUpperCase() === "IVA" ? "Vat" : "Income";
}

/** Líneas precargadas desde la vista previa (solo las que retienen un monto). */
export function purchaseRetentionLines(
  preview: RetentionPreviewDto | null,
): IssueRetentionLineRequest[] {
  return (preview?.lines ?? [])
    .filter((l) => l.amountRetained > 0)
    .map((l) => ({
      taxType: toRetentionTaxType(l.taxType),
      retentionCode: l.retentionCode,
      baseAmount: l.taxableBase,
      retentionRate: l.retentionPct,
      retainedAmount: l.amountRetained,
      retentionCodeDescription: l.retentionCodeName,
    }));
}

/** La retención solo puede activarse si la vista previa propone al menos una línea. */
export function canApplyPurchaseRetention(preview: RetentionPreviewDto | null): boolean {
  return purchaseRetentionLines(preview).length > 0;
}

/** Sin intención siempre está completa; con intención exige líneas, punto de emisión y fecha. */
export function isPurchaseRetentionIntentComplete(
  preview: RetentionPreviewDto | null,
  state: PurchaseRetentionIntentState,
): boolean {
  if (!state.appliesRetention) return true;
  return canApplyPurchaseRetention(preview) && !!state.emissionPointId && !!state.issueDate;
}

/**
 * Intención a enviar en `purchaseService.confirm`. `undefined` = confirmar sin retención (mismo
 * comportamiento de siempre). Nunca incluye número de retención ni Tenant/Company/Branch.
 */
export function buildPurchaseRetentionIntent(
  preview: RetentionPreviewDto | null,
  state: PurchaseRetentionIntentState,
): RetentionIntentRequest | undefined {
  if (!state.appliesRetention || !isPurchaseRetentionIntentComplete(preview, state)) return undefined;
  return {
    appliesRetention: true,
    emissionPointId: state.emissionPointId,
    issueDate: state.issueDate,
    lines: purchaseRetentionLines(preview),
  };
}
