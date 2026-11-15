import { PurchaseLineBreakdownModal } from "../components/PurchaseLineBreakdownModal";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZHFieldLabel } from "../../../components/zh/ZHFieldLabel";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHInfoRow } from "../../../components/zh/ZHInfoRow";
import { DistributeCostModal } from "../components/DistributeCostModal";
import { ResolvePendingProductsModal } from "../components/ResolvePendingProductsModal";
import { usePrecisionDecimals, usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { ZHConfirmModal, ZHPromptModal } from "../../../components/zh/ZHConfirmModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { buildConfirmChecklist, buildConfirmCommercialRisk, buildConfirmLineWarnings } from "../utils/purchaseConfirmChecklist";
import { buildPurchaseCancelMessage } from "../utils/purchaseCancelMessage";
import { PurchaseConfirmChecklist } from "../components/PurchaseConfirmChecklist";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseDialogs({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  const moneyDecimals = usePrecisionDecimals("money");
  const xmlConfirmChecklist = buildConfirmChecklist(
    ctx.lines,
    ctx.editing?.lines ?? [],
    ctx.vatRatesMap,
    ctx.iceRatesMap,
    t,
    moneyDecimals,
  );
  const precisionPolicy = usePrecisionPolicy();
  // Solo se calcula con el modal abierto; mismas incidencias que las tarjetas de línea.
  const confirmLineWarnings = ctx.modalConfirm
    ? buildConfirmLineWarnings(ctx.lines, ctx.lineReadinessByKey, precisionPolicy, ctx.vatRatesMap, ctx.iceRatesMap, t)
    : [];
  const confirmCommercialRisk = ctx.modalConfirm
    ? buildConfirmCommercialRisk(ctx.lines, ctx.lineReadinessByKey, t)
    : null;
  return (
<>
      <ResolvePendingProductsModal
        open={ctx.modalPendingProducts}
        sources={ctx.modalPendingProductSources}
        supplierName={ctx.supplierProfile?.name ?? ""}
        supplierId={ctx.formWatch.supplierId || undefined}
        onClose={ctx.closePendingProducts}
        onResolved={(result) => void ctx.applyResolvedLines(result)}
      />
      {ctx.breakdownSourceId && ctx.lines.find(l => l.purchaseReceptionLineId === ctx.breakdownSourceId)?.xmlSource && (
        <PurchaseLineBreakdownModal
          key={ctx.breakdownSourceId}
          source={ctx.lines.find(l => l.purchaseReceptionLineId === ctx.breakdownSourceId)!}
          lines={ctx.lines}
          supplierId={ctx.formWatch.supplierId}
          warehouseId={ctx.formWatch.globalWarehouseId}
          onCancel={() => ctx.setBreakdownSourceId(null)}
          onApply={rows => ctx.applyBreakdown(ctx.breakdownSourceId!, rows)}
        />
      )}
      <DistributeCostModal
        error={ctx.distributionError}
        open={ctx.modalDistributeCost}
        busy={ctx.distributionBusy}
        invoice={ctx.editing}
        formLines={ctx.lines}
        onCancel={() => ctx.setModalDistributeCost(false)}
        onSave={ctx.saveCostDistribution}
        onRemove={ctx.removeCostDistribution}
      />

      <ZHConfirmModal
        open={ctx.modalConfirm}
        variant="warning"
        title={t("purchases.confirm.title", "Confirmar compra")}
        message={
          <>
            {ctx.editing && (
              <PurchaseConfirmSummary
                supplierName={ctx.supplierProfile?.name ?? ctx.editing.supplierName}
                invoiceNumber={ctx.editing.invoiceNumber}
                issueDate={ctx.editing.issueDate}
                lineCount={ctx.editing.lines.length}
                total={ctx.editing.grandTotal}
              />
            )}
            <PurchaseConfirmChecklist summary={xmlConfirmChecklist} lineWarnings={confirmLineWarnings}
              commercialRisk={confirmCommercialRisk} />
            {ctx.retentionIntent.appliesRetention && ctx.whPreview && (
              <ZHPageNotice
                variant="info"
                icon="receipt_long"
                message={t(
                  "purchases.confirm.withRetention",
                  "Se emitirá la retención junto con la compra, en una sola operación.",
                )}
                detail={`${t("purchases.retention.totalToWithhold", "Total a retener")}: ${formatMoneyWithSymbol(ctx.whPreview.totalRetained, moneyDecimals)}`}
              />
            )}
          </>
        }
        confirmLabel={t("common.confirm", "Confirmar")}
        onCancel={() => ctx.setModalConfirm(false)}
        onConfirm={ctx.handleConfirm}
      />

      <ZHPromptModal
        open={ctx.modalCancelReason}
        variant="danger"
        title={t("purchases.cancel.title", "Anular compra")}
        message={
          ctx.editing
            ? buildPurchaseCancelMessage(t, ctx.editing.invoiceNumber, ctx.retention)
            : ""
        }
        label={t("purchases.cancel.reasonLabel", "Motivo de anulación")}
        placeholder={t("purchases.cancel.reasonPlaceholder", "Ingrese el motivo...")}
        confirmLabel={t("purchases.cancel.confirm", "Anular")}
        onCancel={() => ctx.setModalCancelReason(false)}
        onConfirm={ctx.handleCancel}
      />

      {/* FASE 5 PURCHASE-UX — descartar borrador: soft, sin reversos, motivo opcional. */}
      <ZHPromptModal
        open={ctx.modalDiscardDraft}
        variant="danger"
        required={false}
        title={t("purchases.discardDraft.title", "Descartar borrador")}
        message={
          ctx.editing
            ? t("purchases.discardDraft.message", { invoiceNumber: ctx.editing.invoiceNumber })
            : ""
        }
        label={t("purchases.discardDraft.reasonLabel", "Motivo (opcional)")}
        placeholder={t("purchases.discardDraft.reasonPlaceholder", "Ej.: cargado por error, duplicado...")}
        confirmLabel={t("purchases.discardDraft.confirm", "Descartar borrador")}
        onCancel={() => ctx.setModalDiscardDraft(false)}
        onConfirm={(reason) => void ctx.handleDiscardDraft(reason)}
      />

      {/* PURCHASE-DRAFT-RECOVERY D3 — salir con cambios sin guardar exige confirmación explícita. */}
      <ZHConfirmModal
        open={ctx.pendingLeave}
        variant="warning"
        title={t("purchases.draftRecovery.leaveTitle", "Hay cambios sin guardar")}
        message={t(
          "purchases.draftRecovery.leaveMessage",
          "Si sale ahora se perderán los cambios que no guardó en el borrador. Use \"Guardar borrador\" para conservarlos.",
        )}
        confirmLabel={t("purchases.draftRecovery.leaveConfirm", "Descartar cambios")}
        cancelLabel={t("purchases.draftRecovery.leaveCancel", "Seguir editando")}
        onCancel={ctx.cancelLeave}
        onConfirm={ctx.confirmLeave}
      />

      {/* ZH-RETENTION-SRI-ANNULMENT-01 — la retención ya está autorizada por el SRI. */}
      <ZHConfirmModal
        open={ctx.sriAnnulmentReason !== null}
        variant="warning"
        title={t("purchases.sriAnnulment.title", "La retención ya fue autorizada por el SRI")}
        message={t(
          "purchases.sriAnnulment.message",
          "La retención ya fue autorizada por el SRI y debe anularse primero ante el SRI. Se registrará la solicitud con el motivo indicado; la compra seguirá vigente (no se anula, no se revierte la cuenta por pagar ni la contabilidad) hasta que el SRI informe ANULADO (el ERP lo verifica automáticamente en el SRI).",
        )}
        confirmLabel={t("purchases.sriAnnulment.start", "Iniciar proceso de anulación")}
        cancelLabel={t("common.cancel", "Cancelar")}
        onCancel={() => ctx.setSriAnnulmentReason(null)}
        onConfirm={() => void ctx.handleStartSriAnnulment()}
      />
</>
  );
}

/** FASE 5 PURCHASE-UX — identifica la compra antes de confirmarla (evita confirmar la equivocada). */
function PurchaseConfirmSummary({
  supplierName,
  invoiceNumber,
  issueDate,
  lineCount,
  total,
}: {
  supplierName: string;
  invoiceNumber: string;
  issueDate: string;
  lineCount: number;
  total: number;
}) {
  const { t } = useI18n();
  return (
    <div className="pf-confirm-summary">
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.supplier", "Proveedor")}</ZHFieldLabel>}
        value={<ZHDataValue variant="strong">{supplierName}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.number", "Número")}</ZHFieldLabel>}
        value={<ZHDataValue variant="code">{invoiceNumber}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.date", "Fecha")}</ZHFieldLabel>}
        value={<ZHDataValue variant="numeric">{formatDate(issueDate)}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.lines", "Líneas")}</ZHFieldLabel>}
        value={<ZHDataValue variant="numeric">{lineCount}</ZHDataValue>}
      />
      <ZHInfoRow
        wide
        label={<ZHFieldLabel size="sm">{t("purchases.confirm.summary.total", "Total")}</ZHFieldLabel>}
        value={<ZHMoneyValue value={total} precision="money" emphasis="strong" />}
      />
    </div>
  );
}
