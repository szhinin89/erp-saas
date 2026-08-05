import { useEffect, useRef, useState } from "react";
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
  companyBrandingSchema,
  defaultCompanyBrandingValues,
  type CompanyBrandingValues,
} from "../schemas/companyBrandingSchema";

const ALLOWED_LOGO_TYPES = ["image/png", "image/jpeg", "image/webp"];
const MAX_LOGO_SIZE_BYTES = 5 * 1024 * 1024;

function parseBrandingConfiguration(raw: string | null): CompanyBrandingValues {
  if (!raw) return { ...defaultCompanyBrandingValues };
  try {
    const parsed = JSON.parse(raw) as Partial<CompanyBrandingValues>;
    return {
      primaryColor: parsed.primaryColor ?? "",
      secondaryColor: parsed.secondaryColor ?? "",
      slogan: parsed.slogan ?? "",
    };
  } catch {
    return { ...defaultCompanyBrandingValues };
  }
}

export function BrandingSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("configuracion.empresa.view");
  const canEdit = canShow("configuracion.empresa.edit");

  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoPreviewUrl, setLogoPreviewUrl] = useState<string | null>(null);
  const [currentLogoUrl, setCurrentLogoUrl] = useState<string | null>(null);
  const [logoUploading, setLogoUploading] = useState(false);
  const [logoUploadProgress, setLogoUploadProgress] = useState(0);
  const [logoError, setLogoError] = useState<string | null>(null);
  const [logoSaved, setLogoSaved] = useState(false);
  const logoFileInputRef = useRef<HTMLInputElement>(null);

  const [altLogoFile, setAltLogoFile] = useState<File | null>(null);
  const [altLogoPreviewUrl, setAltLogoPreviewUrl] = useState<string | null>(
    null,
  );
  const [currentAltLogoUrl, setCurrentAltLogoUrl] = useState<string | null>(
    null,
  );
  const [altLogoUploading, setAltLogoUploading] = useState(false);
  const [altLogoUploadProgress, setAltLogoUploadProgress] = useState(0);
  const [altLogoError, setAltLogoError] = useState<string | null>(null);
  const [altLogoSaved, setAltLogoSaved] = useState(false);
  const altLogoFileInputRef = useRef<HTMLInputElement>(null);

  const [identitySaving, setIdentitySaving] = useState(false);
  const [identityError, setIdentityError] = useState<string | null>(null);
  const [identitySaved, setIdentitySaved] = useState(false);

  const profileState = useAsync(() => companyProfileService.getProfile());

  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors, isDirty },
  } = useForm<CompanyBrandingValues>({
    resolver: zodResolver(companyBrandingSchema),
    defaultValues: defaultCompanyBrandingValues,
  });

  useEffect(() => {
    const logo = profileState.data?.logo;
    if (!logo) {
      setCurrentLogoUrl(null);
      return;
    }

    let objectUrl: string | null = null;
    let cancelled = false;

    companyProfileService
      .getLogoBlob()
      .then((blob) => {
        if (cancelled || !blob) return;
        objectUrl = URL.createObjectURL(blob);
        setCurrentLogoUrl(objectUrl);
      })
      .catch(() => {
        if (!cancelled) setCurrentLogoUrl(null);
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [profileState.data?.logo?.id, profileState.data?.logo?.lastUpdatedAt]);

  useEffect(() => {
    const alternateLogo = profileState.data?.alternateLogo;
    if (!alternateLogo) {
      setCurrentAltLogoUrl(null);
      return;
    }

    let objectUrl: string | null = null;
    let cancelled = false;

    companyProfileService
      .getLogoAltBlob()
      .then((blob) => {
        if (cancelled || !blob) return;
        objectUrl = URL.createObjectURL(blob);
        setCurrentAltLogoUrl(objectUrl);
      })
      .catch(() => {
        if (!cancelled) setCurrentAltLogoUrl(null);
      });

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [
    profileState.data?.alternateLogo?.id,
    profileState.data?.alternateLogo?.lastUpdatedAt,
  ]);

  useEffect(() => {
    return () => {
      if (logoPreviewUrl) URL.revokeObjectURL(logoPreviewUrl);
    };
  }, [logoPreviewUrl]);

  useEffect(() => {
    return () => {
      if (altLogoPreviewUrl) URL.revokeObjectURL(altLogoPreviewUrl);
    };
  }, [altLogoPreviewUrl]);

  useEffect(() => {
    const profile = profileState.data;
    if (!profile) return;
    reset(parseBrandingConfiguration(profile.brandingConfiguration));
  }, [profileState.data?.brandingConfiguration, reset]);

  const handleLogoFileChange = (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0] ?? null;
    setLogoError(null);
    setLogoSaved(false);

    if (logoPreviewUrl) URL.revokeObjectURL(logoPreviewUrl);

    if (!file) {
      setLogoFile(null);
      setLogoPreviewUrl(null);
      return;
    }

    if (!ALLOWED_LOGO_TYPES.includes(file.type)) {
      setLogoError("Formato no soportado. Use PNG, JPG o WEBP.");
      setLogoFile(null);
      setLogoPreviewUrl(null);
      if (logoFileInputRef.current) logoFileInputRef.current.value = "";
      return;
    }

    if (file.size > MAX_LOGO_SIZE_BYTES) {
      setLogoError("El archivo supera el tamaño máximo de 5 MB.");
      setLogoFile(null);
      setLogoPreviewUrl(null);
      if (logoFileInputRef.current) logoFileInputRef.current.value = "";
      return;
    }

    setLogoFile(file);
    setLogoPreviewUrl(URL.createObjectURL(file));
  };

  const handleLogoUpload = async () => {
    if (!logoFile) return;
    setLogoUploading(true);
    setLogoUploadProgress(0);
    setLogoError(null);
    setLogoSaved(false);
    try {
      await companyProfileService.uploadLogo(logoFile, setLogoUploadProgress);
      setLogoSaved(true);
      if (logoPreviewUrl) URL.revokeObjectURL(logoPreviewUrl);
      setLogoFile(null);
      setLogoPreviewUrl(null);
      if (logoFileInputRef.current) logoFileInputRef.current.value = "";
      profileState.refetch();
    } catch (err) {
      setLogoError(
        formatApiRequestError(err, {
          offline: t("common.apiUnreachable"),
          generic: t("common.errorGeneric"),
        }),
      );
    } finally {
      setLogoUploading(false);
    }
  };

  const handleAltLogoFileChange = (
    event: React.ChangeEvent<HTMLInputElement>,
  ) => {
    const file = event.target.files?.[0] ?? null;
    setAltLogoError(null);
    setAltLogoSaved(false);

    if (altLogoPreviewUrl) URL.revokeObjectURL(altLogoPreviewUrl);

    if (!file) {
      setAltLogoFile(null);
      setAltLogoPreviewUrl(null);
      return;
    }

    if (!ALLOWED_LOGO_TYPES.includes(file.type)) {
      setAltLogoError("Formato no soportado. Use PNG, JPG o WEBP.");
      setAltLogoFile(null);
      setAltLogoPreviewUrl(null);
      if (altLogoFileInputRef.current) altLogoFileInputRef.current.value = "";
      return;
    }

    if (file.size > MAX_LOGO_SIZE_BYTES) {
      setAltLogoError("El archivo supera el tamaño máximo de 5 MB.");
      setAltLogoFile(null);
      setAltLogoPreviewUrl(null);
      if (altLogoFileInputRef.current) altLogoFileInputRef.current.value = "";
      return;
    }

    setAltLogoFile(file);
    setAltLogoPreviewUrl(URL.createObjectURL(file));
  };

  const handleAltLogoUpload = async () => {
    if (!altLogoFile) return;
    setAltLogoUploading(true);
    setAltLogoUploadProgress(0);
    setAltLogoError(null);
    setAltLogoSaved(false);
    try {
      await companyProfileService.uploadLogoAlt(
        altLogoFile,
        setAltLogoUploadProgress,
      );
      setAltLogoSaved(true);
      if (altLogoPreviewUrl) URL.revokeObjectURL(altLogoPreviewUrl);
      setAltLogoFile(null);
      setAltLogoPreviewUrl(null);
      if (altLogoFileInputRef.current) altLogoFileInputRef.current.value = "";
      profileState.refetch();
    } catch (err) {
      setAltLogoError(
        formatApiRequestError(err, {
          offline: t("common.apiUnreachable"),
          generic: t("common.errorGeneric"),
        }),
      );
    } finally {
      setAltLogoUploading(false);
    }
  };

  const onIdentitySubmit = handleSubmit(async (values) => {
    if (!canEdit) return;
    setIdentityError(null);
    setIdentitySaved(false);
    setIdentitySaving(true);
    try {
      await companyProfileService.updateBranding({
        primaryColor: values.primaryColor || undefined,
        secondaryColor: values.secondaryColor || undefined,
        slogan: values.slogan || undefined,
      });
      setIdentitySaved(true);
      profileState.refetch();
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setIdentityError(msg),
      );
      if (!applied)
        setIdentityError(
          formatApiRequestError(err, {
            offline: t("common.apiUnreachable"),
            generic: t("common.errorGeneric"),
          }),
        );
    } finally {
      setIdentitySaving(false);
    }
  });

  const handleIdentityDiscard = () => {
    setIdentityError(null);
    setIdentitySaved(false);
    const profile = profileState.data;
    if (profile) {
      reset(parseBrandingConfiguration(profile.brandingConfiguration));
    }
  };

  if (!canView) return <NoAccessPage title={t("settings.company.title")} />;
  if (profileState.loading) return <LoadingState />;

  return (
    <>
      {profileState.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={profileState.error}
        />
      )}
      {logoError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={logoError}
        />
      )}
      {logoSaved && (
        <ZHPageNotice
          variant="success"
          message={t("settings.company.logo.uploaded")}
        />
      )}
      {altLogoError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={altLogoError}
        />
      )}
      {altLogoSaved && (
        <ZHPageNotice
          variant="success"
          message={t("settings.company.branding.alternateLogo.uploaded")}
        />
      )}
      {identityError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={identityError}
        />
      )}
      {identitySaved && (
        <ZHPageNotice variant="success" message={t("settings.company.saved")} />
      )}

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              photo_camera
            </span>
            <p className="pg-section-label">
              {t("settings.company.logo.title")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label={t("settings.company.logo.preview")}>
              {(logoPreviewUrl ?? currentLogoUrl) ? (
                <img
                  src={logoPreviewUrl ?? currentLogoUrl ?? undefined}
                  alt={t("settings.company.logo.title")}
                  className="branding-logo-preview"
                />
              ) : (
                <p className="zh-text-muted zh-text-xs">
                  {t("settings.company.logo.noLogo")}
                </p>
              )}
            </ZHField>

            <ZHField
              label={t("settings.company.logo.selectFile")}
              hint={`${t("settings.company.logo.formats")} · ${t("settings.company.logo.maxSize")}`}
            >
              <input
                ref={logoFileInputRef}
                type="file"
                className="zh-input"
                accept="image/png,image/jpeg,image/webp"
                disabled={logoUploading || !canEdit}
                onChange={handleLogoFileChange}
              />
              {logoFile && (
                <p className="zh-text-muted zh-text-xs zh-mt-4">
                  {logoFile.name}
                </p>
              )}
              {logoUploading && (
                <progress
                  className="branding-upload-progress"
                  value={logoUploadProgress}
                  max={100}
                  aria-label={t("settings.company.logo.preview")}
                />
              )}
              <div className="zh-mt-8">
                <ZHBtn
                  variant="secondary"
                  size="md"
                  type="button"
                  disabled={!logoFile || logoUploading || !canEdit}
                  onClick={handleLogoUpload}
                >
                  <span className="material-symbols-outlined">upload</span>
                  {logoUploading
                    ? t("settings.company.logo.uploading")
                    : t("settings.company.logo.upload")}
                </ZHBtn>
              </div>
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      <div className="pg-section">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              image
            </span>
            <p className="pg-section-label">
              {t("settings.company.branding.alternateLogo.title")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField label={t("settings.company.logo.preview")}>
              {(altLogoPreviewUrl ?? currentAltLogoUrl) ? (
                <img
                  src={altLogoPreviewUrl ?? currentAltLogoUrl ?? undefined}
                  alt={t("settings.company.branding.alternateLogo.title")}
                  className="branding-logo-preview"
                />
              ) : (
                <p className="zh-text-muted zh-text-xs">
                  {t("settings.company.branding.alternateLogo.noLogo")}
                </p>
              )}
            </ZHField>

            <ZHField
              label={t("settings.company.logo.selectFile")}
              hint={`${t("settings.company.logo.formats")} · ${t("settings.company.logo.maxSize")}`}
            >
              <input
                ref={altLogoFileInputRef}
                type="file"
                className="zh-input"
                accept="image/png,image/jpeg,image/webp"
                disabled={altLogoUploading || !canEdit}
                onChange={handleAltLogoFileChange}
              />
              {altLogoFile && (
                <p className="zh-text-muted zh-text-xs zh-mt-4">
                  {altLogoFile.name}
                </p>
              )}
              {altLogoUploading && (
                <progress
                  className="branding-upload-progress"
                  value={altLogoUploadProgress}
                  max={100}
                  aria-label={t("settings.company.branding.alternateLogo.title")}
                />
              )}
              <div className="zh-mt-8">
                <ZHBtn
                  variant="secondary"
                  size="md"
                  type="button"
                  disabled={!altLogoFile || altLogoUploading || !canEdit}
                  onClick={handleAltLogoUpload}
                >
                  <span className="material-symbols-outlined">upload</span>
                  {altLogoUploading
                    ? t("settings.company.logo.uploading")
                    : t("settings.company.logo.upload")}
                </ZHBtn>
              </div>
            </ZHField>
          </ZHGrid>
        </div>
      </div>

      <form onSubmit={onIdentitySubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                palette
              </span>
              <p className="pg-section-label">
                {t("settings.company.branding.identity.title")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField
                label={t("settings.company.branding.identity.primaryColor")}
                error={errors.primaryColor?.message}
              >
                <div
                  className="zh-flex-end zh-gap-8 branding-color-row"
                >
                  <input
                    type="color"
                    className="zh-color-swatch"
                    disabled={identitySaving || !canEdit}
                    {...register("primaryColor")}
                  />
                  <input
                    className="zh-input"
                    placeholder="Ej. color principal HEX"
                    disabled={identitySaving || !canEdit}
                    {...register("primaryColor")}
                  />
                </div>
              </ZHField>

              <ZHField
                label={t("settings.company.branding.identity.secondaryColor")}
                error={errors.secondaryColor?.message}
              >
                <div
                  className="zh-flex-end zh-gap-8 branding-color-row"
                >
                  <input
                    type="color"
                    className="zh-color-swatch"
                    disabled={identitySaving || !canEdit}
                    {...register("secondaryColor")}
                  />
                  <input
                    className="zh-input"
                    placeholder="Ej. color secundario HEX"
                    disabled={identitySaving || !canEdit}
                    {...register("secondaryColor")}
                  />
                </div>
              </ZHField>

              <ZHField
                label={t("settings.company.branding.identity.slogan")}
                error={errors.slogan?.message}
              >
                <input
                  className="zh-input"
                  placeholder={t("settings.company.branding.identity.slogan")}
                  disabled={identitySaving || !canEdit}
                  {...register("slogan")}
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
              disabled={identitySaving || !isDirty}
              onClick={handleIdentityDiscard}
            >
              Descartar Cambios
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={identitySaving || !canEdit || !isDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {identitySaving ? t("common.saving") : "Guardar Configuración"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
