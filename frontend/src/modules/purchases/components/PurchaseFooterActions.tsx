import { PurchaseActionReason } from "../components/PurchaseActionReason";
import { useNavigate } from "react-router-dom";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { useI18n } from "../../../i18n/i18n";
import { canDiscardDraft } from "../utils/purchaseDocumentState";
import type { usePurchasesPage } from "../hooks/usePurchasesPage";

type PurchasesCtx = ReturnType<typeof usePurchasesPage>;

export function PurchaseFooterActions({ ctx }: { ctx: PurchasesCtx }) {
  const { t } = useI18n();
  const navigate = useNavigate();
  return (

          <div className="pf-footer">
            <ZHBtn
              type="button"
              variant="secondary"
              onClick={() =>
                ctx.requestLeave(() => {
                  ctx.resetForm();
                  ctx.setTab("listado");
                })
              }
            >
              <span className="material-symbols-outlined zh-icon-md">
                arrow_back
              </span>
              {ctx.readOnly ? t("common.back", "Volver") : t("common.cancel", "Cancelar")}
            </ZHBtn>
            {/* FASE 5 PURCHASE-UX — una compra confirmada/anulada es solo lectura: sin Guardar. */}
            {!ctx.readOnly && (
            <ZHBtn
              id="purchase-save"
              type="button"
              variant="primary"
              onClick={ctx.handleSave}
              disabled={
                ctx.saving ||
                !ctx.formWatch.invoiceNumber.trim() ||
                !ctx.formWatch.supplierId.trim() ||
                !ctx.formWatch.docTypeCode.trim() ||
                !ctx.formWatch.issueDate ||
                !ctx.canUseSriDocTypes ||
                ctx.isDuplicateAccessKeyBlocking ||
                ctx.isSupplierInactiveBlocking ||
                ctx.hasLineSaveBlockers ||
                ctx.lines.length === 0
              }
            >
              <span className="material-symbols-outlined zh-icon-md">
                save
              </span>
              {ctx.saving
                ? t("common.saving", "Guardando...")
                : ctx.editing
                  ? t("purchases.actions.updateDraft", "Actualizar borrador")
                  : t("purchases.actions.saveDraft", "Guardar borrador")}
            </ZHBtn>
            )}
            {/* FASE 5 PURCHASE-UX — un borrador guardado se descarta (no se anula). "Cancelar" de
                arriba sigue siendo solo navegación. */}
            {canDiscardDraft(ctx.editing) && (
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => ctx.setModalDiscardDraft(true)}
                disabled={ctx.saving}
              >
                <span className="material-symbols-outlined zh-icon-md">delete</span>
                {t("purchases.actions.discardDraft", "Descartar borrador")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Draft" && (<>
              <ZHBtn
                type="button"
                variant="primary"
                onClick={() => ctx.setModalConfirm(true)}
                disabled={
                  ctx.fieldDisabled ||
                  ctx.pendingReceptionItems.length > 0 ||
                  ctx.hasLineReadinessBlockers ||
                  ctx.scheduleBlocksConfirm ||
                  ctx.hasUnsavedFormChanges
                }

              >
                <span className="material-symbols-outlined zh-icon-md">
                  check_circle
                </span>
                {t("purchases.actions.confirm", "Confirmar compra")}
              </ZHBtn>
              <PurchaseActionReason reason={ctx.pendingReceptionItems.length > 0
                    ? t(
                        "purchases.validation.resolvePendingItemsBeforeConfirm",
                        "Resuelva los productos pendientes de vinculación antes de confirmar.",
                      )
                    : ctx.hasLineReadinessBlockers
                      ? ctx.confirmBlockedTitle
                      : ctx.scheduleBlocksConfirm
                        ? ctx.scheduleMismatchConfirmMessage
                        : ctx.hasUnsavedFormChanges
                        ? ctx.unsavedChangesMessage
                        : undefined} action={t("purchases.actions.confirm", "Confirmar compra")} />
            </>)}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="secondary"
                title={t("purchases.creditNote.actions.openCreditNote", "Abrir nota de crédito")}
                onClick={() =>
                  navigate(`/purchases/credit-notes/new?invoiceId=${ctx.editing!.id}`)
                }
              >
                <span className="material-symbols-outlined zh-icon-md">
                  request_quote
                </span>
                {t("purchases.creditNote.actions.creditNoteButton", "Nota de crédito")}
              </ZHBtn>
            )}
            {ctx.editing && ctx.editing.status === "Confirmed" && (
              <ZHBtn
                type="button"
                variant="destructive"
                onClick={() => ctx.setModalCancelReason(true)}
                disabled={ctx.saving}
              >
                <span className="material-symbols-outlined zh-icon-md">
                  block
                </span>
                {t("purchases.actions.cancelPurchase", "Anular compra")}
              </ZHBtn>
            )}
          </div>

  );
}
