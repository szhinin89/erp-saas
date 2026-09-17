import { z } from "zod";

// BANK-CATALOG-01 — espejo de CreateBankValidator/UpdateBankValidator (ERP.Application). Code y
// CountryCode son inmutables tras crear (Bank.Update no los acepta).

export const createBankSchema = z.object({
  code: z
    .string()
    .min(1, "El código es obligatorio.")
    .max(30, "El código no puede superar 30 caracteres."),
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(150, "El nombre no puede superar 150 caracteres."),
  shortName: z
    .string()
    .max(50, "El nombre corto no puede superar 50 caracteres.")
    .optional()
    .default(""),
});

export type CreateBankFormValues = z.infer<typeof createBankSchema>;

export function emptyCreateBankForm(): CreateBankFormValues {
  return { code: "", name: "", shortName: "" };
}

export const editBankSchema = z.object({
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(150, "El nombre no puede superar 150 caracteres."),
  shortName: z
    .string()
    .max(50, "El nombre corto no puede superar 50 caracteres.")
    .optional()
    .default(""),
});

export type EditBankFormValues = z.infer<typeof editBankSchema>;
