import {
  Controller,
  type Control,
  type FieldErrors,
  type UseFormRegister,
} from "react-hook-form";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhFileUpload } from "../../../../components/zh/ZhFileUpload";
import { useI18n } from "../../../../i18n/i18n";
import { formatDateTime } from "../../../../lib/formatters/dateFormatters";
import {
  SRI_WSDL_DEFAULTS,
  type SriConfigValues,
} from "../schemas/sriConfigurationSchema";
import type {
  SriCertificateInfoDto,
  SriConfigurationDto,
} from "../api/electronicInvoicingService";
import "./sri-config-page.css";

type SriEnvOption = { value: 1 | 2; label: string; description: string };

type SriConfigPageDataTabProps = {
  register: UseFormRegister<SriConfigValues>;
  control: Control<SriConfigValues>;
  errors: FieldErrors<SriConfigValues>;
  saving: boolean;
  canEdit: boolean;
  showPass: boolean;
  setShowPass: (value: boolean | ((prev: boolean) => boolean)) => void;
  hasExistingConfig: boolean;
  sriConfig: SriConfigurationDto | null;
  setWsdlUrl: (url: string) => void;
  sriEnvOptions: SriEnvOption[];
  certUploading: boolean;
  certUploadProgress: number;
  certUploadError: string | null;
  certInspection: SriCertificateInfoDto | null;
  certInspecting: boolean;
  onCertFileSelected: (file: File) => void;
};

