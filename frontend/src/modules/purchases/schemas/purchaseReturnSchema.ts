import { z } from "zod";

// Line input used exclusively by the Return modality of supplier credit notes.
export const purchaseReturnLineSchema = z.object({
  originalInvoiceDetailId: z.string().min(1),
  quantity: z.coerce.number().positive("La cantidad devuelta debe ser mayor a cero."),
});

export type PurchaseReturnLineFormValues = z.infer<typeof purchaseReturnLineSchema>;
