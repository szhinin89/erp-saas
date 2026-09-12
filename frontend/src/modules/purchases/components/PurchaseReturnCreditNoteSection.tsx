import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHMoneyValue } from "../../../components/zh/ZHMoneyValue";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatDate, formatDateTime, todayIso } from "../../../lib/formatters/dateFormatters";
import {
  linkSupplierCreditNoteSchema,
  emptyLinkSupplierCreditNoteForm,
  type LinkSupplierCreditNoteFormValues,
} from "../schemas/purchaseReturnSchema";
import { purchaseReturnService, type PurchaseReturnDto } from "../api/purchaseReturnService";
import {
  getPurchaseCreditNoteStatusLabel,
  PURCHASE_CREDIT_NOTE_STATUS_BADGE,
} from "../utils/purchaseCreditNoteStatus";

type Props = {
  purchaseReturn: PurchaseReturnDto;
  onLinked: (updated: PurchaseReturnDto) => void;
};

/**
 * Sección "Nota de Crédito del proveedor" del detalle de `PurchaseReturn`
 * (Fase 9/12). A diferencia de `SalesReturnCreditNoteSection.tsx` (que
 * consulta el documento electrónico que Ventas EMITE automáticamente vía
 * `ElectronicDocumentDiagnosticPanel`), esta sección resuelve un problema
 * distinto: el registro MANUAL de la Nota de Crédito que el PROVEEDOR emite y
 * envía por fuera del ERP — nunca un documento electrónico propio, sin XML/RIDE
 * que consultar. No hay componente existente que reutilizar para este flujo
 * (auditoría de reutilización, Fase 12) — formulario nuevo, RHF + Zod desde
 * el inicio (F-V1/F-V2).
 */
