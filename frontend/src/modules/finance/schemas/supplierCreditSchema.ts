import { z } from "zod";
import { formatMoney } from "../../../lib/sanitizers";

// ── Aplicar crédito ────────────────────────────────────────────────────
// Espejo de ApplySupplierCreditValidator — amount ≤ AvailableAmount es la
// fuente de verdad del backend (§13.4); aquí solo se anticipa para no hacer
// un viaje al servidor con un monto que el backend rechazará seguro.

// ZH-DESIGN-SYSTEM-PRECISION-04G — `moneyDecimals` solo decide la REPRESENTACIÓN del saldo en el
// mensaje (el caller React lo resuelve con usePrecisionDecimals("money")); la regla no cambia.
export function buildApplySupplierCreditSchema(availableAmount: number, moneyDecimals: number) {
  return z.object({
    targetPurchasePayableId: z.string().min(1, "Seleccione la cuenta por pagar destino."),
    amount: z.coerce
      .number()
      .positive("El monto debe ser mayor a cero.")
      .max(
        availableAmount,
        `El monto no puede exceder el saldo disponible (${formatMoney(availableAmount, moneyDecimals)}).`,
      ),
  });
}

export type ApplySupplierCreditFormValues = z.infer<
  ReturnType<typeof buildApplySupplierCreditSchema>
>;

// ── Registrar reembolso ─────────────────────────────────────────────────
// Espejo de RegisterSupplierCreditRefundValidator — externalReference
// condicionalmente obligatorio según PaymentMethod.RequiresReference,
// consultado del catálogo real (nunca hardcodeado, mismo criterio que el
// plan Fase 13 cambio exacto #3).

export function buildRegisterSupplierCreditRefundSchema(
  availableAmount: number,
  requiresReference: boolean,
  moneyDecimals: number,
) {
  return z
    .object({
      /** Codifica el destino elegido como "bank:<id>" o "cash:<id>". */
      destination: z.string().min(1, "Seleccione la cuenta bancaria o caja."),
      paymentMethodCode: z.string().min(1, "Seleccione la forma de pago."),
      amount: z.coerce
        .number()
        .positive("El monto debe ser mayor a cero.")
        .max(
          availableAmount,
          `El monto no puede exceder el saldo disponible (${formatMoney(availableAmount, moneyDecimals)}).`,
        ),
      effectiveDate: z.string().min(1, "La fecha efectiva es obligatoria."),
      externalReference: z.string().optional().default(""),
    })
    .superRefine((data, ctx) => {
      if (requiresReference && !data.externalReference.trim()) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["externalReference"],
          message: "La forma de pago seleccionada requiere una referencia.",
        });
      }
    });
}

export type RegisterSupplierCreditRefundFormValues = z.infer<
  ReturnType<typeof buildRegisterSupplierCreditRefundSchema>
>;

// ── Reversa de reembolso ────────────────────────────────────────────────

export const reverseSupplierCreditRefundSchema = z.object({
  reason: z
    .string()
    .min(1, "El motivo de la reversa es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
  effectiveDate: z.string().min(1, "La fecha efectiva es obligatoria."),
});

export type ReverseSupplierCreditRefundFormValues = z.infer<
  typeof reverseSupplierCreditRefundSchema
>;
