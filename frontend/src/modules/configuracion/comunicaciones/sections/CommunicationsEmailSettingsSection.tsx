import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZhTextInput } from "../../../../components/zh/inputs/ZhTextInput";
import { ZhNumberInput } from "../../../../components/zh/inputs/ZhNumberInput";
import { useI18n } from "../../../../i18n/i18n";
import { useCommunicationsEmailSettingsPage } from "./useCommunicationsEmailSettingsPage";

export function CommunicationsEmailSettingsSection() {
  const { t } = useI18n();
  const page = useCommunicationsEmailSettingsPage();
  const { register, control } = page.form;
  void control;

  if (!page.canView)
    return <NoAccessPage title={t("settings.communications.email.title")} />;
  if (page.settingsState.loading) return <LoadingState />;

  const sourceLabel =
    page.settingsState.data?.source === "OrgSettings"
      ? t("settings.communications.email.sourceOrgSettings")
      : t("settings.communications.email.sourceEnvironmentFallback");

  return (
    <>
      {page.settingsState.error && (
        <ZHPageNotice
          variant="error"
          message={t("settings.communications.email.loadError")}
          detail={page.settingsState.error}
        />
      )}
      {page.saveError && (
        <ZHPageNotice
          variant="error"
          message={t("settings.communications.email.saveErrorTitle")}
          detail={page.saveError}
        />
      )}
      {page.saved && (
        <ZHPageNotice
          variant="success"
          message={t("settings.communications.email.saved")}
        />
      )}

      <form onSubmit={page.onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">mail</span>
              <p className="pg-section-label">
                {t("settings.communications.email.sectionSmtp")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant="info"
              message={t("settings.communications.email.sourceInfo", { source: sourceLabel })}
            />

            <ZHToggle
              label={t("settings.communications.email.enabledLabel")}
              description={t("settings.communications.email.enabledDesc")}
              value={page.enabledValue}
              onChange={(next) => page.setValue("enabled", next, { shouldDirty: true })}
              disabled={page.saving || !page.canEdit}
            />

            <ZHGrid cols={2}>
              <ZHField
                label={t("settings.communications.email.smtpHost")}
                required={page.enabledValue}
                error={page.errors.smtpHost?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  placeholder="smtp.zoho.com"
                  {...register("smtpHost")}
                />
              </ZHField>
              <ZHField
                label={t("settings.communications.email.smtpPort")}
                error={page.errors.smtpPort?.message}
              >
                <ZhNumberInput
                  disabled={page.saving || !page.canEdit}
                  {...register("smtpPort")}
                />
              </ZHField>
              <ZHField
                label={t("settings.communications.email.smtpUsername")}
                required={page.enabledValue}
                error={page.errors.smtpUsername?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  {...register("smtpUsername")}
                />
              </ZHField>
              <ZHField
                label={
                  page.passwordConfigured
                    ? t("settings.communications.email.smtpPasswordNewLabel")
                    : t("settings.communications.email.smtpPassword")
                }
                error={page.errors.smtpPassword?.message}
                hint={
                  page.passwordConfigured
                    ? t("settings.communications.email.passwordConfiguredYes")
                    : t("settings.communications.email.passwordConfiguredNo")
                }
                hintType={page.passwordConfigured ? "success" : "muted"}
              >
                <div className="sri-pass-wrap">
                  <input
                    className="zh-input sri-pass-input"
                    type={page.showPass ? "text" : "password"}
                    placeholder={
                      page.passwordConfigured
                        ? t("settings.communications.email.smtpPasswordPlaceholderKeep")
                        : t("settings.communications.email.smtpPasswordPlaceholderNew")
                    }
                    disabled={page.saving || !page.canEdit}
                    {...register("smtpPassword")}
                  />
                  <button
                    type="button"
                    className="sri-pass-toggle"
                    onClick={() => page.setShowPass((p) => !p)}
                    tabIndex={-1}
                    aria-label={
                      page.showPass
                        ? t("settings.communications.email.hidePassword")
                        : t("settings.communications.email.showPassword")
                    }
                  >
                    <span className="material-symbols-outlined pg-icon-18">
                      {page.showPass ? "visibility_off" : "visibility"}
                    </span>
                  </button>
                </div>
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">badge</span>
              <p className="pg-section-label">
                {t("settings.communications.email.sectionSender")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label={t("settings.communications.email.senderEmail")}
                required={page.enabledValue}
                error={page.errors.senderEmail?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  {...register("senderEmail")}
                />
              </ZHField>
              <ZHField
                label={t("settings.communications.email.senderName")}
                error={page.errors.senderName?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  {...register("senderName")}
                />
              </ZHField>
              <ZHField
                label={t("settings.communications.email.replyToEmail")}
                error={page.errors.replyToEmail?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  {...register("replyToEmail")}
                />
              </ZHField>
              <ZHField
                label={t("settings.communications.email.defaultLanguage")}
                error={page.errors.defaultLanguage?.message}
              >
                <ZhTextInput
                  disabled={page.saving || !page.canEdit}
                  {...register("defaultLanguage")}
                />
              </ZHField>
            </ZHGrid>

            <ZHToggle
              label={t("settings.communications.email.useSslLabel")}
              description={t("settings.communications.email.useSslDesc")}
              value={page.useSslValue}
              onChange={(next) => page.setValue("useSsl", next, { shouldDirty: true })}
              disabled={page.saving || !page.canEdit}
            />

            <ZHField
              label={t("settings.communications.email.maxRetries")}
              error={page.errors.maxRetries?.message}
            >
              <ZhNumberInput
                disabled={page.saving || !page.canEdit}
                {...register("maxRetries")}
              />
            </ZHField>
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            {t("settings.communications.email.footerInfo")}
          </div>
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={page.saving || !page.isDirty}
              onClick={page.handleDiscard}
            >
              {t("common.discard")}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={page.saving || !page.canEdit || !page.isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {page.saving
                ? t("common.saving")
                : t("settings.communications.email.saveButton")}
            </ZHBtn>
          </div>
        </div>
      </form>

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">forward_to_inbox</span>
            <p className="pg-section-label">
              {t("settings.communications.email.sectionTest")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          {page.testResult && (
            <ZHPageNotice variant="success" message={page.testResult} />
          )}
          {page.testError && (
            <ZHPageNotice
              variant="error"
              message={t("settings.communications.email.testErrorTitle")}
              detail={page.testError}
            />
          )}
          <ZHGrid cols={2}>
            <ZHField label={t("settings.communications.email.testRecipient")}>
              <ZhTextInput
                value={page.testEmail}
                onChange={(e) => page.setTestEmail(e.target.value)}
                disabled={page.testSending || !page.canEdit}
                placeholder="tu-correo@empresa.com"
              />
            </ZHField>
          </ZHGrid>
          <ZHBtn
            variant="secondary"
            size="md"
            type="button"
            disabled={page.testSending || !page.canEdit || !page.testEmail}
            onClick={page.handleSendTest}
          >
            <span className="material-symbols-outlined">send</span>
            {page.testSending
              ? t("settings.communications.email.testSending")
              : t("settings.communications.email.testButton")}
          </ZHBtn>
        </div>
      </div>
    </>
  );
}
