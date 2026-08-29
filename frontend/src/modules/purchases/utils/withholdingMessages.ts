import { formatMoneyWithSymbol } from "../../../lib/sanitizers";

/**
 * CRITICAL-CONFIRMATIONS-PURCHASES-EXPENSES-03: mensaje del modal "Emitir retención"
 * (PurchasesPage.tsx) extraído a función pura para poder probarlo sin montar la pantalla
 * completa. Mantiene el flujo existente de selección de punto de emisión — solo mejora el
 * resumen/advertencia mostrado antes de emitir (documento, proveedor, total retenido si está
 * disponible, impacto SRI/contable). No cambia cálculo de retenciones, payload ni endpoint.
 */
export function buildWithholdingIssueMessage(
  invoiceNumber: string,
  supplierName: string,
  totalRetained: number | null | undefined,
): string {
  const totalPart =
    totalRetained != null
      ? ` por un total retenido de ${formatMoneyWithSymbol(totalRetained)}`
      : "";
  return (
    `Vas a emitir una retención vinculada a la compra ${invoiceNumber} — ${supplierName}` +
    `${totalPart}. Esta acción genera un comprobante de retención con impacto tributario ` +
    `(SRI) y contable, vinculado a esta compra — ingresa el punto de emisión para continuar.`
  );
}