// NOTA (Bloque 14B-3): ninguno de los controles de este archivo (radio de
// ambiente SRI, display readonly de tipo de emisión, contraseña de
// certificado con toggle, WSDL) fue migrado a componentes ZH — configuración
// SRI crítica, excluida explícitamente del alcance de ese bloque.
export function SriConfigPageDataTab({
  register,
  control,
  errors,
  saving,
  canEdit,
  showPass,
  setShowPass,
  hasExistingConfig,
  sriConfig,
  setWsdlUrl,
  sriEnvOptions,
  certUploading,
  certUploadProgress,
  certUploadError,
  certInspection,
  certInspecting,
  onCertFileSelected,
}: SriConfigPageDataTabProps) {
  const { t } = useI18n();

  return (
    <>
      <div className="pg-section sri-section-mb">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              cloud
            </span>
            <p className="pg-section-label">
              {t("settings.electronicInvoicing.sri.sectionEnvironment")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHPageNotice
            variant="warning"
            message={t("settings.electronicInvoicing.sri.envWarning")}
          />
          <div className="sri-env-stack">
            <Controller
              name="environment"
              control={control}
              render={({ field }) => (
                <>
                  {sriEnvOptions.map((opt) => (
                    <label
                      key={opt.value}
                      className={`sri-env-option ${field.value === opt.value ? "sri-env-option--selected" : ""}`}
                    >
                      <input
                        type="radio"
                        value={opt.value}
                        checked={field.value === opt.value}
                        onChange={() => field.onChange(opt.value)}
                        disabled={saving || !canEdit}
                      />
                      <div>
                        <div className="sri-env-option-title">{opt.label}</div>
                        <div className="sri-env-option-desc">
                          {opt.description}
                        </div>
                      </div>
                    </label>
                  ))}
                </>
              )}
            />
            {errors.environment && (
              <p className="sri-field-error">{errors.environment.message}</p>
            )}
          </div>
          <div className="sri-block-mt">
            <ZHField label={t("settings.electronicInvoicing.sri.emissionType")}>
              <input
                className="zh-input sri-input-readonly-muted"
                value={t("settings.electronicInvoicing.sri.emissionTypeValue")}
                readOnly
                disabled
              />
            </ZHField>
          </div>
        </div>
      </div>

      <div className="pg-section sri-section-mb">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              verified_user
            </span>
            <p className="pg-section-label">
              {t("settings.electronicInvoicing.sri.sectionCertificate")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHPageNotice
            variant="info"
            message={t("settings.electronicInvoicing.sri.certInfo")}
          />
          <p className="sri-hint-sm">
            {t("settings.electronicInvoicing.sri.certPasswordHint")}
          </p>

          {!hasExistingConfig ? (
            <div className="sri-block-mt">
              <ZHPageNotice
                variant="warning"
                message={t(
                  "settings.electronicInvoicing.sri.certRequiresConfigSaved",
                )}
              />
            </div>
          ) : (
            <div className="sri-block-mt">
              <ZhFileUpload
                accept=".p12,.pfx"
                disabled={saving || !canEdit}
                uploading={certUploading}
                progress={certUploadProgress}
                error={certUploadError}
                onFileSelected={onCertFileSelected}
                currentFile={
                  sriConfig?.certFileName
                    ? {
                        name: sriConfig.certFileName,
                        sizeBytes: sriConfig.certSizeBytes ?? 0,
                        uploadedAt: sriConfig.certUploadedAtUtc
                          ? formatDateTime(sriConfig.certUploadedAtUtc)
                          : "",
                      }
                    : null
                }
                selectLabel={t(
                  "settings.electronicInvoicing.sri.certSelectFile",
                )}
                dropLabel={t("settings.electronicInvoicing.sri.certDropHint")}
                uploadingLabel={t(
                  "settings.electronicInvoicing.sri.certUploading",
                )}
                noFileLabel={t("settings.electronicInvoicing.sri.certNoFile")}
              />
            </div>
          )}

          <div className="sri-block-mt">
            <ZHField
              label={
                hasExistingConfig
                  ? t("settings.electronicInvoicing.sri.certPasswordNewLabel")
                  : t("settings.electronicInvoicing.sri.certPassword")
              }
              error={errors.certPassword?.message}
            >
              <div className="sri-pass-wrap">
                <input
                  className="zh-input sri-pass-input"
                  type={showPass ? "text" : "password"}
                  placeholder={
                    hasExistingConfig
                      ? t(
                          "settings.electronicInvoicing.sri.certPasswordPlaceholderKeep",
                        )
                      : t(
                          "settings.electronicInvoicing.sri.certPasswordPlaceholderNew",
                        )
                  }
                  disabled={saving || !canEdit}
                  {...register("certPassword")}
                />
                <button
                  type="button"
                  className="sri-pass-toggle"
                  onClick={() => setShowPass((p) => !p)}
                  tabIndex={-1}
                  aria-label={
                    showPass
                      ? t("settings.electronicInvoicing.sri.hidePassword")
                      : t("settings.electronicInvoicing.sri.showPassword")
                  }
                >
                  <span className="material-symbols-outlined pg-icon-18">
                    {showPass ? "visibility_off" : "visibility"}
                  </span>
                </button>
              </div>
            </ZHField>
          </div>

          {certInspecting && (
            <p className="sri-hint-sm">
              {t("settings.electronicInvoicing.sri.certInspecting")}
            </p>
          )}

          {!certInspecting && certInspection && (
            <div className="sri-block-mt">
              <ul className="sri-validation-list">
                <li
                  className={`sri-validation-item ${certInspection.passwordCorrect ? "sri-validation-item--pass" : "sri-validation-item--fail"}`}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    {certInspection.passwordCorrect ? "check_circle" : "cancel"}
                  </span>
                  <span>
                    {certInspection.passwordCorrect
                      ? t("settings.electronicInvoicing.sri.certPasswordOk")
                      : (certInspection.errorMessage ??
                        t(
                          "settings.electronicInvoicing.sri.certPasswordWrong",
                        ))}
                  </span>
                </li>
              </ul>
              {certInspection.passwordCorrect && (
                <dl className="sri-cert-info">
                  {certInspection.subject && (
                    <>
                      <dt>
                        {t("settings.electronicInvoicing.sri.certSubject")}
                      </dt>
                      <dd className="mono">{certInspection.subject}</dd>
                    </>
                  )}
                  {certInspection.issuer && (
                    <>
                      <dt>
                        {t("settings.electronicInvoicing.sri.certIssuer")}
                      </dt>
                      <dd className="mono">{certInspection.issuer}</dd>
                    </>
                  )}
                  {certInspection.notAfterUtc && (
                    <>
                      <dt>
                        {t("settings.electronicInvoicing.sri.certExpiresAt")}
                      </dt>
                      <dd>
                        {formatDateTime(certInspection.notAfterUtc)}
                        {certInspection.daysRemaining !== null
                          ? ` (${certInspection.daysRemaining} ${t("settings.electronicInvoicing.sri.certDaysRemaining")})`
                          : ""}
                      </dd>
                    </>
                  )}
                </dl>
              )}
            </div>
          )}
        </div>
      </div>

      <div className="pg-section sri-section-mb">
        <div className="pg-section-header">
          <div className="pg-section-header-left">
            <span className="material-symbols-outlined pg-section-icon">
              api
            </span>
            <p className="pg-section-label">
              {t("settings.electronicInvoicing.sri.sectionWsdl")}
            </p>
          </div>
        </div>
        <div className="pg-section-body">
          <ZHGrid cols={1}>
            <ZHField
              label={t("settings.electronicInvoicing.sri.wsdlUrl")}
              required
              error={errors.wsdlUrl?.message}
            >
              <input
                className="zh-input mono"
                disabled={saving || !canEdit}
                {...register("wsdlUrl")}
              />
            </ZHField>
          </ZHGrid>
          <div className="sri-wsdl-actions">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              disabled={saving || !canEdit}
              onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.pruebas)}
            >
              {t("settings.electronicInvoicing.sri.useTestUrl")}
            </ZHBtn>
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              disabled={saving || !canEdit}
              onClick={() => setWsdlUrl(SRI_WSDL_DEFAULTS.produccion)}
            >
              {t("settings.electronicInvoicing.sri.useProdUrl")}
            </ZHBtn>
          </div>
          <div className="sri-wsdl-hint">
            <strong>
              {t("settings.electronicInvoicing.sri.environmentTest")}:
            </strong>{" "}
            <span className="mono">{SRI_WSDL_DEFAULTS.pruebas}</span>
            <br />
            <strong>
              {t("settings.electronicInvoicing.sri.environmentProd")}:
            </strong>{" "}
            <span className="mono">{SRI_WSDL_DEFAULTS.produccion}</span>
          </div>
        </div>
      </div>
    </>
  );
}
