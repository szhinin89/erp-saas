import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZhDecimalInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useSalesPosPreferencesSection } from "./useSalesPosPreferencesSection";

export function SalesPosPreferencesSection() {
  const { t } = useI18n();
  const page = useSalesPosPreferencesSection();
  const { register } = page.form;

  if (!page.canView)
    return <NoAccessPage title={t("settings.operations.salesPos.title")} />;
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
            <p className="pg-section-label">{t("settings.operations.salesPos.title")}</p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHToggle
            label={t("settings.operations.salesPos.allowManualDiscountLabel")}
            description={t("settings.operations.salesPos.allowManualDiscountDesc")}
            value={page.allowManualDiscountValue}
            onChange={(next) =>
              page.setValue("allowManualDiscount", next, { shouldDirty: true })
            }
            disabled={page.saving || !page.canEdit}
          />

          <ZHField
            label={t("settings.operations.salesPos.maxDiscountPercentLabel")}
            hint={t("settings.operations.salesPos.maxDiscountPercentHint")}
            error={page.errors.maxDiscountPercent?.message}
          >
            <ZhDecimalInput
              disabled={page.saving || !page.canEdit}
              decimals={2}
              positiveOnly
              {...register("maxDiscountPercent")}
            />
          </ZHField>

          <ZHToggle
            label={t("settings.operations.salesPos.allowSellWithoutStockLabel")}
            description={t("settings.operations.salesPos.allowSellWithoutStockDesc")}
            value={page.allowSellWithoutStockValue}
            onChange={(next) =>
              page.setValue("allowSellWithoutStock", next, { shouldDirty: true })
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
