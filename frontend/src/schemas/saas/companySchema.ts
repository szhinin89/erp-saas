import { z } from 'zod';

/** Alineado a `CreateCompanyWithAdminRequest` — validación cliente en español. */
export const createCompanyWithAdminSchema = z.object({
  tenantName: z.string().min(1, 'El nombre del tenant es obligatorio'),
  tenantSlug: z.string().min(1, 'El slug es obligatorio'),
  ruc: z.string().optional(),
  shortName: z.string().optional(),
  tradeName: z.string().optional(),
  dinardap: z.string().optional(),
  logoUrl: z.string().optional(),
  displayOrder: z.coerce.number().int('El orden de visualización debe ser un número entero'),
  priority: z.coerce.number().int('La prioridad debe ser un número entero'),
  adminFirstName: z.string().min(1, 'El nombre del administrador es obligatorio'),
  adminLastName: z.string().min(1, 'El apellido del administrador es obligatorio'),
  adminEmail: z.string().min(1, 'El correo del administrador es obligatorio').email('Correo del administrador no válido'),
  adminPassword: z.string().min(8, 'La contraseña del administrador debe tener al menos 8 caracteres'),
  passwordResetMode: z.coerce.number().int().optional(),
});

export type CreateCompanyFormValues = z.infer<typeof createCompanyWithAdminSchema>;
