import type { useI18n } from "../../../i18n/i18n";
import type { RetentionDocumentDto } from "../../retentions/facades/purchaseRetentionFacade";

type Translate = ReturnType<typeof useI18n>["t"];

/**
 * Message of the cancel-purchase prompt: local effects, the SRI scope of a local cancellation and
 * the retention consequence, all visible in the dialog. ZH-RETENTION-SRI-ANNULMENT-01: with the
 * retention already authorized by SRI the purchase is not cancelled here, so local effects that
 * will not happen are not announced.
 */
export function buildPurchaseCancelMessage(
  t: Translate,
  invoiceNumber: string,
  retention: Pick<RetentionDocumentDto, "status" | "electronicStatus" | "retentionNumber"> | null,
): string {
  const retentionNumber = retention?.retentionNumber ?? "";
  if (retention?.status === "Issued" && retention.electronicStatus === "Authorized")
    return `${t(
      "purchases.cancel.retentionAuthorizedWarning",
      "La retención de esta compra ya fue autorizada por el SRI: la compra no se anula aquí, primero debe anularse la retención ante el SRI.",
    )} (${retentionNumber})`;
  return [
    t("purchases.cancel.message", { invoiceNumber }),
    // ZH-RETENTION-CANCELLATION-LIFECYCLE-01 — the retention is cancelled only with its purchase
    // and is terminal: warned before confirming.
    retention?.status === "Issued"
      ? `${t(
          "purchases.cancel.retentionWarning",
          "También se anulará la retención emitida con esta compra y se revertirá su efecto en la cuenta por pagar y en contabilidad. Una retención anulada no puede volver a emitirse.",
        )} (${retentionNumber})`
      : "",
  ]
    .filter(Boolean)
    .join(" ");
}
