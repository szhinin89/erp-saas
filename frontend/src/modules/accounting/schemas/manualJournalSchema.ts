import { z } from "zod";
import { isValidIsoDate } from "../../../lib/formatters/dateFormatters";

export const manualJournalSchema = z.object({
  entryDate: z.string().refine(isValidIsoDate, "Fecha inválida"),
  concept: z.string().trim().min(1, "El concepto es obligatorio").max(500),
  reference: z.string().max(500),
  lines: z.array(z.object({
    accountId: z.string().uuid("Seleccione una cuenta"),
    description: z.string().max(500),
    debit: z.string(),
    credit: z.string(),
  })),
});
export type ManualJournalValues = z.infer<typeof manualJournalSchema>;

export const manualJournalReasonSchema = z.object({
  reason: z.string().trim().min(1, "El motivo es obligatorio").max(500),
  effectiveDate: z.string().refine(isValidIsoDate, "Fecha inválida"),
});
