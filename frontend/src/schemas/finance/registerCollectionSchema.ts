import { z } from "zod";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — validación de interfaz para el modal de registro
 * de cobro (CxC). El límite superior real de saldo lo valida el backend (SalesReceivable.
 * RegisterCollection) — este schema solo evita envíos con monto obviamente inválido antes de la
 * petición HTTP, siguiendo el estándar de dos niveles (CLAUDE.md).
 */
export function buildRegisterCollectionSchema(maxAmount: number) {
  return z.object({
    amount: z
      .number({ invalid_type_error: "El monto es obligatorio." })
      .positive("El monto del cobro debe ser mayor a cero.")
      .max(
        maxAmount,
        `El monto no puede superar el saldo pendiente (${maxAmount.toFixed(2)}).`,
      ),
    installmentId: z.string().optional().nullable(),
    paymentMethodId: z.string().optional().nullable(),
    financialDestinationId: z.string().optional().nullable(),
    reference: z.string().max(200, "Máximo 200 caracteres.").optional(),
  });
}

export type RegisterCollectionFormValues = z.infer<
  ReturnType<typeof buildRegisterCollectionSchema>
>;
