/**
 * POS-EMISSION-TYPE-SNAPSHOT-01 — tipo de emisión EFECTIVO de la venta en pantalla.
 *
 * - Venta nueva (sin documento): CashSession → EmissionPoint → EmissionType, resuelto en vivo por
 *   el backend en `myCashSession.emissionType`.
 * - Venta ya creada (borrador, autorizada, anulada, histórica): manda SIEMPRE el snapshot
 *   inmutable `SalesInvoice.EmissionType` — el mismo que usa el backend para emitir
 *   (AuthorizeSalesInvoiceHandler), el XML, el RIDE y la tirilla. La caja actual nunca
 *   reinterpreta una factura existente.
 */
export function resolveSalesEmissionType(
  editing: { emissionType: string } | null | undefined,
  session: { emissionType: string | null } | null | undefined,
): string | null {
  if (editing) return editing.emissionType || null;
  return session?.emissionType ?? null;
}
