import { apiGet, apiPost, apiPut } from "../../../lib/apiEnvelope";

export type CommunicationEmailSettingsSource = "OrgSettings" | "EnvironmentFallback";

export type CommunicationEmailSettingsDto = {
  enabled: boolean;
  smtpHost: string | null;
  smtpPort: number | null;
  smtpUsername: string | null;
  passwordConfigured: boolean;
  senderEmail: string | null;
  senderName: string | null;
  useSsl: boolean;
  replyToEmail: string | null;
  maxRetries: number;
  defaultLanguage: string;
  source: CommunicationEmailSettingsSource;
};

/** `smtpPassword` en blanco/omitido conserva la contraseña ya guardada — nunca la borra. */
export type UpdateCommunicationEmailSettingsRequest = {
  enabled: boolean;
  smtpHost: string | null;
  smtpPort: number | null;
  smtpUsername: string | null;
  smtpPassword: string | null;
  senderEmail: string | null;
  senderName: string | null;
  useSsl: boolean;
  replyToEmail: string | null;
  maxRetries: number | null;
  defaultLanguage: string | null;
};

export type SendTestEmailResultDto = {
  sent: boolean;
  message: string;
};

export const communicationsEmailSettingsService = {
  getEmailSettings: () =>
    apiGet<CommunicationEmailSettingsDto>("/api/v1/communications/email-settings"),

  updateEmailSettings: (body: UpdateCommunicationEmailSettingsRequest) =>
    apiPut<CommunicationEmailSettingsDto>("/api/v1/communications/email-settings", body),

  sendTestEmail: (toEmail: string) =>
    apiPost<SendTestEmailResultDto>("/api/v1/communications/email-settings/test", {
      toEmail,
    }),
};
