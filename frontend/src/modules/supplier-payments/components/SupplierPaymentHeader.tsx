import { Controller, useFormContext } from "react-hook-form";
import { ZHField } from "../../../components/zh/ZHForm";
import { ZhDateInput, ZhTextInput } from "../../../components/zh/inputs";
import { SupplierPicker } from "../../purchases/components/SupplierPicker";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  disabled?: boolean;
}

/**
 * Cabecera del pago: proveedor (reutiliza `SupplierPicker`, ya sancionado como picker
 * especializado — CLAUDE.md frontend), fecha y número de recibo manual (opcional; si se deja
 * vacío, el backend asigna `system_number` y ese es el número que se muestra al volver).
 */
export function SupplierPaymentHeader({ disabled }: Props) {
  const {
    control,
    register,
    formState: { errors },
  } = useFormContext<RegisterSupplierPaymentFormValues>();

  return (
    <div className="zh-grid zh-grid--2">
      <ZHField label="Proveedor" required error={errors.supplierId?.message}>
        <Controller
          name="supplierId"
          control={control}
          render={({ field }) => (
            <SupplierPicker
              value={field.value || null}
              onChange={(supplier) => field.onChange(supplier?.id ?? "")}
              disabled={disabled}
            />
          )}
        />
      </ZHField>

      <ZHField label="Fecha de pago" required error={errors.paymentDate?.message}>
        <ZhDateInput className="zh-input" disabled={disabled} {...register("paymentDate")} />
      </ZHField>

      <ZHField
        label="Número de recibo (opcional)"
        error={errors.receiptNumber?.message}
        hint="Si lo deja vacío, el sistema asigna un número de sistema automático al confirmar."
      >
        <ZhTextInput
          className="zh-input"
          maxLength={30}
          disabled={disabled}
          placeholder="N.º de cheque, papeleta, etc."
          {...register("receiptNumber")}
        />
      </ZHField>
    </div>
  );
}
