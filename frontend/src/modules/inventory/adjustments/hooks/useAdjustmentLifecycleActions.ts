import { useCallback, useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { readApiErrorMessage } from "../../../lib/apiError";
import { message } from "../../../../lib/messages";
import { stockAdjustmentsService } from "../api/stockAdjustmentsService";
import type { StockAdjustmentDto } from "../types";

export type AdjustmentLifecycleTarget = {
  id: string;
  adjustmentNumber: string;
} | null;

/**
 * INVENTORY-ADJUSTMENTS-03 — estado y ejecución de las dos transiciones de ciclo de vida
 * (Draft → Executed, Executed → Cancelled) compartidas por la lista y el editor: una sola
 * implementación de la confirmación, la llamada al servicio y el manejo de error, en vez de
 * repetirla en cada pantalla.
 *
 * Ejecutar/Anular no tienen campos de formulario a los que mapear un error de validación, así que
 * NO se usa `applyServerErrors` aquí: se expone el mensaje específico del backend tal cual llega
 * (`readApiErrorMessage` prioriza `data.errors` sobre el mensaje genérico del catálogo), p. ej.
 * "stock insuficiente" o "motivo inactivo". El texto genérico solo aparece si el backend no envió
 * ninguno.
 */
export function useAdjustmentLifecycleActions(onDone: (
  updated: StockAdjustmentDto,
) => void) {
  const { t } = useI18n();
  const [executeTarget, setExecuteTarget] =
    useState<AdjustmentLifecycleTarget>(null);
  const [cancelTarget, setCancelTarget] =
    useState<AdjustmentLifecycleTarget>(null);
  const [cancelReason, setCancelReason] = useState("");
  const [cancelReasonError, setCancelReasonError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const closeExecute = useCallback(() => setExecuteTarget(null), []);

  const closeCancel = useCallback(() => {
    setCancelTarget(null);
    setCancelReason("");
    setCancelReasonError(null);
  }, []);

  const confirmExecute = useCallback(async () => {
    if (!executeTarget) return;
    setActionError(null);
    setBusy(true);
    try {
      const updated = await stockAdjustmentsService.execute(executeTarget.id);
      setExecuteTarget(null);
      onDone(updated);
      message.success(
        t(
          "inventory.adjustments.messages.executed",
          "Ajuste ejecutado correctamente.",
        ),
      );
    } catch (err) {
      setActionError(
        readApiErrorMessage(err) ??
          t(
            "inventory.adjustments.messages.executeError",
            "No se pudo ejecutar el ajuste. Intente nuevamente.",
          ),
      );
      setExecuteTarget(null);
    } finally {
      setBusy(false);
    }
  }, [executeTarget, onDone, t]);

  const confirmCancel = useCallback(async () => {
    if (!cancelTarget) return;
    const reason = cancelReason.trim();
    if (!reason) {
      setCancelReasonError(
        t(
          "inventory.adjustments.messages.cancelReasonRequired",
          "Indique el motivo de la anulación.",
        ),
      );
      return;
    }
    setActionError(null);
    setCancelReasonError(null);
    setBusy(true);
    try {
      const updated = await stockAdjustmentsService.cancel(
        cancelTarget.id,
        reason,
      );
      closeCancel();
      onDone(updated);
      message.success(
        t(
          "inventory.adjustments.messages.cancelled",
          "Ajuste anulado correctamente.",
        ),
      );
    } catch (err) {
      setActionError(
        readApiErrorMessage(err) ??
          t(
            "inventory.adjustments.messages.cancelError",
            "No se pudo anular el ajuste. Intente nuevamente.",
          ),
      );
      closeCancel();
    } finally {
      setBusy(false);
    }
  }, [cancelTarget, cancelReason, closeCancel, onDone, t]);

  return {
    executeTarget,
    setExecuteTarget,
    closeExecute,
    confirmExecute,
    cancelTarget,
    setCancelTarget,
    closeCancel,
    confirmCancel,
    cancelReason,
    setCancelReason,
    cancelReasonError,
    busy,
    actionError,
    setActionError,
  };
}
