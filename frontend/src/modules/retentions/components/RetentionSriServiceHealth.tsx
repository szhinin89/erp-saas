import { ZHSriServiceHealth } from "../../../components/zh/ZHSriServiceHealth";
import type { RetentionElectronicStatus } from "../api/retentionsService";

/** Estados en los que la retención todavía depende de una respuesta del SRI: solo ahí la salud técnica aporta. */
const SRI_IN_FLIGHT_STATUSES: ReadonlySet<RetentionElectronicStatus> = new Set([
  "Pending",
  "Processing",
  "RequiresReconciliation",
]);

/**
 * Salud técnica SRI (Recepción/Autorización) junto al estado electrónico de una retención emitida
 * (Compra y Gasto). Es disponibilidad técnica del servicio, no el estado fiscal de la retención —
 * ese lo sigue mostrando `RetentionElectronicStatusBadge`.
 */
export function RetentionSriServiceHealth({
  status,
}: {
  status: RetentionElectronicStatus | null | undefined;
}) {
  if (!status || !SRI_IN_FLIGHT_STATUSES.has(status)) return null;
  return <ZHSriServiceHealth />;
}
