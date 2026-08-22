import { z } from "zod";

/**
 * Cada schema valida SOLO el subconjunto de campos editable en esta primera versión (los
 * conectados a efecto real en backend — ver plan CONFIG-DYNAMIC-OPERATIONS-01). Los demás campos
 * de cada grupo no se muestran ni se validan aquí; se preservan tal cual vienen del GET al armar
 * el payload de guardado (ver useXxxPreferencesSection.ts).
 */

export const salesPosPreferencesSchema = z.object({
  allowManualDiscount: z.boolean(),
  maxDiscountPercent: z.coerce.number().min(0).max(100),
  allowSellWithoutStock: z.boolean(),
});
export type SalesPosPreferencesValues = z.infer<typeof salesPosPreferencesSchema>;

export const cashPreferencesSchema = z.object({
  requireReasonForDifference: z.boolean(),
  allowCloseWithDifference: z.boolean(),
  maxAllowedDifference: z.coerce.number().min(0),
});
export type CashPreferencesValues = z.infer<typeof cashPreferencesSchema>;

export const purchasesPreferencesSchema = z.object({
  allowConfirmWithoutReceptionXml: z.boolean(),
});
export type PurchasesPreferencesValues = z.infer<typeof purchasesPreferencesSchema>;

export const printingPreferencesSchema = z.object({
  salesReceiptMode: z.enum(["AskBeforePrint", "AlwaysPrint", "NeverAutoPrint"]),
  salesReceiptCopies: z.coerce.number().int().min(1).max(3),
});
export type PrintingPreferencesValues = z.infer<typeof printingPreferencesSchema>;

export const electronicDocumentsPreferencesSchema = z.object({
  emailOnAuthorization: z.boolean(),
});
export type ElectronicDocumentsPreferencesValues = z.infer<
  typeof electronicDocumentsPreferencesSchema
>;
