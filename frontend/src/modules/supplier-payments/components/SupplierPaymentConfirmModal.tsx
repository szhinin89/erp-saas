import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHFormAlert, ZHFormActions } from "../../../components/zh/ZHForm";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney, formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { computePaymentSplit } from "../utils/allocation";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type { PendingInstallmentOption } from "../api/pendingPayablesFacade";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";
import { usePrecisionDecimals } from "../../../hooks/usePrecisionPolicy";

interface Props {
  open: boolean;
  values: RegisterSupplierPaymentFormValues | null;
  supplierName: string;
  methods: PaymentMethodDto[];
  installments: PendingInstallmentOption[];
  saving: boolean;
  submitError: string | null;
  onCancel: () => void;
  onConfirm: () => void;
}

/**
 * Modal de confirmación obligatorio antes de registrar (regla UX: sin Draft, la confirmación
 * directa reemplaza al borrador — ver `project_draft_vs_direct_confirmation_rule`). Muestra
 * exactamente lo que el ticket pide: proveedor, fecha, número de recibo manual (si existe), total,
 * medios de pago, cuotas afectadas y la advertencia de impacto contable/saldos.
 * ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — si queda remanente sin aplicar, este modal ES la
 * confirmación explícita: muestra Aplicado a CxP / Anticipo proveedor y solo "Confirmar pago"
 * envía `confirmUnappliedAmount`. Cancelar no crea pago, crédito, movimiento de caja ni asiento.
 */
export function SupplierPaymentConfirmModal({
  open,
  values,
  supplierName,
  methods,
  installments,
  saving,
  submitError,
  onCancel,
  onConfirm,
}: Props) {
  const moneyDecimals = usePrecisionDecimals("money"); // presentación (04F)
  if (!open || !values) return null;

  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const installmentsById = new Map(installments.map((i) => [i.installmentId, i]));
  const { total, applied, unapplied } = computePaymentSplit(
    values.methodLines,
    values.applicationLines.filter((l) => l.amountApplied > 0),
  );
  const money = (value: number) => formatMoneyWithSymbol(value, moneyDecimals);

  return (
    <ZHModal
      open={open}
      onClose={saving ? () => {} : onCancel}
      size="md"
      title="Confirmar registro de pago"
      subtitle="Revise los datos antes de confirmar — el pago quedará confirmado de inmediato, sin borrador."
      footer={
        <ZHFormActions
          onCancel={onCancel}
          onSave={onConfirm}
          hideDraft
          disableSave={saving}
          labels={{
            cancel: "Cancelar",
            save: saving ? "Registrando..." : unapplied > 0 ? "Confirmar pago" : "Confirmar y registrar",
          }}
        />
      }
    >
      <div className="sp-confirm-summary">
        <dl>
          <dt>Proveedor</dt>
          <dd>{supplierName || "—"}</dd>
          <dt>Fecha</dt>
          <dd>{formatDate(values.paymentDate)}</dd>
          <dt>Número de recibo</dt>
          <dd>{values.receiptNumber?.trim() || "Se asignará un número de sistema automático"}</dd>
          <dt>Total</dt>
          <dd>{formatMoney(total, moneyDecimals)}</dd>
          <dt>Aplicado a CxP</dt>
          <dd>{formatMoney(applied, moneyDecimals)}</dd>
          <dt>Anticipo proveedor</dt>
          <dd>{formatMoney(unapplied > 0 ? unapplied : 0, moneyDecimals)}</dd>
        </dl>

        {unapplied > 0 && applied > 0 && (
          <ZHFormAlert
            type="warning"
            message={`El pago supera el saldo que puede aplicarse en ${money(unapplied)}.`}
            detail={`Se registrará: Aplicado a CxP: ${money(applied)} · Anticipo proveedor: ${money(unapplied)}. ¿Desea continuar?`}
          />
        )}
        {unapplied > 0 && applied === 0 && (
          <ZHFormAlert
            type="warning"
            message="Este pago quedará pendiente de aplicar."
            detail={`Se registrará un anticipo a favor del proveedor por ${money(unapplied)}. ¿Desea continuar?`}
          />
        )}

        <h4 className="sp-confirm-subtitle">Medios de pago</h4>
        <ul className="sp-confirm-list">
          {values.methodLines.map((line, idx) => (
            <li key={idx}>
              {methodsById.get(line.paymentMethodId)?.name ?? "—"} — {formatMoney(line.amount || 0, moneyDecimals)}
            </li>
          ))}
        </ul>

        <h4 className="sp-confirm-subtitle">Cuotas afectadas</h4>
        {values.applicationLines.length === 0 && <p className="sp-line-hint">Sin cuotas: el pago no se aplica a ninguna CxP.</p>}
        <ul className="sp-confirm-list">
          {values.applicationLines.map((line, idx) => {
            const installment = installmentsById.get(line.accountsPayableInstallmentId);
            return (
              <li key={idx}>
                {installment
                  ? `${installment.documentType} ${installment.documentNumber} — Cuota #${installment.installmentNumber}`
                  : "—"}{" "}
                — {formatMoney(line.amountApplied || 0, moneyDecimals)}
              </li>
            );
          })}
        </ul>

        <ZHFormAlert
          type="warning"
          message="Se actualizarán saldos de CxP y se generará asiento contable."
        />

        {submitError && <ZHFormAlert type="error" message="No se pudo registrar el pago" detail={submitError} />}
      </div>
    </ZHModal>
  );
}
