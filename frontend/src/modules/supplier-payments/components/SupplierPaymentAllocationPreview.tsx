import { useMemo } from "react";
import { useFormContext } from "react-hook-form";
import { formatMoney } from "../../../lib/sanitizers";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type { PendingInstallmentOption } from "../api/pendingPayablesFacade";
import { computeAutomaticAllocations } from "../utils/allocation";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  methods: PaymentMethodDto[];
  installments: PendingInstallmentOption[];
}

/**
 * Vista de solo lectura de la distribución automática medio↔cuota — nunca editable por el
 * usuario (regla del ticket: "implementar distribución automática"). Se recalcula en vivo a
 * partir de los medios y cuotas ya cargados en el formulario.
 */
export function SupplierPaymentAllocationPreview({ methods, installments }: Props) {
  const { watch } = useFormContext<RegisterSupplierPaymentFormValues>();
  const methodLines = watch("methodLines") ?? [];
  const applicationLines = watch("applicationLines") ?? [];
  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const installmentsById = new Map(installments.map((i) => [i.installmentId, i]));

  const methodLinesKey = JSON.stringify(methodLines);
  const applicationLinesKey = JSON.stringify(applicationLines);
  const allocations = useMemo(
    () => computeAutomaticAllocations(methodLines, applicationLines),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [methodLinesKey, applicationLinesKey],
  );

  if (allocations.length === 0) {
    return <p className="sp-line-hint">Agregue medios de pago y cuotas para ver la distribución.</p>;
  }

  return (
    <div className="table-scroll">
      <table className="table">
        <thead>
          <tr>
            <th>Medio de pago</th>
            <th>Cuota</th>
            <th className="zh-text-align-right">Monto</th>
          </tr>
        </thead>
        <tbody>
          {allocations.map((a, idx) => {
            const method = methodsById.get(methodLines[a.methodLineIndex]?.paymentMethodId ?? "");
            const installment = installmentsById.get(
              applicationLines[a.applicationLineIndex]?.accountsPayableInstallmentId ?? "",
            );
            return (
              <tr key={idx}>
                <td>{method?.name ?? "—"}</td>
                <td>
                  {installment
                    ? `${installment.documentType} ${installment.documentNumber} — Cuota #${installment.installmentNumber}`
                    : "—"}
                </td>
                <td className="zh-text-align-right">{formatMoney(a.amount)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
