import { useCallback, useState } from "react";
import {
  purchaseService,
  type PurchaseCostDistributionDto,
  type PurchaseInvoiceDto,
  type ReplaceCostDistributionRequest,
} from "../api/purchaseService";
import { formatApiRequestError, readApiErrorCode } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { useI18n } from "../../../i18n/i18n";
import { roundToTotalAmount } from "../utils/purchaseCalc";

/** Distribución en edición: identidad estable (se reutiliza en reintentos) + revisión esperada. */
export type CostDistributionDraft = ReplaceCostDistributionRequest & { id: string };

// Revisión obsoleta o cambio concurrente: la compra se recarga y el usuario vuelve a decidir.
const RELOAD_CODES = new Set(["PURCHASE_COST_DISTRIBUTION_STALE", "CONFLICT"]);

/**
 * Derivados de las distribuciones guardadas (sin prorratear nada): líneas origen o destino —que no se
 * eliminan ni se desglosan hasta editar o retirar su distribución—, costos externos (suman al total)
 * y transporte del XML redistribuido (ya incluido en el total).
 */
export function summarizeCostDistributions(distributions: PurchaseCostDistributionDto[] = []) {
  const sum = (rows: PurchaseCostDistributionDto[]) => roundToTotalAmount(rows.reduce((s, d) => s + d.amount, 0));
  return {
    lineIds: new Set(distributions.flatMap((d) => [...d.allocations.map((a) => a.lineId), ...(d.sourceLineId ? [d.sourceLineId] : [])])),
    externalTotal: sum(distributions.filter((d) => !d.sourceLineId)),
    xmlTransportTotal: sum(distributions.filter((d) => d.sourceLineId)),
  };
}

interface Options {
  editing: PurchaseInvoiceDto | null;
  isDraft: boolean;
  saving: boolean;
  hasUnsavedChanges: boolean;
  reload: (purchaseId: string) => Promise<void>;
}

/**
 * PURCHASE-COST-DISTRIBUTION-CANONICAL-01 — integración del borrador guardado con la única operación
 * oficial de distribución de costos (crear/reemplazar/retirar). No calcula prorrateos: los importes
 * por línea y los totales son siempre los devueltos por el servidor.
 */
export function usePurchaseCostDistributions({ editing, isDraft, saving, hasUnsavedChanges, reload }: Options) {
  const { t } = useI18n();
  const [modalDistributeCost, setModalOpen] = useState(false);
  const [distributionError, setDistributionError] = useState("");
  const [distributionBusy, setDistributionBusy] = useState(false);

  const distributeCostDisabledReason = !isDraft
    ? t("purchases.distributeCost.disabled.notEditable", "Solo puede distribuir flete/gasto en compras editables.")
    : !editing
      ? t("purchases.distributeCost.disabled.notSaved", "Guarde el borrador antes de distribuir flete/gasto.")
      : saving
        ? t("purchases.distributeCost.disabled.saving", "Espere a que termine la operación en curso.")
        : hasUnsavedChanges
          ? t("purchases.distributeCost.disabled.unsavedChanges", "Guarde los cambios antes de distribuir flete/gasto.")
          : editing.lines.length === 0
            ? t("purchases.distributeCost.disabled.noLines", "Agregue al menos un ítem antes de distribuir flete/gasto.")
            : null;

  const setModalDistributeCost = useCallback((open: boolean) => {
    if (open) setDistributionError("");
    setModalOpen(open);
  }, []);

  const run = useCallback(
    async (operation: (purchaseId: string) => Promise<PurchaseInvoiceDto>, success: string): Promise<boolean> => {
      if (!editing || distributeCostDisabledReason) return false;
      setDistributionBusy(true);
      setDistributionError("");
      try {
        await operation(editing.id);
        await reload(editing.id);
        message.success(success);
        return true;
      } catch (err) {
        setDistributionError(formatApiRequestError(err, {
          generic: t("purchases.errors.distributeCostFailed", "Error al distribuir el valor."),
        }));
        if (RELOAD_CODES.has(readApiErrorCode(err) ?? "")) await reload(editing.id);
        return false;
      } finally {
        setDistributionBusy(false);
      }
    },
    [editing, distributeCostDisabledReason, reload, t],
  );

  const saveCostDistribution = useCallback(
    ({ id, ...body }: CostDistributionDraft) =>
      run((purchaseId) => purchaseService.replaceCostDistribution(purchaseId, id, body),
        t("purchases.messages.costDistributed", "Valor distribuido correctamente.")),
    [run, t],
  );

  const removeCostDistribution = useCallback(
    (distribution: PurchaseCostDistributionDto) =>
      run((purchaseId) => purchaseService.removeCostDistribution(purchaseId, distribution.id, distribution.revision),
        t("purchases.messages.costDistributionRemoved", "Distribución retirada.")),
    [run, t],
  );

  return {
    modalDistributeCost,
    setModalDistributeCost,
    distributionError,
    distributionBusy,
    distributeCostDisabledReason,
    saveCostDistribution,
    removeCostDistribution,
  };
}
