import { useEffect } from "react";
import { useI18n } from "../../../i18n/i18n";
import { useFormContext, Controller } from "react-hook-form";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { useSriIdTypes, useSriIdTypesByUsage } from "../api/useSriIdTypes";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import {
  isLegalEntityTypeInferable,
  type BusinessPartnerFormValues,
} from "../../../schemas/masterData/businessPartnerSchema";
import { useLegalEntityTypes } from "../api/useLegalEntityTypes";

type Props = {
  saving: boolean;
  modalLayout?: boolean;
  section?: "identity" | "review";
  usage?: "customer" | "supplier";
  /** Edición de un socio existente (ficha): identificación de solo lectura (el PUT no la modifica) y sin la configuración de alta del proveedor. */
  identityLocked?: boolean;
};

export function MasterDataBpFormFields({
  saving,
  modalLayout = false,
  section = "identity",
  usage,
  identityLocked = false,
}: Props) {
  const { t } = useI18n();
  const {
    register,
    control,
    watch,
    setValue,
    formState: { errors },
  } = useFormContext<BusinessPartnerFormValues>();
  const allTypes = useSriIdTypes();
  const filteredTypes = useSriIdTypesByUsage(usage ?? "");
  const { options: idTypes, loading: loadingTypes } = usage
    ? filteredTypes
    : allTypes;
  const { options: legalEntityTypes, loading: loadingLegalEntityTypes } =
    useLegalEntityTypes();
  const { options: supplierTypeOptions, loading: loadingSupplierTypes } =
    useSriSupplierTypes();
  const watchedIdentificationType = watch("identificationType");

  useEffect(() => {
    // El backend infiere legalEntityTypeCode para RUC/CI — cualquier valor manual anterior
    // quedaría stale y produciría un 422 por contradicción con la nueva identificación.
    if (isLegalEntityTypeInferable(watchedIdentificationType)) {
      setValue("legalEntityTypeCode", undefined, { shouldValidate: false });
    }
  }, [watchedIdentificationType, setValue]);

  if (section === "review") {
    const values = watch();
    const legalEntityLabel = values.legalEntityTypeCode
      ? (legalEntityTypes.find((x) => x.code === values.legalEntityTypeCode)
          ?.name ?? String(values.legalEntityTypeCode))
      : isLegalEntityTypeInferable(values.identificationType)
        ? "Se determina automáticamente"
        : "—";
    const supplierTypeLabel = supplierTypeOptions.find(
      (o) => o.code === values.refundProviderTypeCode,
    )?.name;
    return (
      <dl className="prd-review-grid">
        <dt>Tipo de identificación</dt>
        <dd className="mono">{values.identificationType}</dd>
        <dt>Número</dt>
        <dd className="mono">{values.identificationNumber}</dd>
        <dt>Tipo de entidad legal</dt>
        <dd>{legalEntityLabel}</dd>
        <dt>Razón social</dt>
        <dd>{values.tradeName?.trim() || values.legalName}</dd>
        {values.tradeName?.trim() && (
          <>
            <dt>Nombre legal</dt>
            <dd>{values.legalName}</dd>
          </>
        )}
        <dt>País</dt>
        <dd className="mono">{values.countryCode || "EC"}</dd>
        {usage === "supplier" && (
          <>
            <dt>Tipo de Proveedor</dt>
            <dd>{supplierTypeLabel ?? "—"}</dd>
          </>
        )}
      </dl>
    );
  }

  return (
    <>
      <div className={modalLayout ? "zh-grid zh-grid--1" : "md-partner-group"}>
        <h4 className={modalLayout ? "zh-section-title" : "md-partner-group__title"}>{t("masterdata.bp.detail.id.identification", "Datos de identificación")}</h4>
        <ZHGrid cols={modalLayout ? 2 : 3}>
          <ZHField
            label={t("masterdata.bp.detail.id.idType", "Tipo de identificación")}
            required
            fieldError={errors.identificationType?.message}
          >
            <ZhSelect
              {...register("identificationType")}
              disabled={saving || loadingTypes || identityLocked}
            >
              {loadingTypes ? (
                <option value="">{t("masterdata.bp.detail.loadingShort", "Cargando…")}</option>
              ) : (
                idTypes.map((t) => (
                  <option key={t.code} value={t.code}>
                    {t.code} — {t.name}
                  </option>
                ))
              )}
            </ZhSelect>
          </ZHField>

          <ZHField
            label={t("masterdata.bp.detail.id.idNumber", "Número de identificación")}
            required
            fieldError={errors.identificationNumber?.message}
          >
            <ZhTextInput
              className="zh-input mono"
              {...register("identificationNumber")}
              disabled={saving || identityLocked}
            />
          </ZHField>

          <ZHField
            label={t("masterdata.bp.detail.id.legalEntityType", "Tipo de entidad legal")}
            required={!isLegalEntityTypeInferable(watchedIdentificationType)}
            hint={
              isLegalEntityTypeInferable(watchedIdentificationType)
                ? t("masterdata.bp.detail.id.inferable", "Se determina automáticamente a partir de la identificación.")
                : undefined
            }
            fieldError={errors.legalEntityTypeCode?.message}
          >
            <ZhSelect
              {...register("legalEntityTypeCode", {
                valueAsNumber: true,
              })}
              disabled={
                saving ||
                loadingLegalEntityTypes ||
                isLegalEntityTypeInferable(watchedIdentificationType)
              }
            >
              <option value="">{t("masterdata.bp.detail.select", "— Seleccionar —")}</option>
              {loadingLegalEntityTypes ? (
                <option value="">{t("masterdata.bp.detail.loadingShort", "Cargando…")}</option>
              ) : (
                legalEntityTypes.map((o) => (
                  <option key={o.code} value={o.code}>
                    {o.name}
                  </option>
                ))
              )}
            </ZhSelect>
          </ZHField>
        </ZHGrid>
      </div>

      <div className={modalLayout ? "zh-grid zh-grid--1" : "md-partner-group"}>
        <h4 className={modalLayout ? "zh-section-title" : "md-partner-group__title"}>{t("masterdata.bp.detail.id.main", "Datos principales")}</h4>
        <ZHGrid cols={modalLayout ? 2 : 3}>
          <ZHField
            label={t("masterdata.bp.detail.id.legalName", "Razón social")}
            required
            fieldError={errors.legalName?.message}
          >
            <ZhTextInput
              className="zh-input"
              {...register("legalName")}
              disabled={saving}
            />
          </ZHField>

          <ZHField label={t("masterdata.bp.detail.id.tradeName", "Nombre comercial")}>
            <ZhTextInput
              className="zh-input"
              {...register("tradeName")}
              disabled={saving}
            />
          </ZHField>

          <ZHField
            label={t("masterdata.bp.detail.id.country", "País (ISO alpha-2)")}
            fieldError={errors.countryCode?.message}
          >
            <Controller
              name="countryCode"
              control={control}
              render={({ field }) => (
                <ZhTextInput
                  className="zh-input mono"
                  maxLength={2}
                  placeholder="EC"
                  {...field}
                  value={field.value ?? ""}
                  onChange={(e) =>
                    field.onChange(e.target.value.toUpperCase().slice(0, 2))
                  }
                  disabled={saving}
                />
              )}
            />
          </ZHField>
        </ZHGrid>
      </div>

      {usage === "supplier" && !identityLocked && (
        <div className={modalLayout ? "zh-grid zh-grid--1" : "md-partner-group"}>
          <h4 className={modalLayout ? "zh-section-title" : "md-partner-group__title"}>{t("masterdata.bp.detail.id.supplierConfig", "Configuración del proveedor")}</h4>
          <ZHGrid cols={2}>
            <ZHField
              label={t("masterdata.bp.detail.sri.providerType", "Tipo de Proveedor")}
              required
              fieldError={errors.refundProviderTypeCode?.message}
            >
              <ZhSelect
                {...register("refundProviderTypeCode")}
                disabled={saving || loadingSupplierTypes}
              >
                <option value="">{t("masterdata.bp.detail.select", "— Seleccionar —")}</option>
                {supplierTypeOptions.map((o) => (
                  <option key={o.code} value={o.code}>
                    {o.name}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </ZHGrid>
        </div>
      )}
    </>
  );
}
