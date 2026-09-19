import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZhDecimalInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useCashPreferencesSection } from "./useCashPreferencesSection";

export function CashPreferencesSection() {
  const { t } = useI18n();
  const page = useCashPreferencesSection();
  const { register } = page;

  if (!page.canView)
    return <NoAccessPage title={t("settings.operations.cash.title")} />;
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
            <span className="material-symbols-outlined pg-section-icon">point_of_sale</span>
            <p className="pg-section-label">{t("settings.operations.cash.title")}</p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHToggle
            label={t("settings.operations.cash.requireReasonForDifferenceLabel")}
            description={t("settings.operations.cash.requireReasonForDifferenceDesc")}
            value={page.requireReasonForDifferenceValue}
            onChange={(next) =>
              page.setValue("requireReasonForDifference", next, { shouldDirty: true })
            }
            disabled={page.saving || !page.canEdit}
          />

          <ZHToggle
            label={t("settings.operations.cash.allowCloseWithDifferenceLabel")}
            description={t("settings.operations.cash.allowCloseWithDifferenceDesc")}
            value={page.allowCloseWithDifferenceValue}
            onChange={(next) =>
              page.setValue("allowCloseWithDifference", next, { shouldDirty: true })
            }
            disabled={page.saving || !page.canEdit}
          />

          <ZHField
            label={t("settings.operations.cash.maxAllowedDifferenceLabel")}
            hint={t("settings.operations.cash.maxAllowedDifferenceHint")}
            error={page.errors.maxAllowedDifference?.message}
          >
            <ZhDecimalInput
              disabled={page.saving || !page.canEdit || !page.allowCloseWithDifferenceValue}
              decimals={2}
              positiveOnly
              {...register("maxAllowedDifference")}
            />
          </ZHField>

          <ZHToggle
            label={t("settings.operations.cash.allowManualInOutMovementsLabel")}
            description={t("settings.operations.cash.allowManualInOutMovementsDesc")}
            value={page.allowManualInOutMovementsValue}
            onChange={(next) =>
              page.setValue("allowManualInOutMovements", next, { shouldDirty: true })
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
