import { z } from "zod";

/** DocumentSequence.MaxSequentialValue (backend) — el secuencial SRI se formatea D9. */
export const MAX_SEQUENTIAL_VALUE = 999_999_999;

export const documentSequenceConfigureSchema = z.object({
  nextNumber: z.coerce
    .number({ invalid_type_error: "Ingresa un número." })
    .int("El secuencial debe ser un número entero.")
    .min(1, "El secuencial debe ser mayor o igual a 1.")
    .max(
      MAX_SEQUENTIAL_VALUE,
      `El secuencial no puede superar ${MAX_SEQUENTIAL_VALUE} (9 dígitos).`,
    ),
});

export type DocumentSequenceConfigureFormValues = z.infer<
  typeof documentSequenceConfigureSchema
>;

export function emptyDocumentSequenceConfigureForm(): DocumentSequenceConfigureFormValues {
  return { nextNumber: 1 };
}
