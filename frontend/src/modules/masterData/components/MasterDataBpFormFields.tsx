import { useEffect, useState } from "react";
import { useFormContext, Controller } from "react-hook-form";
import { ZHField, ZHGrid } from "../../../components/zh/ZHForm";
import { ZhSelect } from "../../../components/zh/inputs";
import { useSriIdTypes, useSriIdTypesByUsage } from "../api/useSriIdTypes";
import { useSriSupplierTypes } from "../api/useSriSupplierTypes";
import { paymentTermService } from "../api/paymentTermService";
import type { PaymentTermDto } from "../api/paymentTermService";
import {
  isLegalEntityTypeInferable,
  type BusinessPartnerFormValues,
} from "../../../schemas/masterData/businessPartnerSchema";
import { useLegalEntityTypes } from "../api/useLegalEntityTypes";

type Props = {
  saving: boolean;
  section?: "identity" | "review";
  usage?: "customer" | "supplier";
};

export function MasterDataBpFormFields({
  saving,
  section = "identity",
  usage,
}: Props) {
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
  const [paymentTerms, setPaymentTerms] = useState<PaymentTermDto[]>([]);
  const watchedIdentificationType = watch("identificationType");

  useEffect(() => {
    // El backend infiere legalEntityTypeCode para RUC/CI — cualquier valor manual anterior
    // quedaría stale y produciría un 422 por contradicción con la nueva identificación.
    if (isLegalEntityTypeInferable(watchedIdentificationType)) {
      setValue("legalEntityTypeCode", undefined, { shouldValidate: false });
    }
  }, [watchedIdentificationType, setValue]);

  useEffect(() => {
    if (usage === "supplier")
      paymentTermService
        .list()
        .then(setPaymentTerms)
        .catch(() => {});
  }, [usage]);

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
    const paymentTermLabel = paymentTerms.find(
      (pt) => pt.id === values.paymentTermId,
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
            <dt>Condición de pago</dt>
            <dd>{paymentTermLabel ?? "—"}</dd>
          </>
        )}
      </dl>
    );
  }

  return (
    <ZHGrid cols={2}>
      <ZHField
        label="Tipo de identificación"
        required
        fieldError={errors.identificationType?.message}
      >
        <ZhSelect
          {...register("identificationType")}
          disabled={saving || loadingTypes}
        >
          {loadingTypes ? (
            <option value="">Cargando…</option>
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
        label="Número de identificación"
        required
        fieldError={errors.identificationNumber?.message}
      >
        <input
          className="zh-input mono"
          {...register("identificationNumber")}
          disabled={saving}
        />
      </ZHField>

      <ZHField
        label="Tipo de entidad legal"
        required={!isLegalEntityTypeInferable(watchedIdentificationType)}
        hint={
          isLegalEntityTypeInferable(watchedIdentificationType)
            ? "Se determina automáticamente a partir de la identificación."
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
          <option value="">— Seleccionar —</option>
          {loadingLegalEntityTypes ? (
            <option value="">Cargando...</option>
          ) : (
            legalEntityTypes.map((o) => (
              <option key={o.code} value={o.code}>
                {o.name}
              </option>
            ))
          )}
        </ZhSelect>
      </ZHField>

      {usage === "supplier" && (
        <>
          <ZHField
            label="Tipo de Proveedor"
            required
            fieldError={errors.refundProviderTypeCode?.message}
          >
            <ZhSelect
              {...register("refundProviderTypeCode")}
              disabled={saving || loadingSupplierTypes}
            >
              <option value="">— Seleccionar —</option>
              {supplierTypeOptions.map((o) => (
                <option key={o.code} value={o.code}>
                  {o.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>

          <ZHField
            label="Condición de pago"
            required
            fieldError={errors.paymentTermId?.message}
          >
            <ZhSelect {...register("paymentTermId")} disabled={saving}>
              <option value="">— Seleccionar —</option>
              {paymentTerms
                .filter((pt) => pt.isActive)
                .map((pt) => (
                  <option key={pt.id} value={pt.id}>
                    {pt.code} — {pt.name}
                  </option>
                ))}
            </ZhSelect>
          </ZHField>
        </>
      )}

      <ZHField
        label="Razón social"
        required
        fieldError={errors.legalName?.message}
      >
        <input
          className="zh-input"
          {...register("legalName")}
          disabled={saving}
        />
      </ZHField>

      <ZHField label="Nombre comercial">
        <input
          className="zh-input"
          {...register("tradeName")}
          disabled={saving}
        />
      </ZHField>

      <ZHField
        label="País (ISO alpha-2)"
        fieldError={errors.countryCode?.message}
      >
        <Controller
          name="countryCode"
          control={control}
          render={({ field }) => (
            <input
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
  );
}
