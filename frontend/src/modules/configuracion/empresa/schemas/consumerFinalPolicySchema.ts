import { z } from "zod";

export const consumerFinalMaxAmountSchema = z.object({
  consumerFinalMaxAmount: z.coerce
    .number()
    .min(0, "El monto no puede ser negativo."),
});

export type ConsumerFinalMaxAmountValues = z.infer<
  typeof consumerFinalMaxAmountSchema
>;
