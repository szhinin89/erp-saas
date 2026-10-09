import { z } from "zod";
import { isValidIsoDate } from "../../../lib/formatters/dateFormatters";

/** IL-5A — Company.OpeningBalanceDate: fecha de negocio "YYYY-MM-DD" (ADR-034). */
export const openingBalanceDateSchema = z.object({
  openingBalanceDate: z
    .string()
    .min(1, "La fecha de apertura es obligatoria.")
    .refine(isValidIsoDate, "La fecha de apertura no es válida."),
});

export type OpeningBalanceDateFormValues = z.infer<typeof openingBalanceDateSchema>;
