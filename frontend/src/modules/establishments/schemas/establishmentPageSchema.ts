import { z } from "zod";

export const establishmentSchema = z.object({
  branchId: z.string().optional().nullable(),
  code: z
    .string()
    .min(1, "El código es obligatorio.")
    .max(3, "Máximo 3 dígitos.")
    .regex(/^\d{1,3}$/, "Solo dígitos numéricos (001-999)."),
  name: z
    .string()
    .min(1, "El nombre es obligatorio.")
    .max(200, "Máximo 200 caracteres."),
  address: z
    .string()
    .min(1, "La dirección fiscal es obligatoria.")
    .max(500, "Máximo 500 caracteres."),
  phone: z
    .string()
    .max(40, "Máximo 40 caracteres.")
    .optional()
    .nullable()
    .or(z.literal("")),
  isMain: z.boolean(),
});

export type EstablishmentFormValues = z.infer<typeof establishmentSchema>;

export function emptyEstablishmentForm(
  branchId?: string | null,
): EstablishmentFormValues {
  return {
    branchId: branchId ?? null,
    code: "",
    name: "",
    address: "",
    phone: "",
    isMain: false,
  };
}
