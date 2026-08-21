import { z } from "zod";

export const communicationEmailSettingsSchema = z
  .object({
    enabled: z.boolean(),
    smtpHost: z.string().max(255).optional(),
    smtpPort: z.coerce.number().int().min(1).max(65535).optional(),
    smtpUsername: z.string().max(255).optional(),
    smtpPassword: z.string().max(1000).optional(),
    senderEmail: z.string().email("Correo remitente inválido").max(254).optional(),
    senderName: z.string().max(200).optional(),
    useSsl: z.boolean(),
    replyToEmail: z
      .string()
      .email("Correo de respuesta inválido")
      .max(254)
      .optional()
      .or(z.literal("")),
    maxRetries: z.coerce.number().int().min(0).max(20),
    defaultLanguage: z.string().min(2).max(10),
  })
  .superRefine((values, ctx) => {
    if (!values.enabled) return;
    if (!values.smtpHost) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["smtpHost"],
        message: "El host SMTP es obligatorio cuando el envío está activo.",
      });
    }
    if (!values.smtpUsername) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["smtpUsername"],
        message: "El usuario SMTP es obligatorio cuando el envío está activo.",
      });
    }
    if (!values.senderEmail) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ["senderEmail"],
        message: "El correo remitente es obligatorio cuando el envío está activo.",
      });
    }
  });

export type CommunicationEmailSettingsValues = z.infer<
  typeof communicationEmailSettingsSchema
>;
