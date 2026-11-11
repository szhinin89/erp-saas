import { useEffect, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  Badge,
  LoadingState,
  NoAccessPage,
} from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhTextInput, ZhPhoneInput } from "../../../../components/zh/inputs";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { companyProfileService } from "../api/companyProfileService";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import {
  companyConfigSchema,
  defaultCompanyConfigValues,
  type CompanyConfigValues,
} from "../schemas/companyConfigSchema";

const TIMEZONES = [
  { value: "America/Guayaquil", label: "(GMT-05:00) Guayaquil / Ecuador" },
  { value: "America/Bogota", label: "(GMT-05:00) Bogotá / Colombia" },
  { value: "America/Lima", label: "(GMT-05:00) Lima / Perú" },
  { value: "America/New_York", labelKey: "settings.company.profile.easternTime" },
];

const CURRENCIES = [
  { value: "USD", labelKey: "settings.company.profile.currencyUSD" },
  { value: "EUR", labelKey: "settings.company.profile.currencyEUR" },
];

const TAX_STATUS_LABELS: Record<string, string> = {
  Pending: "settings.company.profile.taxStatus.Pending",
  Verified: "settings.company.profile.taxStatus.Verified",
  Invalid: "settings.company.profile.taxStatus.Invalid",
};

export function CompanyProfileSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("erp.companies.view");
  const canEdit = canShow("erp.companies.update");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const profileState = useAsync(() => companyProfileService.getProfile());

  const {
    register,
    handleSubmit,
    reset,
    control,
    setError: setFieldError,
    formState: { errors, isDirty },
  } = useForm<CompanyConfigValues>({
    resolver: zodResolver(companyConfigSchema),
    defaultValues: defaultCompanyConfigValues,
  });

  const toFormValues = (
    profile: NonNullable<typeof profileState.data>,
  ): CompanyConfigValues => ({
    legalName: profile.legalName ?? "",
    tradeName: profile.tradeName ?? "",
    corporateEmail: profile.corporateEmail ?? "",
    phone: profile.phone ?? "",
    website: profile.website ?? "",
    currencyCode: profile.currencyCode ?? "USD",
    timezone: profile.timezone ?? "America/Guayaquil",
    legalRepName: profile.legalRepName ?? "",
    legalRepPosition: profile.legalRepPosition ?? "",
    legalRepIdNumber: profile.legalRepIdNumber ?? "",
    legalRepEmail: profile.legalRepEmail ?? "",
    legalRepPhone: profile.legalRepPhone ?? "",
  });

  useEffect(() => {
    const profile = profileState.data;
    if (!profile) return;
    reset(toFormValues(profile));
  }, [profileState.data, reset]);

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      await companyProfileService.updateProfile({
        legalName: values.legalName,
        tradeName: values.tradeName || null,
        corporateEmail: values.corporateEmail || null,
        phone: values.phone || null,
        website: values.website || null,
        currencyCode: values.currencyCode,
        timezone: values.timezone,
        legalRepName: values.legalRepName || null,
        legalRepPosition: values.legalRepPosition || null,
        legalRepIdNumber: values.legalRepIdNumber || null,
        legalRepEmail: values.legalRepEmail || null,
        legalRepPhone: values.legalRepPhone || null,
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
      reset(toFormValues(profile));
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

      <form onSubmit={onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                badge
              </span>
              <p className="pg-section-label">{t("settings.company.profile.identification")}</p>
            </div>
            {profile && (
              <div className="zh-flex-end zh-gap-8">
                <Badge
                  variant={profile.isActive ? "success" : "neutral"}
                  label={t(profile.isActive ? "settings.company.operation.statusActive" : "settings.company.operation.statusInactive")}
                />
                <Badge
                  variant="neutral"
                  label={
                    <>
                      RUC:{" "}
                      {t(TAX_STATUS_LABELS[profile.taxIdentificationStatus] ?? profile.taxIdentificationStatus)}
                    </>
                  }
                />
              </div>
            )}
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label={t("settings.company.profile.legalName")}
                required
                error={errors.legalName?.message ? t(errors.legalName?.message) : undefined}
              >
                <ZhTextInput
                  className="zh-input"
                  disabled={saving || !canEdit}
                  {...register("legalName")}
                />
              </ZHField>

              <ZHField
                label={t("settings.company.profile.tradeName")}
                error={errors.tradeName?.message ? t(errors.tradeName?.message) : undefined}
              >
                <ZhTextInput
                  className="zh-input"
                  disabled={saving || !canEdit}
                  {...register("tradeName")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.taxId")}>
                <ZhTextInput
                  className="zh-input"
                  value={profile?.taxIdentificationNumber ?? ""}
                  readOnly
                  disabled
                />
              </ZHField>
            </ZHGrid>
            <p className="zh-text-muted zh-text-xs zh-mt-8">
              {t("settings.company.profile.identityHint")}
            </p>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                business_center
              </span>
              <p className="pg-section-label">{t("settings.company.profile.corporate")}</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label={t("settings.company.profile.email")}
                error={errors.corporateEmail?.message ? t(errors.corporateEmail?.message) : undefined}
              >
                <input
                  className="zh-input"
                  type="email"
                  placeholder={t("settings.company.profile.emailPlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("corporateEmail")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.website")} error={errors.website?.message ? t(errors.website?.message) : undefined}>
                <ZhTextInput
                  className="zh-input"
                  placeholder={t("settings.company.profile.websitePlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("website")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.phone")} error={errors.phone?.message ? t(errors.phone?.message) : undefined}>
                <Controller
                  name="phone"
                  control={control}
                  render={({ field }) => (
                    <ZhPhoneInput {...field} disabled={saving || !canEdit} />
                  )}
                />
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                person
              </span>
              <p className="pg-section-label">{t("settings.company.profile.representative")}</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label={t("settings.company.profile.name")} error={errors.legalRepName?.message ? t(errors.legalRepName?.message) : undefined}>
                <ZhTextInput
                  className="zh-input"
                  placeholder={t("settings.company.profile.namePlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("legalRepName")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.position")} error={errors.legalRepPosition?.message ? t(errors.legalRepPosition?.message) : undefined}>
                <ZhTextInput
                  className="zh-input"
                  placeholder={t("settings.company.profile.positionPlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("legalRepPosition")}
                />
              </ZHField>

              <ZHField
                label={t("settings.company.profile.identification")}
                error={errors.legalRepIdNumber?.message ? t(errors.legalRepIdNumber?.message) : undefined}
              >
                <ZhTextInput
                  className="zh-input"
                  placeholder={t("settings.company.profile.idPlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("legalRepIdNumber")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.repEmail")} error={errors.legalRepEmail?.message ? t(errors.legalRepEmail?.message) : undefined}>
                <input
                  className="zh-input"
                  type="email"
                  placeholder={t("settings.company.profile.repEmailPlaceholder")}
                  disabled={saving || !canEdit}
                  {...register("legalRepEmail")}
                />
              </ZHField>

              <ZHField label={t("settings.company.profile.repPhone")} error={errors.legalRepPhone?.message ? t(errors.legalRepPhone?.message) : undefined}>
                <Controller
                  name="legalRepPhone"
                  control={control}
                  render={({ field }) => (
                    <ZhPhoneInput {...field} disabled={saving || !canEdit} />
                  )}
                />
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                public
              </span>
              <p className="pg-section-label">{t("settings.company.profile.region")}</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label={t("settings.company.profile.currency")}>
                <select
                  disabled={saving || !canEdit}
                  {...register("currencyCode")}
                >
                  {CURRENCIES.map((c) => (
                    <option key={c.value} value={c.value}>
                      {t(c.labelKey)}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField label={t("settings.company.profile.timezone")}>
                <select disabled={saving || !canEdit} {...register("timezone")}>
                  {TIMEZONES.map((z) => (
                    <option key={z.value} value={z.value}>
                      {z.labelKey ? t(z.labelKey) : z.label}
                    </option>
                  ))}
                </select>
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        {profile && (
          <p className="zh-text-muted zh-text-xs zh-mt-8">
            {t("settings.company.profile.updatedAt")} {formatDateTime(profile.updatedAt)}
          </p>
        )}

        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={saving || !isDirty}
              onClick={handleDiscard}
            >
              {t("settings.company.actions.discard")}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={saving || !canEdit || !isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {saving ? t("common.saving") : t("settings.company.actions.save")}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
