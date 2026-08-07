import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput, ZhSelect } from "../../../components/zh/inputs";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { formatMoney } from "../../../lib/sanitizers";
import { todayIso } from "../../../lib/formatters/dateFormatters";
import type { SupplierCreditDto } from "../api/supplierCreditService";
import { supplierCreditService } from "../api/supplierCreditService";
import {
  financialDestinationService,
  type CompanyFinancialDestinationDto,
} from "../api/financialDestinationService";
import { paymentMethodService, type PaymentMethodDto } from "../../sales/api/paymentMethodService";
import {
  buildRegisterSupplierCreditRefundSchema,
  type RegisterSupplierCreditRefundFormValues,
} from "../schemas/supplierCreditSchema";

interface Props {
  open: boolean;
  credit: SupplierCreditDto | null;
  onClose: () => void;
  onRegistered: (updated: SupplierCreditDto) => void;
}

/**
 * Registra un reembolso del crédito de proveedor contra un destino financiero activo. El destino
 * se resuelve vía `GET /finance/financial-destinations?isActive=true` (Remediación 01, Fase 13);
 * `AccountingAccountId` nunca se envía — se deriva server-side (diseño Fase 13 cambio exacto #3).
 * `externalReference` es condicionalmente obligatorio según `PaymentMethod.RequiresReference`
 * (catálogo real, `paymentMethodService.list(true)`) — validado manualmente en el submit porque
 * el resolver de Zod se fija al montar el formulario y no puede reaccionar a la selección del
 * método de pago sin remontar el modal.
 */
export function RegisterSupplierCreditRefundModal({
  open,
  credit,
  onClose,
  onRegistered,
}: Props) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [destinations, setDestinations] = useState<CompanyFinancialDestinationDto[]>([]);
  const [methods, setMethods] = useState<PaymentMethodDto[]>([]);
  const submittingRef = useRef(false);

  const availableAmount = credit?.availableAmount ?? 0;
  const {
    register,
    handleSubmit,
    reset,
    setError,
    watch,
    formState: { errors },
  } = useForm<RegisterSupplierCreditRefundFormValues>({
    resolver: zodResolver(buildRegisterSupplierCreditRefundSchema(availableAmount, false)),
    defaultValues: {
      financialDestinationId: "",
      paymentMethodCode: "",
      amount: availableAmount,
      effectiveDate: todayIso(),
      externalReference: "",
    },
  });
  const selectedPaymentMethodCode = watch("paymentMethodCode");
  const selectedMethod = methods.find((m) => m.code === selectedPaymentMethodCode);

  useEffect(() => {
    if (!open || !credit) return;
    reset({
      financialDestinationId: "",
      paymentMethodCode: "",
      amount: credit.availableAmount,
      effectiveDate: todayIso(),
      externalReference: "",
    });
    setSubmitError("");
    financialDestinationService
      .list(true)
      .then((list) => setDestinations(list.filter((d) => d.currencyCode === credit.currencyCode)))
      .catch(() => setDestinations([]));
    paymentMethodService
      .list(true)
      .then(setMethods)
      .catch(() => setMethods([]));
  }, [open, credit, reset]);

  const handleClose = () => {
    if (saving) return;
    setSubmitError("");
    onClose();
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current || !credit) return;
    if (selectedMethod?.requiresReference && !values.externalReference.trim()) {
      setError("externalReference", {
        type: "manual",
        message: "La forma de pago seleccionada requiere una referencia.",
      });
      return;
    }
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    try {
      await supplierCreditService.registerRefund(credit.id, {
        financialDestinationId: values.financialDestinationId,
        paymentMethodCode: values.paymentMethodCode,
        amount: values.amount,
        effectiveDate: values.effectiveDate,
        externalReference: values.externalReference.trim() || null,
        clientRequestId: crypto.randomUUID(),
      });
      message.success("Reembolso registrado correctamente.");
      const updated = await supplierCreditService.getById(credit.id);
      onRegistered(updated);
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => setSubmitError(msg));
      if (!applied) {
        setSubmitError(
          formatApiRequestError(err, { generic: "No se pudo registrar el reembolso." }),
        );
      }
    } finally {
      submittingRef.current = false;
      setSaving(false);
    }
  });

  if (!credit) return null;

  return (
    <ZHModal
      open={open}
      onClose={handleClose}
      size="md"
      title="Registrar reembolso"
      subtitle={`Proveedor: ${credit.supplierId} — Saldo disponible: ${formatMoney(credit.availableAmount)}`}
    >
      <div>
        <ZHField
          label="Destino financiero"
          required
          fieldError={errors.financialDestinationId?.message}
        >
          <ZhSelect className="zh-input" disabled={saving} {...register("financialDestinationId")}>
            <option value="">Seleccione un destino</option>
            {destinations.map((d) => (
              <option key={d.id} value={d.id}>
                {d.code} — {d.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField label="Forma de pago" required fieldError={errors.paymentMethodCode?.message}>
          <ZhSelect className="zh-input" disabled={saving} {...register("paymentMethodCode")}>
            <option value="">Seleccione una forma de pago</option>
            {methods.map((m) => (
              <option key={m.id} value={m.code}>
                {m.name}
              </option>
            ))}
          </ZhSelect>
        </ZHField>

        <ZHField label="Monto a reembolsar" required fieldError={errors.amount?.message}>
          <ZhDecimalInput decimals={2} positiveOnly disabled={saving} {...register("amount")} />
        </ZHField>

        <ZHField label="Fecha efectiva" required fieldError={errors.effectiveDate?.message}>
          <input type="date" className="zh-input" disabled={saving} {...register("effectiveDate")} />
        </ZHField>

        <ZHField
          label={
            selectedMethod?.requiresReference
              ? "Referencia (obligatoria)"
              : "Referencia (opcional)"
          }
          fieldError={errors.externalReference?.message}
        >
          <input
            className="zh-input"
            maxLength={200}
            disabled={saving}
            placeholder="N.º de transferencia, cheque, etc."
            {...register("externalReference")}
          />
        </ZHField>

        {submitError ? (
          <ZHPageNotice variant="error" message="Error" detail={submitError} />
        ) : null}

        <ZHFormActions
          onCancel={handleClose}
          onSave={() => void onValid()}
          hideDraft
          disableSave={saving}
          labels={{ cancel: "Cancelar", save: saving ? "Registrando..." : "Registrar reembolso" }}
        />
      </div>
    </ZHModal>
  );
}
