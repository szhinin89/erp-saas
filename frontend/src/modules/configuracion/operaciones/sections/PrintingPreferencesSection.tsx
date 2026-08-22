import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhNumberInput, ZhSelect } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { usePrintingPreferencesSection } from "./usePrintingPreferencesSection";

export function PrintingPreferencesSection() {
  const { t } = useI18n();
  const page = usePrintingPreferencesSection();
  const { register } = page.form;

  if (!page.canView)
    return <NoAccessPage title={t("settings.operations.printing.title")} />;
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

      <ZHPageNotice
        variant="info"
        message={t("settings.operations.printing.printAgentNote")}
      />

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">print</span>
            <p className="pg-section-label">{t("settings.operations.printing.title")}</p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField
              label={t("settings.operations.printing.modeLabel")}
              hint={t("settings.operations.printing.modeHint")}
              error={page.errors.salesReceiptMode?.message}
            >
              <ZhSelect disabled={page.saving || !page.canEdit} {...register("salesReceiptMode")}>
                <option value="AskBeforePrint">
                  {t("settings.operations.printing.modeAskBeforePrint")}
                </option>
                <option value="AlwaysPrint">
                  {t("settings.operations.printing.modeAlwaysPrint")}
                </option>
                <option value="NeverAutoPrint">
                  {t("settings.operations.printing.modeNeverAutoPrint")}
                </option>
              </ZhSelect>
            </ZHField>

            <ZHField
              label={t("settings.operations.printing.copiesLabel")}
              error={page.errors.salesReceiptCopies?.message}
            >
              <ZhNumberInput
                disabled={page.saving || !page.canEdit}
                min={1}
                max={3}
                {...register("salesReceiptCopies")}
              />
            </ZHField>
          </ZHGrid>
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
