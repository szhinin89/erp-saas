import { z } from "zod";

// FLOW-READY-02C.3 — espejo de CreateDraftPurchaseCreditNoteValidator/
// UpdatePurchaseCreditNoteDraftValidator (ERP.Application). La fuente de
// verdad de la regla de negocio sigue siendo el backend (incluyendo el
// bloqueo por excedente de saldo, que nunca se recalcula aquí) — esto es
// solo feedback inmediato de UI, mismo criterio que purchaseReturnSchema.ts.
// Alcance v1 (§0.1 del diseño): solo cubre el tipo Descuento/Promoción —
// nunca item/bodega/cantidad, la devolución física delega en
// purchaseReturnSchema.ts sin cambios.

export const purchaseCreditNoteLineSchema = z.object({
  description: z
    .string()
    .min(1, "La descripción de la línea es obligatoria.")
    .max(500, "La descripción no puede superar 500 caracteres."),
  subtotal: z.coerce.number().positive("El subtotal de la línea debe ser mayor a cero."),
  vatCode: z.string().nullable().optional(),
  vatRate: z.coerce.number().nullable().optional(),
  vatAmount: z.coerce.number().min(0, "El IVA de la línea no puede ser negativo."),
});

export type PurchaseCreditNoteLineFormValues = z.infer<
  typeof purchaseCreditNoteLineSchema
>;

// FLOW-READY-02C-R1.2 — flujo principal de Discount: una línea por resumen fiscal real de la
// compra (PurchaseInvoiceTaxSummary). El cliente solo captura la referencia y la base a descontar
// — VatCode/VatRate/IceCode/IceRate se heredan siempre del resumen de origen en el backend, nunca
// se editan aquí.
export const purchaseCreditNoteTaxSummaryLineSchema = z.object({
  sourcePurchaseInvoiceTaxSummaryId: z.string().min(1),
  taxableBase: z.coerce.number().positive("La base de descuento debe ser mayor a cero."),
});

export type PurchaseCreditNoteTaxSummaryLineFormValues = z.infer<
  typeof purchaseCreditNoteTaxSummaryLineSchema
>;

export const purchaseCreditNoteDraftSchema = z.object({
  creditNoteNumber: z
    .string()
    .min(1, "El número de nota de crédito es obligatorio.")
    .max(17, "El número no puede superar 17 caracteres."),
  accessKey: z.string().max(49, "La clave de acceso no puede superar 49 caracteres.").optional(),
  authorizationNumber: z
    .string()
    .max(49, "El número de autorización no puede superar 49 caracteres.")
    .optional(),
  authorizationDate: z.string().optional(),
  issueDate: z.string().min(1, "La fecha de emisión es obligatoria."),
  reason: z
    .string()
    .min(1, "El motivo/concepto es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
  lines: z.array(purchaseCreditNoteLineSchema),
  taxSummaryLines: z.array(purchaseCreditNoteTaxSummaryLineSchema),
});

export type PurchaseCreditNoteDraftFormValues = z.infer<
  typeof purchaseCreditNoteDraftSchema
>;

export function emptyPurchaseCreditNoteDraftForm(): PurchaseCreditNoteDraftFormValues {
  return {
    creditNoteNumber: "",
    accessKey: "",
    authorizationNumber: "",
    authorizationDate: "",
    issueDate: "",
    reason: "",
    lines: [],
    taxSummaryLines: [],
  };
}

// ── Cancelación ─────────────────────────────────────────────────────────
// Espejo de CancelPurchaseCreditNoteValidator.

export const cancelPurchaseCreditNoteSchema = z.object({
  reason: z
    .string()
    .min(1, "El motivo de la cancelación es obligatorio.")
    .max(500, "El motivo no puede superar 500 caracteres."),
});

export type CancelPurchaseCreditNoteFormValues = z.infer<
  typeof cancelPurchaseCreditNoteSchema
>;
