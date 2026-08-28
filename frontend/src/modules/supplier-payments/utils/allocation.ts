/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — distribución automática medio↔cuota ("waterfall"): recorre las
 * aplicaciones en orden, consumiendo la capacidad de cada medio secuencialmente hasta cubrirlas.
 * Garantiza, siempre que Σmedios === Σaplicaciones (ya validado por el schema antes de llamar
 * esto), que cada medio queda distribuido al 100% y cada aplicación cubierta al 100% — exactamente
 * el invariante que `SupplierPayment.Create` exige en el backend.
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
