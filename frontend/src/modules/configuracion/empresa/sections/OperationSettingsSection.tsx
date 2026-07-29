import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { companyProfileService } from "../api/companyProfileService";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import {
  companyOperationSchema,
  defaultCompanyOperationValues,
  type CompanyOperationValues,
} from "../schemas/companyOperationSchema";

export function OperationSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("configuracion.empresa.view");
  const canEdit = canShow("configuracion.empresa.edit");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const profileState = useAsync(() => companyProfileService.getProfile());

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { isDirty },
  } = useForm<CompanyOperationValues>({
    resolver: zodResolver(companyOperationSchema),
    defaultValues: defaultCompanyOperationValues,
  });

  useEffect(() => {
    const profile = profileState.data;
    if (!profile) return;
    reset({
      languageCode:
        (profile.languageCode as CompanyOperationValues["languageCode"]) ??
        "es",
    });
  }, [profileState.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await companyProfileService.updateOperation({
        languageCode: values.languageCode,
      });
      setSaved(true);
      profileState.refetch();
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, {
            offline: t("common.apiUnreachable"),
            generic: t("common.errorGeneric"),
          }),
        );
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    const profile = profileState.data;
    if (profile) {
      reset({
        languageCode:
          (profile.languageCode as CompanyOperationValues["languageCode"]) ??
          "es",
      });
    }
  };

  if (!canView) return <NoAccessPage title={t("settings.company.title")} />;
  if (profileState.loading) return <LoadingState />;

  const profile = profileState.data;

  return (
    <>
      {profileState.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={profileState.error}
        />
      )}
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={saveError}
        />
      )}
      {saved && (
        <ZHPageNotice variant="success" message={t("settings.company.saved")} />
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              monitoring
            </span>
            <p className="pg-section-label">
              {t("settings.company.operation.title")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label={t("settings.company.operation.companyStatus")}>
              <span
                className={`pg-kpi-badge ${profile?.isActive ? "pg-kpi-badge--success" : "pg-kpi-badge--neutral"}`}
              >
                {profile?.isActive
                  ? t("settings.company.operation.statusActive")
                  : t("settings.company.operation.statusInactive")}
              </span>
            </ZHField>

            <ZHField label={t("settings.company.operation.onboardingStatus")}>
              <span
                className={`pg-kpi-badge ${profile?.onboardingCompleted ? "pg-kpi-badge--success" : "pg-kpi-badge--neutral"}`}
              >
                {profile?.onboardingCompleted
                  ? t("settings.company.operation.onboardingDone")
                  : t("settings.company.operation.onboardingPending")}
              </span>
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      <form onSubmit={onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                translate
              </span>
              <p className="pg-section-label">
                {t("settings.company.operation.languageCode")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label={t("settings.company.operation.languageCode")}>
                <select
                  disabled={saving || !canEdit}
                  {...register("languageCode")}
                >
                  <option value="es">{t("app.langMenu.spanish")}</option>
                  <option value="en">{t("app.langMenu.english")}</option>
                  <option value="qu">{t("app.langMenu.kichwa")}</option>
                </select>
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
              disabled={saving || !isDirty}
              onClick={handleDiscard}
            >
              Descartar Cambios
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={saving || !canEdit || !isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? t("common.saving") : "Guardar Configuración"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
