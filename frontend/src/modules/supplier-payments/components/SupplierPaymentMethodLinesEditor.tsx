import { useEffect, useRef } from "react";
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
 *
 * ZH-SUPPLIER-PAYMENT-CASH-TRANSFER-HARDENING-02A — el catálogo decide el destino: un medio con
 * `affectsPhysicalCash` solo ofrece cajas; cualquier otro solo cuentas bancarias; los medios de
 * crédito (`isCreditAllowed`) no se ofrecen. Las fuentes bancarias capturan fecha de transacción y
 * número de operación (obligatorio si el medio `requiresReference`) — base de la futura
 * conciliación bancaria. El backend vuelve a validar todo (fail-closed).
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
    setValue,
    formState: { errors },
  } = useFormContext<RegisterSupplierPaymentFormValues>();
  const { fields, append, remove } = useFieldArray({ control, name: "methodLines" });
  const payableMethods = methods.filter((m) => !m.isCreditAllowed);
  const methodsById = new Map(payableMethods.map((m) => [m.id, m]));
  const watchedLines = watch("methodLines");
  const paymentDate = watch("paymentDate");

  // 02A-FINAL — comodidad visible, no relleno silencioso: la primera vez que una línea pasa a
  // medio bancario se precarga su fecha de transacción con la fecha del pago (el usuario la ve y
  // puede cambiarla). Una sola vez por línea: si el usuario la borra, queda vacía y el submit la
  // exige — el backend nunca completa la fecha por su cuenta.
  const prefilledLineIds = useRef(new Set<string>());
  useEffect(() => {
    fields.forEach((field, index) => {
      const line = watchedLines?.[index];
      const method = line?.paymentMethodId ? methodsById.get(line.paymentMethodId) : undefined;
      const isBank = method !== undefined && !method.affectsPhysicalCash;
      if (!isBank) {
        prefilledLineIds.current.delete(field.id);
        return;
      }
      if (prefilledLineIds.current.has(field.id)) return;
      prefilledLineIds.current.add(field.id);
      if (!line?.transactionDate) setValue(`methodLines.${index}.transactionDate`, paymentDate ?? "");
    });
  });

  return (
    <div className="sp-lines">
      {fields.map((field, index) => {
        const selectedMethodId = watchedLines?.[index]?.paymentMethodId;
        const selectedMethod = selectedMethodId ? methodsById.get(selectedMethodId) : undefined;
        const isCheck = selectedMethod?.detailType === "Check";
        const isCashMethod = selectedMethod?.affectsPhysicalCash === true;
        const isBankMethod = selectedMethod !== undefined && !isCashMethod;
        const requiresOperationNumber = isBankMethod && !isCheck && selectedMethod.requiresReference;
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
                {payableMethods.map((m) => (
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
                <option value="">
                  {selectedMethod ? "Seleccione..." : "Seleccione primero el medio de pago"}
                </option>
                {isBankMethod &&
                  bankAccounts.map((b) => (
                    <option key={b.id} value={`bank:${b.id}`}>
                      Banco: {b.displayName}
                    </option>
                  ))}
                {isCashMethod &&
                  cashRegisters.map((c) => (
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

            {isBankMethod && (
              <ZHField
                label="Fecha de transacción bancaria"
                required
                error={lineErrors?.transactionDate?.message}
              >
                <ZhDateInput
                  className="zh-input"
                  disabled={disabled}
                  {...register(`methodLines.${index}.transactionDate` as const)}
                />
              </ZHField>
            )}

            <ZHField
              label={requiresOperationNumber ? "Número de operación bancaria" : "Referencia (opcional)"}
              required={requiresOperationNumber}
              error={lineErrors?.referenceNumber?.message}
            >
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
            transactionDate: "",
            notes: "",
          })
        }
      >
        + Agregar medio de pago
      </ZHBtn>
    </div>
  );
}
