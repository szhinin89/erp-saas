import { useEffect, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhDecimalInput } from "../../../components/zh/inputs/ZhDecimalInput";
import { message } from "../../../lib/messages";
import { applyServerErrors } from "../../lib/validationErrors";
import { formatApiRequestError } from "../../lib/apiError";
import { formatMoney } from "../../../lib/sanitizers";
import type { SupplierCreditDto } from "../api/supplierCreditService";
import { supplierCreditService } from "../api/supplierCreditService";
import { payableService, type PurchasePayableDto } from "../api/payableService";
import {
  buildApplySupplierCreditSchema,
  type ApplySupplierCreditFormValues,
} from "../schemas/supplierCreditSchema";

interface Props {
  open: boolean;
  credit: SupplierCreditDto | null;
  onClose: () => void;
  onApplied: (updated: SupplierCreditDto) => void;
}

/**
 * Aplica el crédito de proveedor contra una CxP destino del mismo proveedor. El selector de CxP
 * se resuelve exclusivamente vía `payableService.list(status, supplierId, ...)` (filtro
 * server-side — nunca client-side sobre una lista completa, diseño Fase 13 cambio exacto #2).
 * Mismo patrón RHF+Zod que `RegisterPaymentModal.tsx` (P0-03).
 */
export function ApplySupplierCreditModal({ open, credit, onClose, onApplied }: Props) {
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [payables, setPayables] = useState<PurchasePayableDto[]>([]);
  const [loadingPayables, setLoadingPayables] = useState(false);
  const submittingRef = useRef(false);

  const availableAmount = credit?.availableAmount ?? 0;
  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<ApplySupplierCreditFormValues>({
    resolver: zodResolver(buildApplySupplierCreditSchema(availableAmount)),
    defaultValues: { targetPurchasePayableId: "", amount: availableAmount },
  });

  useEffect(() => {
    if (!open || !credit) return;
    reset({ targetPurchasePayableId: "", amount: credit.availableAmount });
    setSubmitError("");
    setLoadingPayables(true);
    payableService
      .list("pending", credit.supplierId, 1, 100)
      .then((r) => setPayables(r.items.filter((p) => p.balanceDue > 0)))
      .catch(() => setPayables([]))
      .finally(() => setLoadingPayables(false));
  }, [open, credit, reset]);

  const handleClose = () => {
    if (saving) return;
    setSubmitError("");
    onClose();
  };

  const onValid = handleSubmit(async (values) => {
    if (submittingRef.current || !credit) return;
    submittingRef.current = true;
    setSubmitError("");
    setSaving(true);
    try {
      const updated = await supplierCreditService.apply(credit.id, {
        targetPurchasePayableId: values.targetPurchasePayableId,
        amount: values.amount,
        clientRequestId: crypto.randomUUID(),
      });
      message.success("Crédito aplicado correctamente.");
      onApplied(updated);
      onClose();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, setError, (msg) => setSubmitError(msg));
      if (!applied) {
        setSubmitError(
          formatApiRequestError(err, { generic: "No se pudo aplicar el crédito." }),
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
      title="Aplicar crédito de proveedor"
      subtitle={`Proveedor: ${credit.supplierId} — Saldo disponible: ${formatMoney(credit.availableAmount)}`}
    >
      <div>
        <ZHField
          label="Cuenta por pagar destino"
          required
          fieldError={errors.targetPurchasePayableId?.message}
        >
          <select
            className="zh-input"
            disabled={saving || loadingPayables}
            {...register("targetPurchasePayableId")}
          >
            <option value="">
              {loadingPayables ? "Cargando..." : "Seleccione una cuenta por pagar"}
            </option>
            {payables.map((p) => (
              <option key={p.id} value={p.id}>
                {p.purchaseId} — Saldo {formatMoney(p.balanceDue)}
              </option>
            ))}
          </select>
        </ZHField>

        <ZHField label="Monto a aplicar" required fieldError={errors.amount?.message}>
          <ZhDecimalInput
            decimals={2}
            positiveOnly
            disabled={saving}
            {...register("amount")}
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
          labels={{ cancel: "Cancelar", save: saving ? "Aplicando..." : "Aplicar crédito" }}
        />
      </div>
    </ZHModal>
  );
}