export function PurchaseReturnCreditNoteSection({ purchaseReturn, onLinked }: Props) {
  const navigate = useNavigate();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LinkSupplierCreditNoteFormValues>({
    resolver: zodResolver(linkSupplierCreditNoteSchema),
    defaultValues: { ...emptyLinkSupplierCreditNoteForm(), issueDate: todayIso() },
  });

  if (purchaseReturn.fiscalStatus === "SupplierCreditNoteRegistered") {
    // PURCHASE-RETURN-CREDIT-NOTE-DETAIL-ENRICHMENT-01 — número/clave/fecha/total legibles en
    // vez del Id crudo del documento; si por alguna razón no se pudo resolver ningún dato (p. ej.
    // el documento de recepción fue eliminado), se muestra un fallback claro — nunca "documento ."
    // vacío ni el GUID crudo.
    const hasReadableInfo = Boolean(purchaseReturn.supplierCreditNoteInvoiceNumber);
    return (
      <ZHCard title="Nota de Crédito del proveedor">
        {hasReadableInfo ? (
          <div className="sr-general-grid">
            <div>
              <span className="sr-general-grid__label">N.º NC proveedor</span>
              <span className="sr-general-grid__value">
                {purchaseReturn.supplierCreditNoteInvoiceNumber}
              </span>
            </div>
            <div>
              <span className="sr-general-grid__label">Clave de acceso</span>
              <span className="sr-general-grid__value">
                {purchaseReturn.supplierCreditNoteAccessKey ?? "—"}
              </span>
            </div>
            <div>
              <span className="sr-general-grid__label">Fecha de emisión</span>
              <span className="sr-general-grid__value">
                {formatDate(purchaseReturn.supplierCreditNoteIssueDate)}
              </span>
            </div>
            {purchaseReturn.supplierCreditNoteAuthorizationDate && (
              <div>
                <span className="sr-general-grid__label">Fecha de autorización</span>
                <span className="sr-general-grid__value">
                  {formatDateTime(purchaseReturn.supplierCreditNoteAuthorizationDate)}
                </span>
              </div>
            )}
            {purchaseReturn.supplierCreditNoteTotalAmount !== null && (
              <div>
                <span className="sr-general-grid__label">Total NC</span>
                <span className="sr-general-grid__value">
                  <ZHMoneyValue value={purchaseReturn.supplierCreditNoteTotalAmount} />
                </span>
              </div>
            )}
            <div>
              <span className="sr-general-grid__label">Factura afectada</span>
              <span className="sr-general-grid__value">
                {purchaseReturn.purchaseInvoiceNumber ?? purchaseReturn.purchaseInvoiceId}
              </span>
            </div>
            {purchaseReturn.linkedPurchaseCreditNoteId && (
              <div>
                <span className="sr-general-grid__label">NC de compra (interna)</span>
                <span className="sr-general-grid__value">
                  {purchaseReturn.linkedPurchaseCreditNoteStatus && (
                    <Badge
                      label={getPurchaseCreditNoteStatusLabel(
                        purchaseReturn.linkedPurchaseCreditNoteStatus,
                      )}
                      variant={
                        PURCHASE_CREDIT_NOTE_STATUS_BADGE[
                          purchaseReturn.linkedPurchaseCreditNoteStatus
                        ] ?? "gray"
                      }
                    />
                  )}
                  <ZHBtn
                    type="button"
                    variant="ghost"
                    onClick={() =>
                      navigate(`/purchases/credit-notes/${purchaseReturn.linkedPurchaseCreditNoteId}`)
                    }
                  >
                    Ver NC de compra
                  </ZHBtn>
                </span>
              </div>
            )}
          </div>
        ) : (
          <p className="sr-reason-readonly">
            {purchaseReturn.supplierCreditNoteDocumentId
              ? "Nota de Crédito vinculada — NC no encontrada."
              : "Sin nota de crédito vinculada."}
          </p>
        )}
      </ZHCard>
    );
  }

  if (purchaseReturn.fiscalStatus !== "PendingSupplierCreditNote") {
    return null;
  }

  const onSubmit = async (values: LinkSupplierCreditNoteFormValues) => {
    try {
      await purchaseReturnService.linkCreditNote(purchaseReturn.id, {
        ...values,
        clientRequestId: crypto.randomUUID(),
      });
      const updated = await purchaseReturnService.getById(purchaseReturn.id);
      onLinked(updated);
      message.success("Nota de Crédito vinculada correctamente.");
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => message.error(msg));
      if (!applied) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo vincular la Nota de Crédito.",
          }),
        );
      }
    }
  };

  return (
    <ZHCard title="Registrar Nota de Crédito del proveedor">
      <form onSubmit={(e) => void handleSubmit(onSubmit)(e)}>
        <div className="sr-general-grid">
          <ZHField label="Clave de acceso" required fieldError={errors.accessKey?.message}>
            <ZhTextInput className="zh-input" {...register("accessKey")} />
          </ZHField>
          <ZHField label="RUC/CI proveedor" required fieldError={errors.supplierRuc?.message}>
            <ZhTextInput className="zh-input" {...register("supplierRuc")} />
          </ZHField>
          <ZHField label="Nombre proveedor" required fieldError={errors.supplierName?.message}>
            <ZhTextInput className="zh-input" {...register("supplierName")} />
          </ZHField>
          <ZHField label="N.º comprobante" required fieldError={errors.invoiceNumber?.message}>
            <ZhTextInput className="zh-input" {...register("invoiceNumber")} />
          </ZHField>
          <ZHField label="Fecha de emisión" required fieldError={errors.issueDate?.message}>
            <ZhDateInput className="zh-input" {...register("issueDate")} />
          </ZHField>
          <ZHField label="Moneda" required fieldError={errors.currencyCode?.message}>
            <ZhTextInput className="zh-input" {...register("currencyCode")} />
          </ZHField>
          <ZHField label="Subtotal" required fieldError={errors.subtotal?.message}>
            <ZhDecimalInput decimals={2} {...register("subtotal")} />
          </ZHField>
          <ZHField label="IVA" required fieldError={errors.vatAmount?.message}>
            <ZhDecimalInput decimals={2} {...register("vatAmount")} />
          </ZHField>
          <ZHField label="Total" required fieldError={errors.totalAmount?.message}>
            <ZhDecimalInput decimals={2} positiveOnly {...register("totalAmount")} />
          </ZHField>
        </div>
        <ZHFormActions
          hideCancel
          hideDraft
          onSave={() => void handleSubmit(onSubmit)()}
          disableSave={isSubmitting}
          labels={{ save: isSubmitting ? "Vinculando..." : "Vincular Nota de Crédito" }}
        />
      </form>
    </ZHCard>
  );
}
