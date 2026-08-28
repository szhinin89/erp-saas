import { z } from "zod";

/**
 * SUPPLIER-PAYMENTS-FRONTEND-15E — validación de interfaz del formulario de registro de Pagos a
 * Proveedores. Cubre exclusivamente reglas verificables con los datos propios del formulario
 * (campos obligatorios, montos > 0, Σmedios = Σaplicaciones); las reglas que dependen de datos
 * externos cargados en runtime (PaymentMethod.DetailType para exigir cheque, saldo pendiente real
 * de cada cuota) se validan de forma imperativa en el submit handler — evita depender de que
 * react-hook-form recapture un resolver de Zod construido con datos que todavía no terminaron de
 * cargar al montar la página.
 */

export const supplierPaymentMethodLineSchema = z.object({
  paymentMethodId: z.string().min(1, "El medio de pago es obligatorio."),
  financialDestinationId: z.string().min(1, "La caja o cuenta bancaria es obligatoria."),
  amount: z
    .number({ invalid_type_error: "El monto es obligatorio." })
    .positive("El monto debe ser mayor a cero."),
  referenceNumber: z.string().max(60, "Máximo 60 caracteres.").optional().nullable(),
  checkNumber: z.string().max(30, "Máximo 30 caracteres.").optional().nullable(),
  checkDate: z.string().optional().nullable(),
  notes: z.string().max(500, "Máximo 500 caracteres.").optional().nullable(),
});

export const supplierPaymentApplicationLineSchema = z.object({
  accountsPayableInstallmentId: z.string().min(1, "La cuota es obligatoria."),
  amountApplied: z
    .number({ invalid_type_error: "El monto es obligatorio." })
    .positive("El monto debe ser mayor a cero."),
});

export const registerSupplierPaymentSchema = z
  .object({
    supplierId: z.string().min(1, "El proveedor es obligatorio."),
    paymentDate: z.string().min(1, "La fecha es obligatoria."),
    receiptNumber: z.string().max(30, "Máximo 30 caracteres.").optional().nullable(),
    methodLines: z
      .array(supplierPaymentMethodLineSchema)
      .min(1, "Debe agregar al menos un medio de pago."),
    applicationLines: z
      .array(supplierPaymentApplicationLineSchema)
      .min(1, "Debe seleccionar al menos una cuota."),
  })
  .superRefine((data, ctx) => {
    const totalMethods = data.methodLines.reduce((sum, l) => sum + (l.amount || 0), 0);
    const totalApplications = data.applicationLines.reduce(
      (sum, l) => sum + (l.amountApplied || 0),
      0,
    );
    if (Math.abs(totalMethods - totalApplications) > 0.005) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["methodLines"],
        message:
          `La suma de los medios de pago (${totalMethods.toFixed(2)}) debe ser igual a la ` +
          `suma de las cuotas aplicadas (${totalApplications.toFixed(2)}).`,
      });
    }
  });

export type RegisterSupplierPaymentFormValues = z.infer<typeof registerSupplierPaymentSchema>;
