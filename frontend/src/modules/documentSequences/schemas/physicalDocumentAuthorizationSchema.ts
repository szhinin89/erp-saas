import { z } from "zod";
import { MAX_SEQUENTIAL_VALUE } from "./documentSequencesPageSchema";

const businessDate = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, "Ingresa una fecha válida.");

const sequential = (label: string) =>
  z.coerce
    .number({ invalid_type_error: "Ingresa un número." })
    .int(`El ${label} debe ser un número entero.`)
    .min(1, `El ${label} debe ser mayor o igual a 1.`)
    .max(
      MAX_SEQUENTIAL_VALUE,
      `El ${label} no puede superar ${MAX_SEQUENTIAL_VALUE} (9 dígitos).`,
    );

/** Un año después de la fecha (vigencia máxima, D.E. 430 art. 6), como "YYYY-MM-DD". */
function oneYearAfter(date: string): string {
  const [year, month, day] = date.split("-");
  return `${Number(year) + 1}-${month}-${day}`;
}

/**
 * PHYSICAL-FISCAL-F1 — datos de la autorización de impresión del SRI. Mismas reglas que
 * CreatePhysicalDocumentAuthorizationCommandValidator (el backend es la autoridad).
 */
export const physicalDocumentAuthorizationSchema = z
  .object({
    docTypeCode: z.string().min(1, "Selecciona el tipo de comprobante."),
    authorizationNumber: z
      .string()
      .trim()
      .min(1, "El número de autorización del SRI es obligatorio.")
      .max(49, "El número de autorización no puede superar 49 dígitos.")
      .regex(/^\d+$/, "El número de autorización del SRI debe contener solo dígitos."),
    authorizedOn: businessDate,
    expiresOn: businessDate,
    firstSequential: sequential("primer secuencial"),
    lastSequential: sequential("último secuencial"),
  })
  .superRefine((value, ctx) => {
    if (value.expiresOn < value.authorizedOn)
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["expiresOn"],
        message: "La fecha de caducidad no puede ser anterior a la fecha de autorización.",
      });
    else if (value.expiresOn > oneYearAfter(value.authorizedOn))
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["expiresOn"],
        message:
          "La fecha de caducidad debe estar dentro del año siguiente a la fecha de autorización.",
      });
    if (value.lastSequential < value.firstSequential)
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["lastSequential"],
        message: "El último secuencial no puede ser menor que el primero.",
      });
  });

export type PhysicalDocumentAuthorizationFormValues = z.infer<
  typeof physicalDocumentAuthorizationSchema
>;

export function emptyPhysicalDocumentAuthorizationForm(): PhysicalDocumentAuthorizationFormValues {
  return {
    docTypeCode: "",
    authorizationNumber: "",
    authorizedOn: "",
    expiresOn: "",
    firstSequential: 1,
    lastSequential: 1,
  };
}
