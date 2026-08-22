import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHToggle } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { useElectronicDocumentsPreferencesSection } from "./useElectronicDocumentsPreferencesSection";

export function ElectronicDocumentsPreferencesSection() {
  const { t } = useI18n();
  const page = useElectronicDocumentsPreferencesSection();

  if (!page.canView)
    return <NoAccessPage title={t("settings.operations.electronicDocuments.title")} />;
  if (page.settingsState.loading) return <LoadingState />;

  return (
    <form onSubmit={page.onSubmit}>
      {page.settingsState.error && (
        <ZHPageNotice
          variant="error"
          message={t("settings.operations.loadError")}
          detail={page.settingsState.error}
        />
      )}
      {page.saveError && (
        <ZHPageNotice
          variant="error"
          message={t("settings.operations.saveErrorTitle")}
          detail={page.saveError}
        />
      )}
      {page.saved && (
        <ZHPageNotice variant="success" message={t("settings.operations.saved")} />
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">description</span>
            <p className="pg-section-label">
              {t("settings.operations.electronicDocuments.title")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHToggle
            label={t("settings.operations.electronicDocuments.emailOnAuthorizationLabel")}
            description={t("settings.operations.electronicDocuments.emailOnAuthorizationDesc")}
            value={page.emailOnAuthorizationValue}
            onChange={(next) =>
              page.setValue("emailOnAuthorization", next, { shouldDirty: true })
            }
            disabled={page.saving || !page.canEdit}
          />
        </div>
      </div>

      <div className="pg-actions-bar">
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
            {page.saving ? t("common.saving") : t("settings.operations.saveButton")}
          </ZHBtn>
        </div>
      </div>
    </form>
  );
}
