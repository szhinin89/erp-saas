import { z } from "zod";

// ACCOUNTING-07 — espejo de CreateAccountingPeriodCommandValidator (ERP.Application). Contigüidad,
// orden frente a períodos cerrados, número correlativo y ejercicio = año de las fechas los decide el
// backend (AccountingPeriodStructure/AccountingPeriod) y llegan por applyServerErrors/409.
export const accountingPeriodSchema = z
  .object({
    fiscalYear: z
      .number({ invalid_type_error: "El ejercicio es obligatorio." })
      .int()
      .min(2000, "El ejercicio es obligatorio."),
    periodNumber: z
      .number({ invalid_type_error: "El número debe estar entre 1 y 12." })
      .int()
      .min(1, "El número debe estar entre 1 y 12.")
      .max(12, "El número debe estar entre 1 y 12."),
    startDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "La fecha de inicio es obligatoria."),
    endDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "La fecha de fin es obligatoria."),
  })
  .refine((v) => v.endDate >= v.startDate, {
    path: ["endDate"],
    message: "La fecha de fin no puede ser anterior a la fecha de inicio.",
  });

export type AccountingPeriodFormValues = z.infer<typeof accountingPeriodSchema>;
