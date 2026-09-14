import type {
  CardDetailInput,
  ChequeDetailInput,
  PaymentMethodDetailType,
  TransferDetailInput,
} from "../api/salesService";

/**
 * SALES-TRANSFER-PAYMENT-REFERENCE-PAYLOAD-01: deriva el valor que debe viajar en
 * `payments[].reference` a partir del detalle capturado en `PaymentDetailModal`. El backend
 * (`PaymentMethod.RequiresReference`) rechaza la emisión si el método exige referencia y llega
 * `null` — antes de este fix, `SalesPage.tsx` siempre enviaba `reference: null` sin importar el
 * comprobante/autorización/número de cheque que el usuario ya había capturado en el modal.
 */
export function deriveDetailReference(
  detailType: PaymentMethodDetailType,
  detail: {
    card?: CardDetailInput;
    transfer?: TransferDetailInput;
    cheque?: ChequeDetailInput;
  },
): string | null {
  switch (detailType) {
    case "Transfer":
      return detail.transfer?.receiptNumber?.trim() || null;
    case "Card":
      return detail.card?.authorizationCode?.trim() || null;
    case "Check":
      return detail.cheque?.chequeNumber?.trim() || null;
    default:
      return null;
  }
}
