import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import type { AccountingCompleteness } from "../../api/accountingApi";

/**
 * ACCOUNTING-CONTINUITY-06 — etiqueta de cada código del readiness único de completitud contable.
 * Solo presentación: la decisión de completitud viene íntegra del backend (`completeness`), nunca se
 * recalcula aquí.
 */
const REASON_LABELS: Record<string, string> = {
  POSTINGS_UNRESOLVED: "Contabilizaciones pendientes o fallidas",
  OPENING_BATCHES_UNPOSTED: "Lotes de apertura sin asiento",
  OPENING_ASI_MISSING: "Asiento inicial (ASI) sin publicar",
  OPENING_ASI_UNRESOLVED: "Asiento inicial (ASI) pendiente o fallido",
  SALE_COST_PENDING: "Ventas con costo pendiente",
  ORIGINS_WITHOUT_ACCOUNTING: "Documentos sin contabilización",
};

type Props = {
  completeness: AccountingCompleteness | null;
};

/** Advertencia de reporte provisional cuando el alcance consultado tiene contabilidad incompleta. */
export function ReportCompletenessNotice({ completeness }: Props) {
  if (!completeness || completeness.isComplete) return null;
  const reasons = completeness.reasons
    .map((r) => `${REASON_LABELS[r.code] ?? r.code}: ${r.count}`)
    .join(" · ");
  return (
    <ZHPageNotice
      variant="warning"
      message={`Reporte provisional — contabilidad incompleta al ${formatDate(completeness.asOf)}.`}
      detail={reasons}
    />
  );
}
