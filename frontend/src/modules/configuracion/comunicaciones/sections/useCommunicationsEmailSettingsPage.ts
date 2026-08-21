import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useAsync } from "../../../../hooks/useAsync";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import {
  communicationsEmailSettingsService,
  type CommunicationEmailSettingsDto,
} from "../api/communicationsEmailSettingsService";
import {
  communicationEmailSettingsSchema,
  type CommunicationEmailSettingsValues,
} from "../schemas/communicationEmailSettingsSchema";

export function useCommunicationsEmailSettingsPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("communications.view");
  const canEdit = canShow("communications.configure");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [showPass, setShowPass] = useState(false);
  const [passwordConfigured, setPasswordConfigured] = useState(false);

  const [testEmail, setTestEmail] = useState("");
  const [testSending, setTestSending] = useState(false);
  const [testResult, setTestResult] = useState<string | null>(null);
  const [testError, setTestError] = useState<string | null>(null);

  const settingsState = useAsync(
    () => communicationsEmailSettingsService.getEmailSettings(),
    canView,
  );

  const form = useForm<CommunicationEmailSettingsValues>({
    resolver: zodResolver(communicationEmailSettingsSchema),
    defaultValues: {
      enabled: false,
      smtpHost: "",
      smtpPort: 587,
      smtpUsername: "",
      smtpPassword: "",
      senderEmail: "",
      senderName: "",
      useSsl: true,
      replyToEmail: "",
      maxRetries: 3,
      defaultLanguage: "es",
    },
  });

  const {
    handleSubmit,
    reset,
    watch,
    setValue,
    formState: { errors, isDirty },
  } = form;
  const enabledValue = watch("enabled");
  const useSslValue = watch("useSsl");

  const resetFromData = (d: CommunicationEmailSettingsDto) => {
    reset({
      enabled: d.enabled,
      smtpHost: d.smtpHost ?? "",
      smtpPort: d.smtpPort ?? 587,
      smtpUsername: d.smtpUsername ?? "",
      smtpPassword: "",
      senderEmail: d.senderEmail ?? "",
      senderName: d.senderName ?? "",
      useSsl: d.useSsl,
      replyToEmail: d.replyToEmail ?? "",
      maxRetries: d.maxRetries,
      defaultLanguage: d.defaultLanguage,
    });
    setPasswordConfigured(d.passwordConfigured);
  };

  useEffect(() => {
    const d = settingsState.data;
    if (!d) return;
    resetFromData(d);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [settingsState.data]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const updated = await communicationsEmailSettingsService.updateEmailSettings({
        enabled: values.enabled,
        smtpHost: values.smtpHost || null,
        smtpPort: values.smtpPort ?? null,
        smtpUsername: values.smtpUsername || null,
        smtpPassword: values.smtpPassword || null,
        senderEmail: values.senderEmail || null,
        senderName: values.senderName || null,
        useSsl: values.useSsl,
        replyToEmail: values.replyToEmail || null,
        maxRetries: values.maxRetries,
        defaultLanguage: values.defaultLanguage,
      });
      setSaved(true);
      resetFromData(updated);
    } catch (err) {
      const applied = applyServerErrors(err, form.setError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(
          formatApiRequestError(err, {
            offline: t("settings.communications.email.offlineError"),
            generic: t("settings.communications.email.genericSaveError"),
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const d = settingsState.data;
    if (d) resetFromData(d);
  };

  const handleSendTest = async () => {
    if (!testEmail) return;
    setTestError(null);
    setTestResult(null);
    setTestSending(true);
    try {
      const result = await communicationsEmailSettingsService.sendTestEmail(testEmail);
      setTestResult(result.message);
    } catch (err) {
      setTestError(
        formatApiRequestError(err, {
          offline: t("settings.communications.email.offlineError"),
          generic: t("settings.communications.email.testGenericError"),
        }),
      );
    } finally {
      setTestSending(false);
    }
  };

  return {
    canView,
    canEdit,
    saving,
    saveError,
    saved,
    showPass,
    setShowPass,
    passwordConfigured,
    settingsState,
    form,
    errors,
    isDirty,
    enabledValue,
    useSslValue,
    setValue,
    onSubmit,
    handleDiscard,
    testEmail,
    setTestEmail,
    testSending,
    testResult,
    testError,
    handleSendTest,
  };
}
