import { z } from "zod";

export const consumerFinalMaxAmountSchema = z.object({
  consumerFinalMaxAmount: z.coerce
    .number()
    .min(0, "settings.company.validation.nonnegative"),
});

export type ConsumerFinalMaxAmountValues = z.infer<
  typeof consumerFinalMaxAmountSchema
>;
