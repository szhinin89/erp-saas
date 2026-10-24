import { useEffect, useId, useState } from "react";
import { useNavigate } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHDataValue } from "../../../components/zh/ZHDataValue";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { useI18n } from "../../../i18n/i18n";
import { usePrecisionPolicy } from "../../../hooks/usePrecisionPolicy";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";
import { buildPurchaseLinePresentation } from "../utils/purchaseLinePresentation";
import { buildPurchaseLineIssues } from "../utils/purchaseLineIssues";
import { purchaseFieldErrors } from "../utils/purchaseFieldErrors";
import { isPurchaseRetentionIntentComplete } from "../utils/purchaseRetentionIntent";
import { purchaseStatusBadge } from "../utils/purchaseDocumentState";
import { focusPurchaseTarget } from "../utils/purchaseFocus";

/** Composes existing ZH patterns. One form status, navigation summary and general rejection. */
export function PurchaseFormFeedback({ ctx }: { ctx: ReturnType<typeof usePurchasesPage> }) {
  const { t } = useI18n();
  const precision = usePrecisionPolicy();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [showReception, setShowReception] = useState(false);
  const id = useId();
  const errors = purchaseFieldErrors(ctx.errors);
  const rootLines = ctx.errors.lines?.message ?? ctx.errors.lines?.root?.message;
  const retentionIncomplete = !isPurchaseRetentionIntentComplete(ctx.whPreview, ctx.retentionIntent);
  const missingHeader = !ctx.formWatch.supplierId.trim() || !ctx.formWatch.docTypeCode.trim()
    || !ctx.formWatch.invoiceNumber.trim() || !ctx.formWatch.issueDate;
  const saveBlocked = ctx.hasLineSaveBlockers || ctx.isDuplicateAccessKeyBlocking || ctx.isSupplierInactiveBlocking
    || !ctx.canUseSriDocTypes || missingHeader || ctx.lines.length === 0 || errors.length > 0;
  // documentState already derives the Confirm blockers; only the retention intent travels apart.
  const confirmBlocked = ctx.documentState.kind === "draftIncomplete" || retentionIncomplete;
  const entries: { id: string; label: string }[] = [];
  if (!ctx.readOnly) {
    ctx.lines.forEach((line, index) => {
      const vm = buildPurchaseLinePresentation(line, precision, t, ctx.vatRatesMap, ctx.iceRatesMap);
      if (buildPurchaseLineIssues({ vm, t, readiness: ctx.lineReadinessByKey[line._key],
        hasFieldErrors: purchaseFieldErrors(ctx.errors.lines?.[index]).length > 0, lineLocked: true }).length > 0
        || (!!line.purchaseReceptionLineId && !line.itemId)) {
        entries.push({ id: `purchase-line-${line._key}`, label: `${t("purchases.feedback.line", { count: index + 1 })} · ${line.description || t("purchases.lines.noLinkedProduct", "Sin producto vinculado")}` });
      }
    });
    if (ctx.errors.supplierId || !ctx.formWatch.supplierId || ctx.isSupplierInactiveBlocking)
      entries.push({ id: "purchase-supplier", label: t("purchases.supplier.title", "Datos del proveedor") });
    if (!ctx.formWatch.docTypeCode || !ctx.formWatch.invoiceNumber || !ctx.formWatch.issueDate || ctx.sriDocTypesUnavailable
      || ctx.errors.docTypeCode || ctx.errors.invoiceNumber || ctx.errors.issueDate)
      entries.push({ id: "purchase-document", label: t("purchases.feedback.document", "Revisar documento") });
    if (ctx.isDuplicateAccessKeyBlocking) entries.push({ id: "purchase-duplicate-error", label: t("purchases.electronic.accessKey", "Clave de acceso") });
    if (rootLines || ctx.lines.length === 0) entries.push({ id: "purchase-lines", label: t("purchases.lines.title", "Detalle de productos") });
    if (ctx.scheduleBlocksConfirm) entries.push({ id: "purchase-schedule", label: t("purchases.feedback.schedule", "Revisar cuotas") });
    if (ctx.hasUnsavedFormChanges) entries.push({ id: "purchase-save", label: t("purchases.feedback.saveChanges", "Guardar cambios") });
    if (retentionIncomplete || ctx.whPreviewError) entries.push({ id: "purchase-retention", label: t("purchases.retention.title", "Retenciones") });
  }
  const specificError = (ctx.isDuplicateAccessKeyBlocking && ctx.saveError === ctx.duplicateAccessKeyTitle)
    || (ctx.isSupplierInactiveBlocking && ctx.saveError === ctx.supplierInactiveMessage)
    || (ctx.sriDocTypesUnavailable && ctx.saveError === t("purchases.validation.sriDocTypesUnavailableTitle"))
    || (ctx.hasLineReadinessBlockers && [ctx.lineReadinessBlockedTitle, ctx.confirmBlockedTitle].includes(ctx.saveError))
    || (ctx.hasUnsavedFormChanges && ctx.saveError === ctx.unsavedChangesMessage)
    || (ctx.scheduleBlocksConfirm && ctx.saveError === ctx.scheduleMismatchConfirmMessage)
    || (retentionIncomplete && ctx.saveError === t("purchases.retention.incompleteIntent", "Complete la retención (punto de emisión y fecha) o desactívela antes de confirmar."))
    || (!!rootLines && ctx.saveError === rootLines);
  const generalError = ctx.saveError && !specificError ? ctx.saveError : "";
  const generalDetails = [...new Set(ctx.saveErrorDetails)].filter(text => text && text !== generalError);
  useEffect(() => {
    if (!ctx.saveError) return;
    const target = generalError ? "purchase-form-error" : ctx.isDuplicateAccessKeyBlocking ? "purchase-duplicate-error"
      : ctx.isSupplierInactiveBlocking ? "purchase-supplier" : ctx.scheduleBlocksConfirm ? "purchase-schedule"
        : retentionIncomplete ? "purchase-retention" : ctx.sriDocTypesUnavailable ? "purchase-document" : "purchase-form-feedback";
    const frame = requestAnimationFrame(() => focusPurchaseTarget(target));
    return () => cancelAnimationFrame(frame);
  }, [ctx.saveError, generalError, ctx.isDuplicateAccessKeyBlocking, ctx.isSupplierInactiveBlocking, ctx.scheduleBlocksConfirm, ctx.sriDocTypesUnavailable, retentionIncomplete]);
  const status = ctx.editing ? purchaseStatusBadge(ctx.editing.status) : null;
  let statusText = t("purchases.feedback.ready", "Listo para confirmar");
  if (ctx.readOnly) statusText = t("purchases.feedback.readOnly", "Solo lectura");
  else if (saveBlocked) statusText = t("purchases.feedback.saveBlocked", "No permite guardar");
  else if (ctx.documentState.kind === "new") statusText = t("purchases.documentState.newDetail");
  else if (confirmBlocked) statusText = t("purchases.feedback.confirmBlocked", "Puede guardar · no confirmar");
  return <>
    {generalError && <div id="purchase-form-error" tabIndex={-1}>
      <ZHPageNotice variant="error" message={generalError} detail={generalDetails.join("\n")} className={generalDetails.length > 0 ? "pf-validation-summary" : undefined} />
    </div>}
    {ctx.isDuplicateAccessKeyBlocking && <div id="purchase-duplicate-error" tabIndex={-1}>
      <ZHPageNotice variant="error" message={ctx.duplicateAccessKeyTitle} detail={ctx.duplicateAccessKeyDetail} />
      {ctx.duplicateAccessKey?.purchaseId && <ZHBtn type="button" onClick={() => {
        const purchaseId = ctx.duplicateAccessKey?.purchaseId;
        if (purchaseId) ctx.requestLeave(() => { navigate(`/purchases?invoiceId=${purchaseId}`); void ctx.loadForEdit(purchaseId); });
      }}>{t("purchases.duplicate.viewExisting", "Ver compra existente")}</ZHBtn>}
    </div>}
    <div className="pf-doc-state" id="purchase-form-feedback" tabIndex={-1} aria-live="polite">
      <Badge variant={status?.variant ?? "neutral"} size="md" label={status ? t(status.key, status.fallback) : t("purchases.documentState.new", "Nueva compra · sin guardar")} />
      <ZHDataValue variant="muted">{statusText}</ZHDataValue>
      {!ctx.readOnly && ctx.hasUnsavedFormChanges && <Badge variant="warning" label={t("purchases.feedback.unsavedChanges", "Cambios sin guardar")} />}
      {ctx.readOnly && ctx.editing?.cancelledAt && <ZHDataValue variant="muted">{formatDateTime(ctx.editing.cancelledAt)} · {ctx.editing.cancelReason ?? t("purchases.notices.noReason", "Sin motivo")}</ZHDataValue>}
      {entries.length > 0 && <ZHBtn type="button" variant="secondary" size="sm" expanded={open} aria-controls={id} onClick={() => setOpen(value => !value)}>
        {t("purchases.feedback.issues", { count: entries.length })}
      </ZHBtn>}
      {ctx.receptionProcessingNotice && <ZHBtn type="button" variant="secondary" size="sm" expanded={showReception} aria-controls={`${id}-reception`} onClick={() => setShowReception(value => !value)}>
        {t("purchases.feedback.reception", "Advertencias de recepción")}
      </ZHBtn>}
      {ctx.retention?.annulment && <ZHBtn type="button" size="sm" onClick={() => focusPurchaseTarget("purchase-retention")}>{t("purchases.feedback.sriRequest", "Ver solicitud SRI")}</ZHBtn>}
    </div>
    {open && entries.length > 0 && <div id={id} className="pf-doc-state">
      {entries.map(entry => <ZHBtn key={entry.id} type="button" size="sm" onClick={() => focusPurchaseTarget(entry.id)}>{entry.label}</ZHBtn>)}
    </div>}
    {showReception && ctx.receptionProcessingNotice && <div id={`${id}-reception`}><ZHDataValue variant="muted">{ctx.receptionProcessingNotice}</ZHDataValue></div>}
  </>;
}
