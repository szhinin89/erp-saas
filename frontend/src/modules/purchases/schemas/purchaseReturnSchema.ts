import { z } from "zod";

// ── Draft (crear/editar) ────────────────────────────────────────────────
// Espejo de CreatePurchaseReturnDraftValidator/UpdatePurchaseReturnDraftValidator
// (ERP.Application) — la fuente de verdad de la regla de negocio sigue siendo
// el backend; esto es solo feedback inmediato de UI (mismo criterio que
// salesReturnSchema.ts).

export const purchaseReturnLineSchema = z.object({
  originalInvoiceDetailId: z.string().min(1),
  quantity: z.coerce.number().positive("La cantidad devuelta debe ser mayor a cero."),
});

export type PurchaseReturnLineFormValues = z.infer<typeof purchaseReturnLineSchema>;

export const purchaseReturnDraftSchema = z.object({
  reason: z
    .string()
    .min(1, "El motivo de la devolución es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
  lines: z
    .array(purchaseReturnLineSchema)
    .min(1, "Seleccione al menos una línea con cantidad a devolver."),
});

export type PurchaseReturnDraftFormValues = z.infer<typeof purchaseReturnDraftSchema>;

export function emptyPurchaseReturnDraftForm(): PurchaseReturnDraftFormValues {
  return { reason: "", lines: [] };
}

// ── Cancelación ─────────────────────────────────────────────────────────
// Espejo de CancelPurchaseReturnValidator.

export const cancelPurchaseReturnSchema = z.object({
  reason: z
    .string()
    .min(1, "El motivo de la cancelación es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
});

export type CancelPurchaseReturnFormValues = z.infer<typeof cancelPurchaseReturnSchema>;

// ── Vínculo de Nota de Crédito recibida ─────────────────────────────────
// Espejo de RegisterAndLinkSupplierCreditNoteValidator — la validación
// cuantitativa (tolerancia 0.01 contra el total autorizado) es responsabilidad
// exclusiva del backend (§18.4bis), nunca se recalcula aquí.

export const linkSupplierCreditNoteSchema = z.object({
  accessKey: z.string().min(1, "La clave de acceso es obligatoria."),
  supplierRuc: z.string().min(1, "El RUC/CI del proveedor es obligatorio."),
  supplierName: z.string().min(1, "El nombre del proveedor es obligatorio."),
  invoiceNumber: z.string().min(1, "El número de comprobante es obligatorio."),
  issueDate: z.string().min(1, "La fecha de emisión es obligatoria."),
  subtotal: z.coerce.number().min(0, "El subtotal no puede ser negativo."),
  vatAmount: z.coerce.number().min(0, "El IVA no puede ser negativo."),
  totalAmount: z.coerce.number().positive("El total debe ser mayor a cero."),
  currencyCode: z.string().min(1, "La moneda es obligatoria."),
});

export type LinkSupplierCreditNoteFormValues = z.infer<
  typeof linkSupplierCreditNoteSchema
>;

export function emptyLinkSupplierCreditNoteForm(): LinkSupplierCreditNoteFormValues {
  return {
    accessKey: "",
    supplierRuc: "",
    supplierName: "",
    invoiceNumber: "",
    issueDate: "",
    subtotal: 0,
    vatAmount: 0,
    totalAmount: 0,
    currencyCode: "USD",
  };
}
