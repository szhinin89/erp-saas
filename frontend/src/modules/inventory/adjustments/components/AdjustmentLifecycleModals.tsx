import { useI18n } from "../../../../i18n/i18n";
import { ZHConfirmModal } from "../../../../components/zh/ZHConfirmModal";
import { ZHField } from "../../../../components/zh/ZHForm";
import { ZhTextarea } from "../../../../components/zh/inputs/ZhTextarea";
import type { useAdjustmentLifecycleActions } from "../hooks/useAdjustmentLifecycleActions";

type Props = {
  lifecycle: ReturnType<typeof useAdjustmentLifecycleActions>;
};

/**
 * INVENTORY-ADJUSTMENTS-03 — los dos diálogos de confirmación del ciclo de vida, compartidos por
 * la lista y el editor. Reutiliza `ZHConfirmModal` (estándar único de confirmación del DS): el de
 * anulación aprovecha su prop `message: ReactNode` para recoger el motivo obligatorio con
 * `ZHField` + `ZhTextarea`, en vez de construir un diálogo propio.
 */
export function AdjustmentLifecycleModals({ lifecycle }: Props) {
  const { t } = useI18n();

  return (
    <>
      <ZHConfirmModal
        open={!!lifecycle.executeTarget}
        title={t("inventory.adjustments.execute.title", "Ejecutar ajuste")}
        message={
          <p className="zh-confirm-message">
            {t(
              "inventory.adjustments.execute.warning",
              "El ajuste afectará el stock y quedará registrado en Kardex. Esta acción no se puede deshacer.",
            )}{" "}
            <strong>{lifecycle.executeTarget?.adjustmentNumber}</strong>
          </p>
        }
        confirmLabel={t(
          "inventory.adjustments.execute.confirm",
          "Sí, ejecutar",
        )}
        cancelLabel={t("common.cancel", "Cancelar")}
        variant="warning"
        onConfirm={() => void lifecycle.confirmExecute()}
        onCancel={lifecycle.closeExecute}
      />

      <ZHConfirmModal
        open={!!lifecycle.cancelTarget}
        title={t("inventory.adjustments.cancel.title", "Anular ajuste")}
        message={
          <>
            <p className="zh-confirm-message">
              {t(
                "inventory.adjustments.cancel.warning",
                "Se revertirá el movimiento de stock del ajuste ejecutado.",
              )}{" "}
              <strong>{lifecycle.cancelTarget?.adjustmentNumber}</strong>
            </p>
            <ZHField
              label={t(
                "inventory.adjustments.cancel.reasonLabel",
                "Motivo de anulación",
              )}
              required
              error={lifecycle.cancelReasonError}
            >
              <ZhTextarea
                rows={3}
                value={lifecycle.cancelReason}
                onChange={(e) => lifecycle.setCancelReason(e.target.value)}
                maxLength={500}
                aria-required="true"
                aria-label={t(
                  "inventory.adjustments.cancel.reasonLabel",
                  "Motivo de anulación",
                )}
              />
            </ZHField>
          </>
        }
        confirmLabel={t("inventory.adjustments.cancel.confirm", "Sí, anular")}
        cancelLabel={t("common.cancel", "Cancelar")}
        variant="danger"
        onConfirm={() => void lifecycle.confirmCancel()}
        onCancel={lifecycle.closeCancel}
      />
    </>
  );
}
