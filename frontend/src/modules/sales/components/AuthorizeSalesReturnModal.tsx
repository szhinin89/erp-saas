import { useEffect, useRef, useState } from "react";
import { useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions, ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import { formatMoney } from "../../../lib/sanitizers";
import {
  buildAuthorizeSalesReturnSchema,
  type AuthorizeSalesReturnFormValues,
} from "../schemas/salesReturnSchema";
import {
  salesReturnService,
  type SalesReturnDto,
  type SalesReturnRefundMethod,
} from "../api/salesReturnService";

interface Props {
  open: boolean;
  salesReturn: SalesReturnDto | null;
  onClose: () => void;
  onAuthorized: (updated: SalesReturnDto) => void;
}

const METHOD_OPTIONS: { value: SalesReturnRefundMethod; label: string }[] = [
  { value: "Cash", label: "Efectivo (Caja)" },
  { value: "ReceivableCredit", label: "Crédito a Cuenta por Cobrar" },
];

/**
 * Captura la asignación explícita de reembolso (decisión de producto: sin
 * prorrateo automático) y llama exclusivamente
 * `POST /api/v1/sales/returns/{id}/authorize`. La validación real de
 * Σ Amount == GrandTotal la hace el dominio en el backend — este modal solo
 * anticipa el mismo chequeo para dar feedback inmediato (misma filosofía que
 * RegisterCollectionModal en Finance).
 */
export function AuthorizeSalesReturnModal({
  open,
  salesReturn,
  onClose,
  onAuthorized,
}: Props) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const submittingRef = useRef(false);

  const grandTotal = salesReturn?.grandTotal ?? 0;
  const {
    control,
    register,
    handleSubmit,
    reset,
    setError,
    watch,
    formState: { errors },
  } = useForm<AuthorizeSalesReturnFormValues>({
    resolver: zodResolver(buildAuthorizeSalesReturnSchema(grandTotal)),
    defaultValues: { refundAllocations: [{ method: "Cash", amount: grandTotal }] },
  });
  const { fields, append, remove } = useFieldArray({
    control,
    name: "refundAllocations",
  });

  useEffect(() => {
    if (!open || !salesReturn) return;
    reset({
      refundAllocations: [{ method: "Cash", amount: salesReturn.grandTotal }],
    });
    setSubmitError("");
  }, [open, salesReturn, reset]);

  const allocations = watch("refundAllocations");
  const sum = (allocations ?? []).reduce((acc, a) => acc + (Number(a.amount) || 0), 0);
  const remaining = grandTotal - sum;

  const handleClose = () => {
    if (saving) return;
    setSubmitError("");
    onClose();
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current || !salesReturn) return;
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    try {
      const updated = await salesReturnService.authorize(salesReturn.id, {
        refundAllocations: values.refundAllocations,
      });
      message.success(`Devolución ${updated.returnNumber} autorizada correctamente.`);
      onAuthorized(updated);
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => setSubmitError(msg));
      if (!applied) {
        const fromApi = readApiErrorMessage(err);
        setSubmitError(
          fromApi ||
            formatApiRequestError(err, {
              generic: "No se pudo autorizar la devolución.",
            }),
        );
      }
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  if (!salesReturn) return null;

  return (
    <ZHModal
      open={open}
      onClose={handleClose}
      size="md"
      title="Autorizar devolución"
      subtitle={`${salesReturn.returnNumber} — Total a reembolsar: ${formatMoney(grandTotal)}`}
    >
      <div>
        {fields.map((field, idx) => (
          <div key={field.id} className="sr-refund-row">
            <ZHField
              label="Forma de reembolso"
              density="compact"
              fieldError={errors.refundAllocations?.[idx]?.method?.message}
            >
              <ZhSelect
                className="zh-input"
                disabled={saving}
                {...register(`refundAllocations.${idx}.method` as const)}
              >
                {METHOD_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
            <ZHField
              label="Monto"
              density="compact"
              fieldError={errors.refundAllocations?.[idx]?.amount?.message}
            >
              <ZhDecimalInput
                decimals={2}
                positiveOnly
                disabled={saving}
                {...register(`refundAllocations.${idx}.amount` as const, {
                  valueAsNumber: true,
                })}
              />
            </ZHField>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              disabled={saving || fields.length <= 1}
              onClick={() => remove(idx)}
              title="Quitar asignación"
            >
              <span className="material-symbols-outlined">delete</span>
            </ZHBtn>
          </div>
        ))}

        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={saving}
          onClick={() => append({ method: "Cash", amount: 0 })}
        >
          + Agregar asignación
        </ZHBtn>

        <div className="sr-refund-summary">
          <span>Asignado: {formatMoney(sum)}</span>
          <span className={remaining !== 0 ? "sr-refund-summary--pending" : ""}>
            {remaining === 0
              ? "Coincide con el total"
              : `Diferencia: ${formatMoney(remaining)}`}
          </span>
        </div>

        {errors.refundAllocations?.message && (
          <ZHPageNotice
            variant="warning"
            message={errors.refundAllocations.message}
          />
        )}

        {submitError ? (
          <ZHPageNotice variant="error" message="Error" detail={submitError} />
        ) : null}

        <ZHFormActions
          onCancel={handleClose}
          onSave={() => {
            void onValid();
          }}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: "Cancelar",
            save: saving ? "Autorizando..." : "Autorizar",
          }}
        />
      </div>
    </ZHModal>
  );
}
