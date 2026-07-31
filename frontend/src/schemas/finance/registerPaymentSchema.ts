import { z } from "zod";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — validación de interfaz para el modal de registro
 * de pago (CxP). Mismo criterio que registerCollectionSchema.ts (Sales): el límite real lo valida
 * el backend (PurchasePayable.RegisterPayment).
 */
export function buildRegisterPaymentSchema(maxAmount: number) {
  return z.object({
    amount: z
      .number({ invalid_type_error: "El monto es obligatorio." })
      .positive("El monto del pago debe ser mayor a cero.")
      .max(
        maxAmount,
        `El monto no puede superar el saldo pendiente (${maxAmount.toFixed(2)}).`,
      ),
    installmentId: z.string().optional().nullable(),
    paymentMethodId: z.string().optional().nullable(),
    reference: z.string().max(200, "Máximo 200 caracteres.").optional(),
  });
}

export type RegisterPaymentFormValues = z.infer<
  ReturnType<typeof buildRegisterPaymentSchema>
>;
