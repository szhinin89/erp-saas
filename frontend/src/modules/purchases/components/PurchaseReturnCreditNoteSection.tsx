import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import {
  linkSupplierCreditNoteSchema,
  emptyLinkSupplierCreditNoteForm,
  type LinkSupplierCreditNoteFormValues,
} from "../schemas/purchaseReturnSchema";
import { purchaseReturnService, type PurchaseReturnDto } from "../api/purchaseReturnService";

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
    return (
      <ZHCard title="Nota de Crédito del proveedor">
        <p className="sr-reason-readonly">
          Nota de Crédito vinculada — documento{" "}
          {purchaseReturn.supplierCreditNoteDocumentId}.
        </p>
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
