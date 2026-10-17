import { z } from "zod";

/**
 * ACCOUNTING-11 — líneas efectivas de una regla contable. La forma por hecho (mínimo de líneas,
 * Debe/Haber, tipos de monto permitidos) y la validez de cada cuenta las decide el backend con
 * PostingFactCatalog; aquí solo se exige que cada línea esté completa.
 */
export const postingRuleSchema = z.object({
  lines: z.array(
    z.object({
      accountId: z.string().uuid("Seleccione una cuenta"),
      nature: z.enum(["Debit", "Credit"]),
      amountKind: z.string().min(1, "Seleccione el tipo de monto"),
    }),
  ),
});
export type PostingRuleFormValues = z.infer<typeof postingRuleSchema>;
