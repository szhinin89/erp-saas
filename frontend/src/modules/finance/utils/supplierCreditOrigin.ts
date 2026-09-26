import type { SupplierCreditDto, SupplierCreditSourceType } from "../api/supplierCreditService";

/**
 * ZH-SUPPLIER-BALANCES-UX-02D-E — única fuente de la presentación del origen de un saldo a favor:
 * etiqueta de negocio ("Anticipo / pago mayor" / "Devolución de compra") y ruta del documento de
 * origen (el backend entrega solo Ids — las URLs son responsabilidad de la UI).
 */
const SOURCE_TYPE_LABEL: Record<SupplierCreditSourceType, string> = {
  PurchaseReturn: "Devolución de compra",
  SupplierPayment: "Anticipo / pago mayor",
};

const SOURCE_ROUTE: Record<SupplierCreditSourceType, (id: string) => string> = {
  PurchaseReturn: (id) => `/purchases/returns/${id}`,
  SupplierPayment: (id) => `/supplier-payments/${id}`,
};

export function supplierCreditSourceLabel(sourceType: SupplierCreditSourceType): string {
  return SOURCE_TYPE_LABEL[sourceType] ?? sourceType;
}

/** Ruta del documento de origen, o `null` si el tipo no tiene pantalla de detalle conocida. */
export function supplierCreditSourceRoute(
  credit: Pick<SupplierCreditDto, "sourceType" | "sourceDocumentId">,
): string | null {
  const build = SOURCE_ROUTE[credit.sourceType] as ((id: string) => string) | undefined;
  return build && credit.sourceDocumentId ? build(credit.sourceDocumentId) : null;
}
