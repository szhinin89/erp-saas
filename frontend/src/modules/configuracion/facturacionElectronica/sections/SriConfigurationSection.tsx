import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { useI18n } from "../../../../i18n/i18n";
import { SriConfigPageDataTab } from "./SriConfigPageDataTab";
import { useSriConfigPage } from "./useSriConfigPage";

export function SriConfigurationSection() {
  const { t } = useI18n();
  const page = useSriConfigPage();
  const { register, control, setValue } = page.form;

  if (!page.canView)
    return <NoAccessPage title={t("settings.electronicInvoicing.title")} />;
  if (page.sriState.loading) return <LoadingState />;

  return (
    <>
      {!page.sriState.data && !page.sriState.loading && (
        <ZHPageNotice
          variant="warning"
          message={t("settings.electronicInvoicing.sri.noConfigWarning")}
          detail={t("settings.electronicInvoicing.sri.noConfigDetail")}
        />
      )}
      {page.sriState.error && (
        <ZHPageNotice
          variant="error"
          message={t("settings.electronicInvoicing.sri.loadError")}
          detail={page.sriState.error}
        />
      )}
      {page.saveError && (
        <ZHPageNotice
          variant="error"
          message={t("settings.electronicInvoicing.sri.saveErrorTitle")}
          detail={page.saveError}
        />
      )}
      {page.saved && (
        <ZHPageNotice
          variant="success"
          message={t("settings.electronicInvoicing.sri.saved")}
        />
      )}
      {page.validationError && (
        <ZHPageNotice
          variant="error"
          message={t("settings.electronicInvoicing.sri.validateErrorTitle")}
          detail={page.validationError}
        />
      )}

      {page.validationResult && (
        <div className="pg-section sri-section-mb">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                fact_check
              </span>
              <p className="pg-section-label">
                {t("settings.electronicInvoicing.sri.validationResultsTitle")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHPageNotice
              variant={page.validationResult.isValid ? "success" : "warning"}
              message={
                page.validationResult.isValid
                  ? t("settings.electronicInvoicing.sri.validationAllPassed")
                  : t("settings.electronicInvoicing.sri.validationSomeFailed")
              }
            />
            <ul className="sri-validation-list">
              {page.validationResult.checks.map((check) => (
                <li
                  key={check.code}
                  className={`sri-validation-item ${check.passed ? "sri-validation-item--pass" : "sri-validation-item--fail"}`}
                >
                  <span className="material-symbols-outlined zh-icon-md">
                    {check.passed ? "check_circle" : "cancel"}
                  </span>
                  <span>{check.message}</span>
                </li>
              ))}
            </ul>
            {page.validationResult.certificate?.passwordCorrect && (
              <dl className="sri-cert-info">
                {page.validationResult.certificate.subject && (
                  <>
                    <dt>{t("settings.electronicInvoicing.sri.certSubject")}</dt>
                    <dd className="mono">
                      {page.validationResult.certificate.subject}
                    </dd>
                  </>
                )}
                {page.validationResult.certificate.issuer && (
                  <>
                    <dt>{t("settings.electronicInvoicing.sri.certIssuer")}</dt>
                    <dd className="mono">
                      {page.validationResult.certificate.issuer}
                    </dd>
                  </>
                )}
                {page.validationResult.certificate.daysRemaining !== null && (
                  <>
                    <dt>
                      {t("settings.electronicInvoicing.sri.certDaysRemaining")}
                    </dt>
                    <dd>{page.validationResult.certificate.daysRemaining}</dd>
                  </>
                )}
              </dl>
            )}
          </div>
        </div>
      )}

      <form onSubmit={page.onSubmit}>
        <SriConfigPageDataTab
          register={register}
          control={control}
          errors={page.errors}
          saving={page.saving}
          canEdit={page.canEdit}
          showPass={page.showPass}
          setShowPass={page.setShowPass}
          hasExistingConfig={!!page.sriState.data}
          sriConfig={page.sriState.data ?? null}
          setWsdlUrl={(url) => setValue("wsdlUrl", url, { shouldDirty: true })}
          sriEnvOptions={page.sriEnvOptions}
          certUploading={page.certUploading}
          certUploadProgress={page.certUploadProgress}
          certUploadError={page.certUploadError}
          certInspection={page.certInspection}
          certInspecting={page.certInspecting}
          onCertFileSelected={page.handleCertFileSelected}
        />

        <div className="pg-actions-bar">
          <div className="pg-actions-info">
            <span className="material-symbols-outlined">info</span>
            {t("settings.electronicInvoicing.sri.footerInfo")}
          </div>
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="secondary"
              size="md"
              type="button"
              disabled={page.validating || !page.sriState.data}
              onClick={page.handleValidate}
            >
              <span className="material-symbols-outlined">fact_check</span>
              {page.validating
                ? t("settings.electronicInvoicing.sri.validating")
                : t("settings.electronicInvoicing.sri.validateButton")}
            </ZHBtn>
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
                : t("settings.electronicInvoicing.sri.saveButton")}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
