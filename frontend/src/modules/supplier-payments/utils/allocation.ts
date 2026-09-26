/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — distribución automática medio↔cuota ("waterfall"): recorre las
 * aplicaciones en orden, consumiendo la capacidad de cada medio secuencialmente hasta cubrirlas.
 * Garantiza, siempre que Σaplicaciones ≤ Σmedios (ya validado por el schema antes de llamar esto),
 * que cada aplicación queda cubierta al 100% y ningún medio se distribuye por encima de su monto —
 * exactamente el invariante que `SupplierPayment.Create` exige en el backend.
 * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — la capacidad que sobra en los últimos medios es el
 * remanente no aplicado (anticipo): nunca se distribuye a una cuota.
 *
 * Trabaja en centavos (enteros) para evitar arrastre de error de punto flotante al sumar/restar
 * fracciones decimales repetidamente — los montos que ve el usuario y los que se envían al backend
 * siempre pasan por `toCents`/`fromCents` una sola vez cada uno.
 */

export interface AllocationPreviewLine {
  methodLineIndex: number;
  applicationLineIndex: number;
  amount: number;
}

const toCents = (amount: number) => Math.round(amount * 100);
const fromCents = (cents: number) => cents / 100;

/**
 * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — mismo cálculo derivado que el backend
 * (`SupplierPayment.AppliedAmount`/`UnappliedAmount`), en centavos. Solo presentación: el backend
 * es la autoridad.
 */
export function computePaymentSplit(
  methodLines: readonly { amount: number }[],
  applicationLines: readonly { amountApplied: number }[],
): { total: number; applied: number; unapplied: number } {
  const total = methodLines.reduce((sum, l) => sum + toCents(l.amount || 0), 0);
  const applied = applicationLines.reduce((sum, l) => sum + toCents(l.amountApplied || 0), 0);
  return { total: fromCents(total), applied: fromCents(applied), unapplied: fromCents(total - applied) };
}

export function computeAutomaticAllocations(
  methodLines: readonly { amount: number }[],
  applicationLines: readonly { amountApplied: number }[],
): AllocationPreviewLine[] {
  const remainingByMethod = methodLines.map((l) => toCents(l.amount || 0));
  const allocations: AllocationPreviewLine[] = [];
  let methodIndex = 0;

  applicationLines.forEach((application, applicationIndex) => {
    let remainingApplication = toCents(application.amountApplied || 0);

    while (remainingApplication > 0 && methodIndex < remainingByMethod.length) {
      if (remainingByMethod[methodIndex] <= 0) {
        methodIndex += 1;
        continue;
      }
      const take = Math.min(remainingByMethod[methodIndex], remainingApplication);
      allocations.push({
        methodLineIndex: methodIndex,
        applicationLineIndex: applicationIndex,
        amount: fromCents(take),
      });
      remainingByMethod[methodIndex] -= take;
      remainingApplication -= take;
      if (remainingByMethod[methodIndex] === 0) methodIndex += 1;
    }
  });

  return allocations;
}
