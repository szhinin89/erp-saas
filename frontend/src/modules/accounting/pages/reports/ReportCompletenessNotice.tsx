import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { formatDate } from "../../../../lib/formatters/dateFormatters";
import type { AccountingCompleteness } from "../../api/accountingApi";
import { completenessReasonLabel } from "../../labels/completenessReasonLabels";

type Props = {
  completeness: AccountingCompleteness | null;
};

/** Advertencia de reporte provisional cuando el alcance consultado tiene contabilidad incompleta. */
export function ReportCompletenessNotice({ completeness }: Props) {
  if (!completeness || completeness.isComplete) return null;
  const reasons = completeness.reasons
    .map((r) => `${completenessReasonLabel(r.code)}: ${r.count}`)
    .join(" · ");
  return (
    <ZHPageNotice
      variant="warning"
      message={`Reporte provisional — contabilidad incompleta al ${formatDate(completeness.asOf)}.`}
      detail={reasons}
    />
  );
}
