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
import {
  payableOriginLabel,
  payableLookupFacade,
  type PayableListItemDto,
} from "../../payables/facades/payableLookupFacade";
import {
  buildApplySupplierCreditSchema,
  type ApplySupplierCreditFormValues,
} from "../schemas/supplierCreditSchema";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

interface Props {
  open: boolean;
  credit: SupplierCreditDto | null;
  onClose: () => void;
  onApplied: (updated: SupplierCreditDto) => void;
  /** 02D-F — CxP destino preseleccionada (entrada contextual desde la CxP); editable. */
  defaultPayableId?: string | null;
}

/**
 * Aplica el crédito de proveedor contra una CxP destino del mismo proveedor. El selector de CxP
 * se resuelve exclusivamente vía `payableLookupFacade.list(...)` (filtro server-side — nunca
 * client-side sobre una lista completa, diseño Fase 13 cambio exacto #2).
 * ZH-SUPPLIER-CREDIT-APPLY-PAYABLES-02D-C: CxP de Compra y de Gasto (el backend resuelve el lock
 * según el origen); pendientes y parcialmente pagadas (el filtro de estado es de un solo valor →
 * dos consultas server-side). Manual no admite saldos a favor (el backend lo rechaza igual).
 */
export function ApplySupplierCreditModal({ open, credit, onClose, onApplied, defaultPayableId }: Props) {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const [saving, setSaving] = useState(false);
  const [submitError, setSubmitError] = useState("");
  const [payables, setPayables] = useState<PayableListItemDto[]>([]);
  const [loadingPayables, setLoadingPayables] = useState(false);
  const submittingRef = useRef(false);

  const availableAmount = credit?.availableAmount ?? 0;
  const {
    register,
    handleSubmit,
    reset,
    setError,
    setValue,
    formState: { errors },
  } = useForm<ApplySupplierCreditFormValues>({
    resolver: zodResolver(buildApplySupplierCreditSchema(availableAmount, moneyDecimals)),
    defaultValues: { targetPurchasePayableId: "", amount: availableAmount },
  });

  useEffect(() => {
    if (!open || !credit) return;
    reset({ targetPurchasePayableId: "", amount: credit.availableAmount });
    setSubmitError("");
    setLoadingPayables(true);
    Promise.all(
      (["pending", "partiallypaid"] as const).map((status) =>
        payableLookupFacade.list({ supplierId: credit.supplierId, status }, 1, 100),
      ),
    )
      .then((results) =>
        setPayables(
          results
            .flatMap((r) => r.items)
            .filter((p) => p.outstandingAmount > 0 && p.originType !== "Manual"),
        ),
      )
      .catch(() => setPayables([]))
      .finally(() => setLoadingPayables(false));
  }, [open, credit, reset]);

  // 02D-F — preselección de la CxP destino una vez cargadas las opciones (solo si es elegible).
  useEffect(() => {
    if (defaultPayableId && payables.some((p) => p.id === defaultPayableId))
      setValue("targetPurchasePayableId", defaultPayableId);
  }, [payables, defaultPayableId, setValue]);

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
      subtitle={`Proveedor: ${credit.supplierName ?? "—"} — Saldo disponible: ${formatMoney(credit.availableAmount, moneyDecimals)}`}
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
                {payableOriginLabel(p.originType)} · {p.documentNumber} — Saldo pendiente{" "}
                {formatMoney(p.outstandingAmount, moneyDecimals)}
              </option>
            ))}
          </select>
        </ZHField>

        <ZHField label="Monto a aplicar" required fieldError={errors.amount?.message}>
          <ZhDecimalInput
            precision="money"
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
