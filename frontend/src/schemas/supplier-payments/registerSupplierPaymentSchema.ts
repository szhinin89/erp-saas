import { z } from "zod";
import { formatMoney } from "../../lib/sanitizers";

/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — validación de interfaz del formulario de registro de Pagos a
 * Proveedores. Cubre exclusivamente reglas verificables con los datos propios del formulario
 * (campos obligatorios, montos > 0, Σaplicaciones ≤ Σmedios — ZH-SUPPLIER-PAYMENT-UNAPPLIED-
 * ADVANCE-02C: el remanente es anticipo); las reglas que dependen de datos externos cargados en
 * runtime (PaymentMethod.DetailType para exigir cheque, saldo pendiente real de cada cuota) se
 * validan de forma imperativa en el submit handler — evita depender de que
 * react-hook-form recapture un resolver de Zod construido con datos que todavía no terminaron de
 * cargar al montar la página.
 */

export const supplierPaymentMethodLineSchema = z.object({
  paymentMethodId: z.string().min(1, "El medio de pago es obligatorio."),
  /** Codifica el destino elegido como "bank:<id>" o "cash:<id>". */
  destination: z.string().min(1, "La caja o cuenta bancaria es obligatoria."),
  amount: z
    .number({ invalid_type_error: "El monto es obligatorio." })
    .positive("El monto debe ser mayor a cero."),
  referenceNumber: z.string().max(60, "Máximo 60 caracteres.").optional().nullable(),
  checkNumber: z.string().max(30, "Máximo 30 caracteres.").optional().nullable(),
  checkDate: z.string().optional().nullable(),
  /** 02A — fecha efectiva de la transacción bancaria (solo destinos "bank:"); vacía ⇒ fecha del pago. */
  transactionDate: z.string().optional().nullable(),
  notes: z.string().max(500, "Máximo 500 caracteres.").optional().nullable(),
});

export const supplierPaymentApplicationLineSchema = z.object({
  accountsPayableInstallmentId: z.string().min(1, "La cuota es obligatoria."),
  amountApplied: z
    .number({ invalid_type_error: "El monto es obligatorio." })
    .positive("El monto debe ser mayor a cero."),
});

// ZH-DESIGN-SYSTEM-PRECISION-05 — `moneyDecimals` solo decide la REPRESENTACIÓN de las sumas del
// mensaje (caller: usePrecisionDecimals("money")); la tolerancia de la regla no cambia.
// ZH-SUPPLIER-PAYMENT-UNAPPLIED-ADVANCE-02C — `allowWithoutPayable` es un GETTER (no un valor): el
// resolver se construye al montar, antes de que cargue la política de empresa; leerla en cada
// validación evita capturar un valor viejo. Sin getter ⇒ false (fail-closed; el backend la aplica).
export function buildRegisterSupplierPaymentSchema(
  moneyDecimals: number,
  options: { allowWithoutPayable?: () => boolean } = {},
) {
  return z
    .object({
      supplierId: z.string().min(1, "El proveedor es obligatorio."),
      paymentDate: z.string().min(1, "La fecha es obligatoria."),
      receiptNumber: z.string().max(30, "Máximo 30 caracteres.").optional().nullable(),
      methodLines: z
        .array(supplierPaymentMethodLineSchema)
        .min(1, "Debe agregar al menos un medio de pago."),
      // 02C — cero cuotas solo si la empresa lo permite (payables.allow_supplier_payment_without_payable);
      // ver superRefine.
      applicationLines: z.array(supplierPaymentApplicationLineSchema),
    })
    .superRefine((data, ctx) => {
      if (data.applicationLines.length === 0 && !(options.allowWithoutPayable?.() ?? false)) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["applicationLines"],
          message: "Debe seleccionar al menos una cuota.",
        });
      }
      const totalMethods = data.methodLines.reduce((sum, l) => sum + (l.amount || 0), 0);
      const totalApplications = data.applicationLines.reduce(
        (sum, l) => sum + (l.amountApplied || 0),
        0,
      );
      if (totalApplications - totalMethods > 0.005) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["methodLines"],
          message:
            `La suma de las cuotas aplicadas (${formatMoney(totalApplications, moneyDecimals)}) no puede ` +
            `superar la suma de los medios de pago (${formatMoney(totalMethods, moneyDecimals)}).`,
        });
      }
    });
}

export type RegisterSupplierPaymentFormValues = z.infer<
  ReturnType<typeof buildRegisterSupplierPaymentSchema>
>;
