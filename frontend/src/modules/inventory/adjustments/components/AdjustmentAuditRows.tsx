import { useI18n } from "../../../../i18n/i18n";
import { ZHInfoRow } from "../../../../components/zh/ZHInfoRow";
import { ZHFieldLabel } from "../../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import type { StockAdjustmentDto } from "../types";

/**
 * Datos documentales/auditoría del ajuste, solo lectura y solo cuando existen: número asignado
 * (no existe antes de guardar), ejecución y anulación. Reutiliza `ZHInfoRow`/`ZHFieldLabel`/
 * `ZHDataValue` — mismo trío que ya usa el resumen de Transferencias.
 */
export function AdjustmentAuditRows({
  adjustment,
}: {
  adjustment: StockAdjustmentDto;
}) {
  const { t } = useI18n();

  return (
    <>
      <ZHInfoRow
        label={
          <ZHFieldLabel size="sm">
            {t("inventory.adjustments.fields.number", "N.º ajuste")}
          </ZHFieldLabel>
        }
        value={
          <ZHDataValue variant="code">{adjustment.adjustmentNumber}</ZHDataValue>
        }
      />
      {adjustment.executedAt && (
        <ZHInfoRow
          label={
            <ZHFieldLabel size="sm">
              {t("inventory.adjustments.fields.executedAt", "Ejecutado")}
            </ZHFieldLabel>
          }
          value={<ZHDataValue>{formatDateTime(adjustment.executedAt)}</ZHDataValue>}
        />
      )}
      {adjustment.cancelledAt && (
        <ZHInfoRow
          label={
            <ZHFieldLabel size="sm">
              {t("inventory.adjustments.fields.cancelledAt", "Anulado")}
            </ZHFieldLabel>
          }
          value={
            <ZHDataValue>
              {formatDateTime(adjustment.cancelledAt)} —{" "}
              {adjustment.cancelledReason ?? "—"}
            </ZHDataValue>
          }
        />
      )}
    </>
  );
}
