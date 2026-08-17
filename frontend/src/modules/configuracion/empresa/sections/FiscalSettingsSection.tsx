import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHInfoRow } from "../../../../components/zh/ZHInfoRow";
import { ZHDataValue } from "../../../../components/zh/ZHDataValue";
import {
  ZhTextInput,
  ZhSelect,
  ZhDecimalInput,
} from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import { companyProfileService } from "../api/companyProfileService";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { sriLookupFacade } from "../../../items/facades/sriLookupFacade";
import type { SriTaxRegimeLookup } from "../../../items/facades/sriLookupFacade";
import {
  companyFiscalSchema,
  defaultCompanyFiscalValues,
  type CompanyFiscalValues,
} from "../schemas/companyFiscalSchema";
import {
  consumerFinalMaxAmountSchema,
  type ConsumerFinalMaxAmountValues,
} from "../schemas/consumerFinalPolicySchema";
import type { ConsumerFinalMaxAmountSource } from "../../../../types/companyProfile";

const CONSUMER_FINAL_SOURCE_LABELS: Record<ConsumerFinalMaxAmountSource, string> =
  {
    Manual: "Manual",
    TaxRegimeDefault: "Default por régimen",
    Fallback: "Fallback (régimen no configurado/reconocido)",
  };

export function FiscalSettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("erp.companies.view");
  const canEdit = canShow("erp.companies.update");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [taxRegimes, setTaxRegimes] = useState<SriTaxRegimeLookup[]>([]);

  const profileState = useAsync(() => companyProfileService.getProfile());
  const policyState = useAsync(() => companyProfileService.getFiscalPolicy());

  const [policySaving, setPolicySaving] = useState(false);
  const [policySaveError, setPolicySaveError] = useState<string | null>(null);
  const [policySaved, setPolicySaved] = useState(false);

  useEffect(() => {
    sriLookupFacade
      .taxRegimes()
      .then(setTaxRegimes)
      .catch(() => {});
  }, []);

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setError: setFieldError,
    formState: { isDirty },
  } = useForm<CompanyFiscalValues>({
    resolver: zodResolver(companyFiscalSchema),
    defaultValues: defaultCompanyFiscalValues,
  });

  const toFormValues = (
    profile: NonNullable<typeof profileState.data>,
  ): CompanyFiscalValues => ({
    taxRegimeCode: profile.taxRegimeCode ?? "",
    isAccountingReq: profile.isAccountingReq,
    specialTaxpayerNo: profile.specialTaxpayerNo ?? "",
    isForeignTrade: profile.isForeignTrade,
    withholdsRenta: profile.withholdsRenta,
    withholdsVat: profile.withholdsVat,
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
      await companyProfileService.updateFiscal({
        taxRegimeCode: values.taxRegimeCode || null,
        isAccountingReq: values.isAccountingReq,
        specialTaxpayerNo: values.specialTaxpayerNo || null,
        isForeignTrade: values.isForeignTrade,
        withholdsRenta: values.withholdsRenta,
        withholdsVat: values.withholdsVat,
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

  const {
    register: registerPolicy,
    handleSubmit: handlePolicySubmit,
    reset: resetPolicy,
    setError: setPolicyFieldError,
    formState: { errors: policyErrors, isDirty: policyDirty },
  } = useForm<ConsumerFinalMaxAmountValues>({
    resolver: zodResolver(consumerFinalMaxAmountSchema),
    defaultValues: { consumerFinalMaxAmount: 0 },
  });

  useEffect(() => {
    const policy = policyState.data;
    if (!policy) return;
    resetPolicy({ consumerFinalMaxAmount: policy.consumerFinalMaxAmount });
  }, [policyState.data, resetPolicy]);

  const onPolicySubmit = handlePolicySubmit(async (values) => {
    if (!canEdit) return;
    setPolicySaveError(null);
    setPolicySaved(false);
    setPolicySaving(true);
    try {
      await companyProfileService.updateConsumerFinalMaxAmount({
        consumerFinalMaxAmount: values.consumerFinalMaxAmount,
      });
      setPolicySaved(true);
      policyState.refetch();
    } catch (err) {
      const applied = applyServerErrors(err, setPolicyFieldError, (msg) =>
        setPolicySaveError(msg),
      );
      if (!applied)
        setPolicySaveError(
          formatApiRequestError(err, {
            offline: t("common.apiUnreachable"),
            generic: t("common.errorGeneric"),
          }),
        );
    } finally {
      setPolicySaving(false);
    }
  });

  const handlePolicyDiscard = () => {
    setPolicySaveError(null);
    setPolicySaved(false);
    const policy = policyState.data;
    if (policy) {
      resetPolicy({ consumerFinalMaxAmount: policy.consumerFinalMaxAmount });
    }
  };

  if (!canView) return <NoAccessPage title={t("settings.company.title")} />;
  if (profileState.loading) return <LoadingState />;

  const isForeignTrade = watch("isForeignTrade");

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
                account_balance
              </span>
              <p className="pg-section-label">
                {t("settings.company.fiscal.title")}
              </p>
            </div>
          </div>
          <div className="pg-section-body">
            <ZHGrid cols={2}>
              <ZHField label={t("settings.company.fiscal.taxRegime")}>
                <ZhSelect
                  disabled={saving || !canEdit}
                  {...register("taxRegimeCode")}
                >
                  <option value="">Sin especificar</option>
                  {taxRegimes.map((regime) => (
                    <option key={regime.code} value={regime.code}>
                      {regime.code} — {regime.name}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>

              <ZHField label={t("settings.company.fiscal.specialTaxpayerNo")}>
                <ZhTextInput
                  className="zh-input"
                  placeholder="N° de resolución"
                  disabled={saving || !canEdit}
                  {...register("specialTaxpayerNo")}
                />
              </ZHField>
            </ZHGrid>

            <div className="zh-checkbox-grid">
              <label className="zh-checkbox-label">
                <input
                  type="checkbox"
                  disabled={saving || !canEdit}
                  {...register("isAccountingReq")}
                />
                {t("settings.company.fiscal.isAccountingReq")}
              </label>
              <label className="zh-checkbox-label">
                <input
                  type="checkbox"
                  disabled={saving || !canEdit}
                  {...register("isForeignTrade")}
                />
                {t("settings.company.fiscal.isForeignTrade")}
              </label>
              {isForeignTrade && (
                <>
                  <label className="zh-checkbox-label">
                    <input
                      type="checkbox"
                      disabled={saving || !canEdit}
                      {...register("withholdsRenta")}
                    />
                    {t("settings.company.fiscal.withholdsRenta")}
                  </label>
                  <label className="zh-checkbox-label">
                    <input
                      type="checkbox"
                      disabled={saving || !canEdit}
                      {...register("withholdsVat")}
                    />
                    {t("settings.company.fiscal.withholdsVat")}
                  </label>
                </>
              )}
            </div>
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

      {policyState.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={policyState.error}
        />
      )}
      {policySaveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={policySaveError}
        />
      )}
      {policySaved && (
        <ZHPageNotice variant="success" message={t("settings.company.saved")} />
      )}

      <form onSubmit={onPolicySubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                block
              </span>
              <p className="pg-section-label">Reglas Consumidor Final</p>
            </div>
          </div>
          <div className="pg-section-body">
            {policyState.loading ? (
              <LoadingState />
            ) : (
              <>
                <ZHInfoRow
                  label="Crédito a Consumidor Final"
                  value={
                    <ZHDataValue variant="strong" tone="danger">
                      Bloqueado
                    </ZHDataValue>
                  }
                  wide
                />
                <p className="zh-text-muted zh-text-xs zh-mt-4">
                  Consumidor Final no puede registrar ventas a crédito porque
                  no identifica un deudor para cuentas por cobrar.
                </p>

                <ZHGrid cols={2}>
                  <ZHField
                    label="Monto máximo Consumidor Final"
                    fieldError={
                      policyErrors.consumerFinalMaxAmount?.message
                    }
                    hint="Si una venta a Consumidor Final supera este monto, debe seleccionarse un cliente identificado."
                  >
                    <ZhDecimalInput
                      decimals={2}
                      positiveOnly
                      disabled={policySaving || !canEdit}
                      {...registerPolicy("consumerFinalMaxAmount")}
                    />
                  </ZHField>

                  <ZHField label="Fuente del valor">
                    <ZHDataValue variant="muted">
                      {policyState.data
                        ? CONSUMER_FINAL_SOURCE_LABELS[
                            policyState.data.consumerFinalMaxAmountSource
                          ]
                        : "—"}
                    </ZHDataValue>
                  </ZHField>
                </ZHGrid>
              </>
            )}
          </div>
        </div>

        <div className="pg-actions-bar">
          <div className="pg-actions-buttons">
            <ZHBtn
              variant="ghost"
              size="md"
              type="button"
              disabled={policySaving || !policyDirty}
              onClick={handlePolicyDiscard}
            >
              Descartar Cambios
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              disabled={policySaving || !canEdit || !policyDirty}
            >
              <span className="material-symbols-outlined">save</span>
              {policySaving ? t("common.saving") : "Guardar Configuración"}
            </ZHBtn>
          </div>
        </div>
      </form>
    </>
  );
}
