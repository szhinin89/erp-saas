import { z } from "zod";

const rucSchema = z
  .string()
  .trim()
  .length(13, "El RUC debe tener 13 caracteres.");

export const companyManagementFormSchema = z.object({
  tenantId: z.string().trim().optional().or(z.literal("")),
  taxId: rucSchema,
  legalName: z
    .string()
    .trim()
    .min(1, "La razón social es obligatoria.")
    .max(200),
  tradeName: z.string().trim().max(200).optional().or(z.literal("")),
  isActive: z.boolean().optional(),
});

export type CompanyManagementFormValues = z.infer<
  typeof companyManagementFormSchema
>;

/**
 * Alta de empresa desde Admin Core: además de la identidad, el administrador inicial (usuario,
 * nombres, apellidos y correo). Sin contraseña: la genera el backend y se entrega una sola vez.
 */
export const adminCoreCompanyCreateSchema = companyManagementFormSchema.extend({
  adminUsername: z
    .string()
    .trim()
    .regex(
      /^[a-zA-Z0-9][a-zA-Z0-9._-]{1,48}[a-zA-Z0-9]$/,
      "El usuario debe tener 3-50 caracteres: letras, números, punto, guion o guion bajo.",
    ),
  adminFirstName: z
    .string()
    .trim()
    .min(1, "Los nombres del administrador son obligatorios.")
    .max(50),
  adminLastName: z
    .string()
    .trim()
    .min(1, "Los apellidos del administrador son obligatorios.")
    .max(50),
  adminEmail: z
    .string()
    .trim()
    .min(1, "El correo del administrador es obligatorio.")
    .email("Ingresa un correo válido.")
    .max(256),
});

export type AdminCoreCompanyCreateValues = z.infer<
  typeof adminCoreCompanyCreateSchema
>;
