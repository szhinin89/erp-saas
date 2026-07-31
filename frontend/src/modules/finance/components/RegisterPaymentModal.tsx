import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import { formatMoney } from "../../../lib/sanitizers";
import type { PurchasePayableDto } from "../api/payableService";
import { paymentService } from "../api/paymentService";
import {
  paymentMethodService,
  type PaymentMethodDto,
} from "../../sales/api/paymentMethodService";
import {
  buildRegisterPaymentSchema,
  type RegisterPaymentFormValues,
} from "../../../schemas/finance/registerPaymentSchema";

interface Props {
  open: boolean;
  payable: PurchasePayableDto | null;
  onClose: () => void;
  onRegistered: () => void;
}

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — registra un pago contra una CxP seleccionada.
 * Mismo patrón exacto que RegisterCollectionModal (CxC), adaptado a PurchasePayable.
 */
export function RegisterPaymentModal({ open, payable, onClose, onRegistered }: Props) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const submittingRef = useRef(false);

  const maxAmount = payable?.balanceDue ?? 0;
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<RegisterPaymentFormValues>({
    resolver: zodResolver(buildRegisterPaymentSchema(maxAmount)),
    defaultValues: { amount: maxAmount, installmentId: "", paymentMethodId: "", reference: "" },
  });

  useEffect(() => {
    if (!open || !payable) return;
    reset({
      amount: payable.balanceDue,
      installmentId: "",
      paymentMethodId: "",
      reference: "",
    });
    setSubmitError("");
    paymentMethodService
      .list(true)
      .then(setMethods)
      .catch(() => setMethods([]));
  }, [open, payable, reset]);

  const handleClose = () => {
    if (saving) return;
    setSubmitError("");
    onClose();
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current || !payable) return;
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    try {
      await paymentService.registerPayment({
        supplierId: payable.supplierId,
        amount: values.amount,
        paymentDate: new Date().toISOString().slice(0, 10),
        paymentMethodId: values.paymentMethodId || null,
        reference: values.reference || null,
        lines: [
          {
            documentId: payable.id,
            installmentId: values.installmentId || null,
            appliedAmount: values.amount,
          },
        ],
      });
      message.success("Pago registrado correctamente.");
      onRegistered();
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => setSubmitError(msg));
      if (!applied) {
        const fromApi = readApiErrorMessage(err);
        setSubmitError(
          fromApi ||
            formatApiRequestError(err, {
              generic: "No se pudo registrar el pago.",
            }),
        );
      }
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  if (!payable) return null;

  const pendingInstallments = payable.installments.filter(
    (i) => i.status !== "paid" && i.paidAmount < i.amount,
  );

  return (
    <ZHModal
      open={open}
      onClose={handleClose}
      size="md"
      title="Registrar pago"
      subtitle={`Proveedor: ${payable.supplierId} — Saldo pendiente: ${formatMoney(payable.balanceDue)}`}
    >
      <div>
        <ZHField label="Monto a pagar" error={errors.amount?.message} required>
          <ZhDecimalInput
            decimals={2}
            positiveOnly
            disabled={saving}
            {...register("amount", {
              valueAsNumber: true,
              setValueAs: (v) => (v === "" ? null : Number(v)),
            })}
          />
        </ZHField>

        {pendingInstallments.length > 0 && (
          <ZHField label="Cuota (opcional)" error={errors.installmentId?.message}>
            <select className="zh-input" disabled={saving} {...register("installmentId")}>
              <option value="">Sin cuota específica</option>
              {pendingInstallments.map((i) => (
                <option key={i.id} value={i.id}>
                  Cuota #{i.installmentNumber} — {formatMoney(i.amount - i.paidAmount)} pendiente
                </option>
              ))}
            </select>
          </ZHField>
        )}

        <ZHField label="Forma de pago (opcional)" error={errors.paymentMethodId?.message}>
          <select className="zh-input" disabled={saving} {...register("paymentMethodId")}>
            <option value="">Sin especificar</option>
            {methods.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </ZHField>

        <ZHField label="Referencia (opcional)" error={errors.reference?.message}>
          <input
            className="zh-input"
            maxLength={200}
            disabled={saving}
            placeholder="N.º de transferencia, cheque, etc."
            {...register("reference")}
          />
        </ZHField>

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
            save: saving ? "Guardando..." : "Registrar pago",
          }}
        />
      </div>
    </ZHModal>
  );
}
