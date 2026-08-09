import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";

type TFn = (key: string, fallback: string) => string;

export const PROCESSING_STATUS_VARIANT: Record<
  PurchaseReceptionItem["processingStatus"],
  "success" | "warning" | "error" | "neutral"
> = {
  PENDING: "neutral",
  PROCESSED: "success",
  PROCESSED_WITH_WARNINGS: "warning",
  FAILED: "error",
};

export function buildDocumentStatusLabel(
  t: TFn,
): Record<PurchaseReceptionItem["documentStatus"], string> {
  return {
    IMPORTED: t("purchases.reception.docStatus.imported", "Guardado"),
    VERIFIED: t("purchases.reception.docStatus.verified", "Verificado"),
    PROCESSED: t("purchases.reception.docStatus.processed", "Procesado"),
    CANCELLED: t("purchases.reception.docStatus.cancelled", "Anulado"),
  };
}

export function buildProcessingStatusLabel(
  t: TFn,
): Record<PurchaseReceptionItem["processingStatus"], string> {
  return {
    PENDING: t("purchases.reception.processing.pending", "Sin procesar"),
    PROCESSED: t("purchases.reception.processing.processed", "OK"),
    PROCESSED_WITH_WARNINGS: t(
      "purchases.reception.processing.warnings",
      "Con advertencias",
    ),
    FAILED: t("purchases.reception.processing.failed", "No interpretado"),
  };
}
