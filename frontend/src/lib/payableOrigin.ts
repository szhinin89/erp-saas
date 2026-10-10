/**
 * Etiqueta del origen de una cuenta por pagar (espejo de `AccountsPayableOriginType` del backend) —
 * contrato compartido: única fuente para Cuentas por pagar (Badge, selector de CxP) y para
 * "Saldos a favor de proveedores" (historial de aplicaciones), sin import cruzado entre módulos.
 */
const PAYABLE_ORIGIN_LABEL: Record<string, string> = {
  PurchaseInvoice: "Compra",
  ExpenseDocument: "Gasto",
  Manual: "Manual",
  InitialBalance: "Saldo inicial",
};

export function payableOriginLabel(originType: string): string {
  return PAYABLE_ORIGIN_LABEL[originType] ?? originType;
}
