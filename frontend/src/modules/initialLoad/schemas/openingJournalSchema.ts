import { z } from "zod";

/**
 * IL-8D — formulario del ASI de apertura. Solo forma del dato (nivel 1, form-validation.md): cuenta
 * elegida y un monto en un solo lado. Cuadre Debe = Haber, cuenta puente en 0, cuentas de control
 * bloqueadas y período abierto los valida SOLO el backend (IL-8A).
 */
export const openingJournalLineSchema = z
  .object({
    accountId: z.string().min(1, "Seleccione una cuenta contable."),
    debit: z.coerce.number().min(0, "El monto no puede ser negativo."),
    credit: z.coerce.number().min(0, "El monto no puede ser negativo."),
    description: z.string().max(500, "La descripción admite como máximo 500 caracteres."),
  })
  .refine((l) => l.debit > 0 !== l.credit > 0, {
    message: "Ingrese un monto mayor a cero solo en Debe o solo en Haber.",
    path: ["debit"],
  });

export const openingJournalSchema = z.object({
  lines: z.array(openingJournalLineSchema).min(2, "El asiento de apertura necesita al menos 2 líneas."),
});

export type OpeningJournalFormValues = z.input<typeof openingJournalSchema>;
export type OpeningJournalFormOutput = z.output<typeof openingJournalSchema>;

/** IL-8B — motivo obligatorio de "Corregir apertura" (máx. JournalEntry.ReverseReasonMaxLength). */
export const correctOpeningSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, "El motivo de la corrección es obligatorio.")
    .max(500, "El motivo admite como máximo 500 caracteres."),
});

export type CorrectOpeningFormValues = z.infer<typeof correctOpeningSchema>;
