import type { SupplierCreditDto, SupplierCreditSourceType } from "../api/supplierCreditService";

/**
 * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — etiqueta del origen de un crédito de proveedor
 * ("Pago a proveedor 00000012" / "Devolución de compra 00000003"); nunca asume PurchaseReturn.
 */
const SOURCE_TYPE_LABEL: Record<SupplierCreditSourceType, string> = {
  PurchaseReturn: "Devolución de compra",
  SupplierPayment: "Pago a proveedor",
};

export function formatSupplierCreditOrigin(
  credit: Pick<SupplierCreditDto, "sourceType" | "sourceDocumentNumber">,
): string {
  const label = SOURCE_TYPE_LABEL[credit.sourceType] ?? credit.sourceType;
  return credit.sourceDocumentNumber ? `${label} ${credit.sourceDocumentNumber}` : label;
}
