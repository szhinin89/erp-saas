import { useEffect, useState } from "react";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHField, ZHFormActions, ZHFormAlert } from "../../../components/zh/ZHForm";
import { ZhSelect, ZhTextarea } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type {
  ReverseSupplierPaymentRequest,
  SupplierPaymentBankReversalReason,
  SupplierPaymentDto,
} from "../api/supplierPaymentService";
import { SUPPLIER_PAYMENT_BANK_REVERSAL_REASONS } from "../constants/bankReversalReasons";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

interface Props {
  open: boolean;
  payment: SupplierPaymentDto | null;
  supplierName: string;
  methods: PaymentMethodDto[];
  saving: boolean;
  submitError: string | null;
  onCancel: () => void;
  onConfirm: (request: ReverseSupplierPaymentRequest) => void;
}

/**
 * SUPPLIER-PAYMENTS-REVERSE-FRONTEND-16C — modal de confirmación obligatorio antes de reversar un
 * pago Confirmed. Mismo patrón que `SupplierPaymentConfirmModal` (ZHModal + resumen + ZHFormAlert
 * de advertencia): el pago no se elimina, solo cambia de estado — por eso el resumen muestra
 * exactamente lo que se va a revertir (medios y cuotas) antes de pedir el motivo obligatorio.
 *
 * ZH-SUPPLIER-PAYMENT-REVERSAL-SEMANTICS-02B-FINAL — la reversa es una CORRECCIÓN DOCUMENTAL de un
 * pago que no llegó a ejecutarse: con fuentes de caja exige confirmar que el efectivo no se entregó
 * y sigue en la misma caja; con fuentes bancarias exige el motivo estructurado. Dinero que sí salió
 * y luego regresó no se revierte aquí (futura devolución de fondos). El backend vuelve a validar.
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
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  const [reason, setReason] = useState("");
  const [cashNotDeliveredConfirmed, setCashNotDeliveredConfirmed] = useState(false);
  const [bankReversalReason, setBankReversalReason] = useState<SupplierPaymentBankReversalReason | "">("");
  const [touched, setTouched] = useState(false);

  useEffect(() => {
    if (open) {
      setReason("");
      setCashNotDeliveredConfirmed(false);
      setBankReversalReason("");
      setTouched(false);
    }
  }, [open]);

  if (!open || !payment) return null;

  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const trimmedReason = reason.trim();
  const reasonError = touched && !trimmedReason ? "El motivo es obligatorio." : null;
  const hasCashLines = payment.methodLines.some((l) => l.cashRegisterId !== null);
  const hasBankLines = payment.methodLines.some((l) => l.companyBankAccountId !== null);
  const cashError =
    touched && hasCashLines && !cashNotDeliveredConfirmed
      ? "Debe confirmar que el efectivo no fue entregado al proveedor."
      : null;
  const bankError =
    touched && hasBankLines && !bankReversalReason ? "Seleccione el motivo de la reversa bancaria." : null;

  const handleConfirm = () => {
    setTouched(true);
    if (!trimmedReason || saving) return;
    if (hasCashLines && !cashNotDeliveredConfirmed) return;
    if (hasBankLines && !bankReversalReason) return;
    onConfirm({
      reason: trimmedReason,
      cashNotDeliveredConfirmed: hasCashLines ? cashNotDeliveredConfirmed : false,
      bankReversalReason: hasBankLines && bankReversalReason ? bankReversalReason : null,
    });
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
          <dd>{formatMoney(payment.totalAmount, moneyDecimals)}</dd>
        </dl>

        <h4 className="sp-confirm-subtitle">Medios de pago</h4>
        <ul className="sp-confirm-list">
          {payment.methodLines.map((line) => (
            <li key={line.id}>
              {methodsById.get(line.paymentMethodId)?.name ?? "—"} — {formatMoney(line.amount, moneyDecimals)}
            </li>
          ))}
        </ul>

        <h4 className="sp-confirm-subtitle">Cuotas aplicadas</h4>
        <ul className="sp-confirm-list">
          {payment.applicationLines.map((line) => (
            <li key={line.id}>{formatMoney(line.amountApplied, moneyDecimals)}</li>
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

        {hasCashLines && (
          <ZHField label="Efectivo" required error={cashError}>
            <label className="sp-reverse-confirm">
              <input
                type="checkbox"
                checked={cashNotDeliveredConfirmed}
                disabled={saving}
                onChange={(e) => setCashNotDeliveredConfirmed(e.target.checked)}
              />
              <span>Confirmo que el efectivo no fue entregado al proveedor y permanece en la misma caja.</span>
            </label>
          </ZHField>
        )}

        {hasBankLines && (
          <ZHField label="Motivo de la reversa bancaria" required error={bankError}>
            <ZhSelect
              className="zh-input"
              value={bankReversalReason}
              disabled={saving}
              aria-label="Motivo de la reversa bancaria"
              onChange={(e) => setBankReversalReason(e.target.value as SupplierPaymentBankReversalReason | "")}
            >
              <option value="">Seleccione...</option>
              {SUPPLIER_PAYMENT_BANK_REVERSAL_REASONS.map((r) => (
                <option key={r.value} value={r.value}>
                  {r.label}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
        )}

        <ZHFormAlert
          type="warning"
          message="Esta acción reversará los saldos de CxP y generará un asiento contable inverso. No se eliminará el pago."
          detail="La reversa corrige un pago que no llegó a ejecutarse. Si el dinero ya salió y el proveedor lo devolvió, no use la reversa: registre una devolución de fondos."
        />

        {submitError && (
          <ZHFormAlert type="error" message="No se pudo reversar el pago" detail={submitError} />
        )}
      </div>
    </ZHModal>
  );
}
