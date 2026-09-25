import { useFieldArray, useFormContext } from "react-hook-form";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZhDateInput, ZhDecimalInput, ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import type { PaymentMethodDto } from "../../sales/facades/paymentMethodLookupFacade";
import type { CompanyBankAccountDto } from "../../finance/api/bankAccountService";
import type { CashRegisterDto } from "../../caja/api/cajaService";
import type { RegisterSupplierPaymentFormValues } from "../../../schemas/supplier-payments/registerSupplierPaymentSchema";

interface Props {
  methods: PaymentMethodDto[];
  bankAccounts: CompanyBankAccountDto[];
  cashRegisters: CashRegisterDto[];
  disabled?: boolean;
}

/**
 * Líneas dinámicas de medio de pago — un pago puede tener varios (transferencia, cheque,
 * efectivo, cualquier otro PaymentMethod activo del catálogo). Cheque exige número y fecha
 * (PaymentMethod.DetailType === "Check", catálogo — no una lista hardcodeada de códigos).
 */
export function SupplierPaymentMethodLinesEditor({
  methods,
  bankAccounts,
  cashRegisters,
  disabled,
}: Props) {
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
              error={lineErrors?.destination?.message}
            >
              <ZhSelect
                className="zh-input"
                disabled={disabled}
                {...register(`methodLines.${index}.destination` as const)}
              >
                <option value="">Seleccione...</option>
                {bankAccounts.map((b) => (
                  <option key={b.id} value={`bank:${b.id}`}>
                    Banco: {b.displayName}
                  </option>
                ))}
                {cashRegisters.map((c) => (
                  <option key={c.id} value={`cash:${c.id}`}>
                    Caja: {c.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>

            <ZHField label="Monto" required error={lineErrors?.amount?.message}>
              <ZhDecimalInput
                precision="money"
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
            destination: "",
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
