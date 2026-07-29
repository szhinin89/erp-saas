import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
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
  { value: "America/New_York", label: "(GMT-05:00) Eastern Time" },
];

const CURRENCIES = [
  { value: "USD", label: "USD — Dólar Estadounidense" },
  { value: "EUR", label: "EUR — Euro" },
];

const TAX_STATUS_LABELS: Record<string, string> = {
  Pending: "Pendiente",
  Verified: "Verificado",
  Invalid: "Inválido",
};

export function CompanyProfileSettingsSection() {
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
    taxIdentificationNumber: profile.taxIdentificationNumber ?? "",
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
        taxIdentificationNumber: values.taxIdentificationNumber || null,
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
              <p className="pg-section-label">Identificación</p>
            </div>
            {profile && (
              <div className="zh-flex-end zh-gap-8">
                <span
                  className={`pg-kpi-badge ${profile.isActive ? "pg-kpi-badge--success" : "pg-kpi-badge--neutral"}`}
                >
                  {profile.isActive ? "Activa" : "Inactiva"}
                </span>
                <span className="pg-kpi-badge pg-kpi-badge--neutral">
                  RUC:{" "}
                  {TAX_STATUS_LABELS[profile.taxIdentificationStatus] ??
                    profile.taxIdentificationStatus}
                </span>
              </div>
            )}
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label="Razón Social"
                required
                error={errors.legalName?.message}
              >
                <input
                  className="zh-input"
                  placeholder="Razón social registrada en el SRI"
                  disabled={saving || !canEdit}
                  {...register("legalName")}
                />
              </ZHField>

              <ZHField
                label="Nombre Comercial"
                error={errors.tradeName?.message}
              >
                <input
                  className="zh-input"
                  placeholder="Nombre visible en documentos"
                  disabled={saving || !canEdit}
                  {...register("tradeName")}
                />
              </ZHField>

              <ZHField
                label="RUC"
                error={errors.taxIdentificationNumber?.message}
              >
                <input
                  className="zh-input"
                  placeholder="13 dígitos"
                  disabled={saving || !canEdit}
                  {...register("taxIdentificationNumber")}
                />
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                business_center
              </span>
              <p className="pg-section-label">Información Corporativa</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label="Correo Corporativo"
                error={errors.corporateEmail?.message}
              >
                <input
                  className="zh-input"
                  type="email"
                  placeholder="contacto@empresa.com"
                  disabled={saving || !canEdit}
                  {...register("corporateEmail")}
                />
              </ZHField>

              <ZHField label="Sitio Web" error={errors.website?.message}>
                <input
                  className="zh-input"
                  placeholder="https://www.empresa.com"
                  disabled={saving || !canEdit}
                  {...register("website")}
                />
              </ZHField>

              <ZHField label="Teléfono Principal" error={errors.phone?.message}>
                <input
                  className="zh-input"
                  placeholder="+593 99 999 9999"
                  disabled={saving || !canEdit}
                  {...register("phone")}
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
              <p className="pg-section-label">Representante Legal</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label="Nombre" error={errors.legalRepName?.message}>
                <input
                  className="zh-input"
                  placeholder="Nombre completo"
                  disabled={saving || !canEdit}
                  {...register("legalRepName")}
                />
              </ZHField>

              <ZHField label="Cargo" error={errors.legalRepPosition?.message}>
                <input
                  className="zh-input"
                  placeholder="Cargo dentro de la empresa"
                  disabled={saving || !canEdit}
                  {...register("legalRepPosition")}
                />
              </ZHField>

              <ZHField
                label="Identificación"
                error={errors.legalRepIdNumber?.message}
              >
                <input
                  className="zh-input"
                  placeholder="Cédula o pasaporte"
                  disabled={saving || !canEdit}
                  {...register("legalRepIdNumber")}
                />
              </ZHField>

              <ZHField label="Correo" error={errors.legalRepEmail?.message}>
                <input
                  className="zh-input"
                  type="email"
                  placeholder="representante@empresa.com"
                  disabled={saving || !canEdit}
                  {...register("legalRepEmail")}
                />
              </ZHField>

              <ZHField label="Teléfono" error={errors.legalRepPhone?.message}>
                <input
                  className="zh-input"
                  placeholder="+593 99 999 9999"
                  disabled={saving || !canEdit}
                  {...register("legalRepPhone")}
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
              <p className="pg-section-label">Configuración Regional</p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label="Moneda Base">
                <select
                  disabled={saving || !canEdit}
                  {...register("currencyCode")}
                >
                  {CURRENCIES.map((c) => (
                    <option key={c.value} value={c.value}>
                      {c.label}
                    </option>
                  ))}
                </select>
              </ZHField>

              <ZHField label="Zona Horaria">
                <select disabled={saving || !canEdit} {...register("timezone")}>
                  {TIMEZONES.map((z) => (
                    <option key={z.value} value={z.value}>
                      {z.label}
                    </option>
                  ))}
                </select>
              </ZHField>
            </ZHGrid>
          </div>
        </div>

        {profile && (
          <p className="zh-text-muted zh-text-xs zh-mt-8">
            Última actualización: {formatDateTime(profile.updatedAt)}
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
