import { useEffect, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions, ZHFormAlert } from "../../../components/zh/ZHForm";
import { ZhTextarea } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type { SupplierPaymentDto } from "../api/supplierPaymentService";

interface Props {
  open: boolean;
  payment: SupplierPaymentDto | null;
  supplierName: string;
  methods: PaymentMethodDto[];
  saving: boolean;
  submitError: string | null;
  onCancel: () => void;
  onConfirm: (reason: string) => void;
}

/**
 * SUPPLIER-PAYMENTS-REVERSE-FRONTEND-16C — modal de confirmación obligatorio antes de reversar un
 * pago Confirmed. Mismo patrón que `SupplierPaymentConfirmModal` (ZHModal + resumen + ZHFormAlert
 * de advertencia): el pago no se elimina, solo cambia de estado — por eso el resumen muestra
 * exactamente lo que se va a revertir (medios y cuotas) antes de pedir el motivo obligatorio.
 */
export function SupplierPaymentReverseModal({
  open,
  payment,
  supplierName,
  methods,
  saving,
  submitError,
  onCancel,
  onConfirm,
}: Props) {
  const [reason, setReason] = useState("");
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setReason("");
      setTouched(false);
    }
  }, [open]);

  if (!open || !payment) return null;

  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const trimmedReason = reason.trim();
  const reasonError = touched && !trimmedReason ? "El motivo es obligatorio." : null;

  const handleConfirm = () => {
    setTouched(true);
    if (!trimmedReason || saving) return;
    onConfirm(trimmedReason);
  };

  return (
    <ZHModal
      open={open}
      onClose={saving ? () => {} : onCancel}
      size="md"
      title="Reversar pago"
      subtitle="El pago no se elimina — queda marcado como Reversado, con los saldos y el asiento contable invertidos."
      footer={
        <ZHFormActions
          onCancel={saving ? undefined : onCancel}
          onSave={handleConfirm}
          hideDraft
          disableSave={saving}
          labels={{ cancel: "Cancelar", save: saving ? "Reversando..." : "Confirmar reversa" }}
        />
      }
    >
      <div className="sp-confirm-summary">
        <dl>
          <dt>Número</dt>
          <dd>{payment.displayNumber}</dd>
          <dt>Proveedor</dt>
          <dd>{supplierName || "—"}</dd>
          <dt>Fecha</dt>
          <dd>{formatDate(payment.paymentDate)}</dd>
          <dt>Total</dt>
          <dd>{formatMoney(payment.totalAmount)}</dd>
        </dl>

        <h4 className="sp-confirm-subtitle">Medios de pago</h4>
        <ul className="sp-confirm-list">
          {payment.methodLines.map((line) => (
            <li key={line.id}>
              {methodsById.get(line.paymentMethodId)?.name ?? "—"} — {formatMoney(line.amount)}
            </li>
          ))}
        </ul>

        <h4 className="sp-confirm-subtitle">Cuotas aplicadas</h4>
        <ul className="sp-confirm-list">
          {payment.applicationLines.map((line) => (
            <li key={line.id}>{formatMoney(line.amountApplied)}</li>
          ))}
        </ul>

        <ZHField label="Motivo de la reversa" required error={reasonError}>
          <ZhTextarea
            rows={3}
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            disabled={saving}
            maxLength={500}
            aria-required="true"
            aria-label="Motivo de la reversa"
          />
        </ZHField>

        <ZHFormAlert
          type="warning"
          message="Esta acción reversará los saldos de CxP y generará un asiento contable inverso. No se eliminará el pago."
        />

        {submitError && (
          <ZHFormAlert type="error" message="No se pudo reversar el pago" detail={submitError} />
        )}
      </div>
    </ZHModal>
  );
}
