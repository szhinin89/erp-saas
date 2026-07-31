import { z } from "zod";

// ── Draft (crear/editar) ────────────────────────────────────────────────
// Espejo de CreateSalesReturnDraftValidator/UpdateSalesReturnDraftValidator
// (ERP.Application) — la fuente de verdad de la regla de negocio sigue siendo
// el backend; esto es solo feedback inmediato de UI.

export const salesReturnLineSchema = z.object({
  invoiceDetailId: z.string().min(1),
  quantity: z.coerce.number().positive("La cantidad devuelta debe ser mayor a cero."),
});

export type SalesReturnLineFormValues = z.infer<typeof salesReturnLineSchema>;

export const salesReturnDraftSchema = z.object({
  salesInvoiceId: z.string().min(1, "Seleccione la factura origen."),
  reason: z
    .string()
    .min(1, "El motivo de la devolución es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
  lines: z
    .array(salesReturnLineSchema)
    .min(1, "Seleccione al menos una línea con cantidad a devolver."),
});

export type SalesReturnDraftFormValues = z.infer<typeof salesReturnDraftSchema>;

export function emptySalesReturnDraftForm(): SalesReturnDraftFormValues {
  return { salesInvoiceId: "", reason: "", lines: [] };
}

// ── Autorización (asignación de reembolso) ─────────────────────────────
// Espejo de AuthorizeSalesReturnCommandValidator — Σ Amount debe coincidir
// exactamente con el total devuelto (mismo invariante que valida el dominio
// en SalesReturn.Authorize()); aquí solo se anticipa para no hacer un viaje
// al servidor con una asignación que el backend rechazará seguro.

export const refundAllocationSchema = z.object({
  method: z.enum(["Cash", "ReceivableCredit"], {
    errorMap: () => ({ message: "La forma de reembolso es obligatoria." }),
  }),
  amount: z.coerce.number().positive("El monto debe ser mayor a cero."),
});

export type RefundAllocationFormValues = z.infer<typeof refundAllocationSchema>;

export function buildAuthorizeSalesReturnSchema(grandTotal: number) {
  return z
    .object({
      refundAllocations: z
        .array(refundAllocationSchema)
        .min(1, "Debe indicar al menos una asignación de reembolso."),
    })
    .superRefine((data, ctx) => {
      const sum = data.refundAllocations.reduce((acc, a) => acc + (a.amount || 0), 0);
      if (Math.abs(sum - grandTotal) > 0.01) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          path: ["refundAllocations"],
          message: `El total de las asignaciones (${sum.toFixed(2)}) no coincide con el total devuelto (${grandTotal.toFixed(2)}).`,
        });
      }
    });
}

export type AuthorizeSalesReturnFormValues = z.infer<
  ReturnType<typeof buildAuthorizeSalesReturnSchema>
>;
