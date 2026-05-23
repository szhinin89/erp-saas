import { z } from 'zod';

/** Alineado a `CreateCompanyWithAdminRequest` — validación cliente en español. */
export const createCompanyWithAdminSchema = z
  .object({
    subscriberName: z.string().min(1, 'Ingresa el nombre de la empresa.'),
    subscriberSlug: z.string().min(1, 'Ingresa el slug de la empresa.'),
    ruc: z.string().optional(),
    shortName: z.string().optional(),
    tradeName: z.string().optional(),
    dinardap: z.string().optional(),
    logoUrl: z.string().optional(),
    displayOrder: z.coerce.number().int('Ingresa un número entero para el orden.'),
    priority: z.coerce.number().int('Ingresa un número entero para la prioridad.'),
    linkExistingAdmin: z.boolean(),
    adminFirstName: z.string(),
    adminLastName: z.string(),
    adminEmail: z
      .string()
      .min(1, 'Ingresa el correo del administrador.')
      .email('Ingresa un correo válido para el administrador.'),
    adminPassword: z.string(),
    passwordResetMode: z.coerce.number().int().optional(),
  })
  .superRefine((data, ctx) => {
    if (data.linkExistingAdmin) return;
    if (data.adminFirstName.trim().length < 1) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminFirstName'],
        message: 'Ingresa el nombre del administrador.',
      });
    }
    if (data.adminLastName.trim().length < 1) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminLastName'],
        message: 'Ingresa el apellido del administrador.',
      });
    }
    if (data.adminPassword.length < 8) {
      ctx.addIssue({
        code: 'custom',
        path: ['adminPassword'],
        message: 'La contraseña del administrador debe tener al menos 8 caracteres.',
      });
    }
  });

export type CreateCompanyFormValues = z.infer<typeof createCompanyWithAdminSchema>;

/** Edición de datos de empresa existente (operador platform, sin admin). */
export const updateSubscriberCompanySchema = z.object({
  subscriberName: z.string().min(1, 'Ingresa el nombre de la empresa.'),
  subscriberSlug: z.string().min(1, 'Ingresa el slug de la empresa.'),
  ruc: z.string().optional(),
  shortName: z.string().optional(),
  tradeName: z.string().optional(),
  dinardap: z.string().optional(),
  logoUrl: z.string().optional(),
  displayOrder: z.coerce.number().int('Ingresa un número entero para el orden.'),
  priority: z.coerce.number().int('Ingresa un número entero para la prioridad.'),
});

export type UpdateSubscriberCompanyFormValues = z.infer<typeof updateSubscriberCompanySchema>;
