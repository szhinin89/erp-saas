import { useFieldArray, useFormContext } from "react-hook-form";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhDateInput, ZhDecimalInput, ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type { CompanyFinancialDestinationDto } from "../../finance/api/financialDestinationService";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  methods: PaymentMethodDto[];
  destinations: CompanyFinancialDestinationDto[];
  disabled?: boolean;
}

/**
 * Líneas dinámicas de medio de pago — un pago puede tener varios (transferencia, cheque,
 * efectivo, cualquier otro PaymentMethod activo del catálogo). Cheque exige número y fecha
 * (PaymentMethod.DetailType === "Check", catálogo — no una lista hardcodeada de códigos).
 */
export function SupplierPaymentMethodLinesEditor({ methods, destinations, disabled }: Props) {
  const {
    control,
    register,
    watch,
    formState: { errors },
  } = useFormContext<RegisterSupplierPaymentFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: "methodLines" });
  const methodsById = new Map(methods.map((m) => [m.id, m]));
  const watchedLines = watch("methodLines");

  return (
    <div className="sp-lines">
      {fields.map((field, index) => {
        const selectedMethodId = watchedLines?.[index]?.paymentMethodId;
        const selectedMethod = selectedMethodId ? methodsById.get(selectedMethodId) : undefined;
        const isCheck = selectedMethod?.detailType === "Check";
        const lineErrors = errors.methodLines?.[index];

        return (
          <div key={field.id} className="sp-line-row">
            <ZHField label="Medio de pago" required error={lineErrors?.paymentMethodId?.message}>
              <ZhSelect
                className="zh-input"
                disabled={disabled}
                {...register(`methodLines.${index}.paymentMethodId` as const)}
              >
                <option value="">Seleccione...</option>
                {methods.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField
              label="Caja / cuenta bancaria"
              required
              error={lineErrors?.financialDestinationId?.message}
            >
              <ZhSelect
                className="zh-input"
                disabled={disabled}
                {...register(`methodLines.${index}.financialDestinationId` as const)}
              >
                <option value="">Seleccione...</option>
                {destinations.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField label="Monto" required error={lineErrors?.amount?.message}>
              <ZhDecimalInput
                decimals={2}
                positiveOnly
                disabled={disabled}
                {...register(`methodLines.${index}.amount` as const, {
                  valueAsNumber: true,
                  setValueAs: (v) => (v === "" ? null : Number(v)),
                })}
              />
            </ZHField>

            {isCheck && (
              <>
                <ZHField
                  label="Número de cheque"
                  required
                  error={lineErrors?.checkNumber?.message}
                >
                  <ZhTextInput
                    className="zh-input"
                    maxLength={30}
                    disabled={disabled}
                    {...register(`methodLines.${index}.checkNumber` as const)}
                  />
                </ZHField>
                <ZHField label="Fecha del cheque" required error={lineErrors?.checkDate?.message}>
                  <ZhDateInput
                    className="zh-input"
                    disabled={disabled}
                    {...register(`methodLines.${index}.checkDate` as const)}
                  />
                </ZHField>
              </>
            )}

            <ZHField label="Referencia (opcional)" error={lineErrors?.referenceNumber?.message}>
              <ZhTextInput
                className="zh-input"
                maxLength={60}
                disabled={disabled}
                {...register(`methodLines.${index}.referenceNumber` as const)}
              />
            </ZHField>

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

      {typeof errors.methodLines?.message === "string" && (
        <p className="zh-field-hint zh-field-hint--error">{errors.methodLines.message}</p>
      )}

      <ZHBtn
        type="button"
        variant="secondary"
        size="sm"
        disabled={disabled}
        onClick={() =>
          append({
            paymentMethodId: "",
            financialDestinationId: "",
            amount: 0,
            referenceNumber: "",
            checkNumber: "",
            checkDate: "",
            notes: "",
          })
        }
      >
        + Agregar medio de pago
      </ZHBtn>
    </div>
  );
}
