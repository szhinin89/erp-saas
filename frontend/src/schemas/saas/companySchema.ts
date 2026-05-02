import { z } from 'zod';

/** Alineado a `CreateCompanyWithAdminRequest` — validación cliente en español. */
export const createCompanyWithAdminSchema = z
  .object({
    tenantName: z.string().min(1, 'El nombre del tenant es obligatorio'),
    tenantSlug: z.string().min(1, 'El slug es obligatorio'),
    ruc: z.string().optional(),
    shortName: z.string().optional(),
    tradeName: z.string().optional(),
    dinardap: z.string().optional(),
    logoUrl: z.string().optional(),
    displayOrder: z.coerce.number().int('El orden de visualización debe ser un número entero'),
    priority: z.coerce.number().int('La prioridad debe ser un número entero'),
    linkExistingAdmin: z.boolean(),
    adminFirstName: z.string(),
    adminLastName: z.string(),
    adminEmail: z.string().min(1, 'El correo del administrador es obligatorio').email('Correo del administrador no válido'),
    adminPassword: z.string(),
    passwordResetMode: z.coerce.number().int().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.linkExistingAdmin) return;
    if (data.adminFirstName.trim().length < 1) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminFirstName'],
        message: 'El nombre del administrador es obligatorio',
      });
    }
    if (data.adminLastName.trim().length < 1) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminLastName'],
        message: 'El apellido del administrador es obligatorio',
      });
    }
    if (data.adminPassword.length < 8) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminPassword'],
        message: 'La contraseña del administrador debe tener al menos 8 caracteres',
      });
    }
  });

export type CreateCompanyFormValues = z.infer<typeof createCompanyWithAdminSchema>;
