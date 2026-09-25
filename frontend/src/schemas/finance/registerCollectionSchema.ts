import { z } from "zod";
import { formatMoney } from "../../lib/sanitizers";

/**
 * P0-03 (ERP_CORE_SUMAK_READINESS_AUDIT.md) — validación de interfaz para el modal de registro
 * de cobro (CxC). El límite superior real de saldo lo valida el backend (SalesReceivable.
 * RegisterCollection) — este schema solo evita envíos con monto obviamente inválido antes de la
 * petición HTTP, siguiendo el estándar de dos niveles (CLAUDE.md).
 */
// ZH-DESIGN-SYSTEM-PRECISION-05 — `moneyDecimals` solo decide la REPRESENTACIÓN del saldo en el
// mensaje (caller: usePrecisionDecimals("money")); la regla no cambia.
export function buildRegisterCollectionSchema(maxAmount: number, moneyDecimals: number) {
  return z.object({
    amount: z
      .number({ invalid_type_error: "El monto es obligatorio." })
      .positive("El monto del cobro debe ser mayor a cero.")
      .max(
        maxAmount,
        `El monto no puede superar el saldo pendiente (${formatMoney(maxAmount, moneyDecimals)}).`,
      ),
    installmentId: z.string().optional().nullable(),
    paymentMethodId: z.string().optional().nullable(),
    /** Codifica el destino elegido como "bank:<id>" o "cash:<id>" — se separa en companyBankAccountId/cashRegisterId al enviar. */
    destination: z.string().optional().nullable(),
    reference: z.string().max(200, "Máximo 200 caracteres.").optional(),
  });
}

export type RegisterCollectionFormValues = z.infer<
  ReturnType<typeof buildRegisterCollectionSchema>
>;
