import { useFieldArray, useFormContext } from "react-hook-form";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhDecimalInput, ZhSelect } from "../../../components/zh/inputs";
import { formatDate } from "../../../lib/formatters/dateFormatters";
import { formatMoney } from "../../../lib/sanitizers";
import type { PendingInstallmentOption } from "../api/pendingPayablesFacade";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  installments: PendingInstallmentOption[];
  disabled?: boolean;
}

/**
 * Líneas dinámicas de aplicación a cuota — una cuota puede pagarse con varios medios y un medio
 * puede cubrir varias cuotas (la distribución real entre ambos la calcula
 * `SupplierPaymentAllocationPreview`, no esta tabla). Cuotas exclusivamente desde
 * `/api/v1/payables` (`pendingPayablesFacade`) — nunca desde Compras/Gastos origen.
 */
export function SupplierPaymentApplicationsEditor({ installments, disabled }: Props) {
  const {
    control,
    register,
    watch,
    formState: { errors },
  } = useFormContext<RegisterSupplierPaymentFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: "applicationLines" });
  const installmentsById = new Map(installments.map((i) => [i.installmentId, i]));
  const watchedLines = watch("applicationLines");

  const selectedElsewhere = (index: number) =>
    new Set(
      (watchedLines ?? [])
        .filter((_, i) => i !== index)
        .map((l) => l.accountsPayableInstallmentId)
        .filter(Boolean),
    );

  return (
    <div className="sp-lines">
      {fields.map((field, index) => {
        const lineErrors = errors.applicationLines?.[index];
        const excluded = selectedElsewhere(index);
        const currentValue = watchedLines?.[index]?.accountsPayableInstallmentId;
        const currentInstallment = currentValue ? installmentsById.get(currentValue) : undefined;

        return (
          <div key={field.id} className="sp-line-row">
            <ZHField
              label="Cuota"
              required
              error={lineErrors?.accountsPayableInstallmentId?.message}
            >
              <ZhSelect
                className="zh-input"
                disabled={disabled}
                {...register(`applicationLines.${index}.accountsPayableInstallmentId` as const)}
              >
                <option value="">Seleccione...</option>
                {installments
                  .filter((i) => i.installmentId === currentValue || !excluded.has(i.installmentId))
                  .map((i) => (
                    <option key={i.installmentId} value={i.installmentId}>
                      {i.documentType} {i.documentNumber} — Cuota #{i.installmentNumber} — Vence{" "}
                      {formatDate(i.dueDate)} — Saldo {formatMoney(i.outstandingAmount)}
                    </option>
                  ))}
              </ZhSelect>
            </ZHField>

            <ZHField label="Monto a aplicar" required error={lineErrors?.amountApplied?.message}>
              <ZhDecimalInput
                decimals={2}
                positiveOnly
                disabled={disabled}
                {...register(`applicationLines.${index}.amountApplied` as const, {
                  valueAsNumber: true,
                  setValueAs: (v) => (v === "" ? null : Number(v)),
                })}
              />
            </ZHField>

            {currentInstallment && (
              <p className="sp-line-hint">
                Saldo pendiente de la cuota: {formatMoney(currentInstallment.outstandingAmount)}
              </p>
            )}

            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              disabled={disabled || fields.length <= 1}
              onClick={() => remove(index)}
            >
              Quitar
            </ZHBtn>
          </div>
        );
      })}

      {typeof errors.applicationLines?.message === "string" && (
        <p className="zh-field-hint zh-field-hint--error">{errors.applicationLines.message}</p>
      )}

      <ZHBtn
        type="button"
        variant="secondary"
        size="sm"
        disabled={disabled || installments.length === 0}
        onClick={() => append({ accountsPayableInstallmentId: "", amountApplied: 0 })}
      >
        + Agregar cuota
      </ZHBtn>

      {installments.length === 0 && (
        <p className="sp-line-hint">
          El proveedor seleccionado no tiene cuotas pendientes en Cuentas por Pagar.
        </p>
      )}
    </div>
  );
}
