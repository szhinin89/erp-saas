import { z } from "zod";

// ACCOUNTING-CONTINUITY-01 — espejo de SetAccountingOperationalModeCommandValidator
// (ERP.Application). Las reglas de negocio (empresa obligada, Disabled aún no disponible, vigencia
// posterior a la versión abierta) las decide el backend y llegan por applyServerErrors.
export const operationalModeSchema = z.object({
  mode: z.enum(["Enabled", "Disabled"], {
    errorMap: () => ({ message: "El modo es obligatorio." }),
  }),
  effectiveFrom: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, "La fecha de vigencia es obligatoria."),
  reason: z.string().max(500, "El motivo no puede superar 500 caracteres.").optional().default(""),
});

export type OperationalModeFormValues = z.infer<typeof operationalModeSchema>;
