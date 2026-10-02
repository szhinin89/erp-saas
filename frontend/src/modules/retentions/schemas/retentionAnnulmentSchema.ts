import { z } from "zod";

// ZH-RETENTION-SRI-ANNULMENT-01/01B — espejo de SubmitRetentionAnnulmentValidator /
// AbandonRetentionAnnulmentValidator (backend = fuente de verdad). No hay formulario de resolución: el
// estado fiscal lo informa el SRI (ConsultaComprobante), nunca el usuario.

const isoDate = (message: string) =>
  z.string().regex(/^\d{4}-\d{2}-\d{2}$/, message);

export const submitAnnulmentSchema = z.object({
  submittedOn: isoDate("Indique la fecha en que presentó la solicitud en SRI en Línea."),
  reference: z.string().max(200, "Máximo 200 caracteres.").optional(),
  notes: z.string().max(1000, "Máximo 1000 caracteres.").optional(),
});
export type SubmitAnnulmentFormValues = z.infer<typeof submitAnnulmentSchema>;

export const abandonAnnulmentSchema = z.object({
  reason: z
    .string()
    .trim()
    .min(1, "El motivo del desistimiento es obligatorio.")
    .max(1000, "Máximo 1000 caracteres."),
});
export type AbandonAnnulmentFormValues = z.infer<typeof abandonAnnulmentSchema>;
