import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { LoadingState, NoAccessPage } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZhNumberInput, ZhDecimalInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { useAsync } from "../../../../hooks/useAsync";
import {
  loadPrecisionPolicy,
  savePrecisionPolicy,
  HIGH_PRECISION_DEFAULTS,
  type PrecisionPolicy,
  type PrecisionProfileType,
} from "../../../../lib/config/precisionPolicy.config";
import { applyServerErrors } from "../../../lib/validationErrors";
import { formatApiRequestError } from "../../../lib/apiError";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import {
  precisionPolicySchema,
  type PrecisionPolicyFormValues,
} from "../schemas/precisionPolicySchema";

const STANDARD_COMMERCIAL_VALUES: Omit<PrecisionPolicyFormValues, "profileType"> = {
  salesUnitPriceDecimals: 2,
  purchaseUnitPriceDecimals: 4,
  quantityDecimals: 4,
  percentageDecimals: 2,
  unitCostDecimals: 6,
  averageCostDecimals: 6,
  conversionFactorDecimals: 6,
  settlementToleranceAmount: 0.01,
};

const HIGH_PRECISION_VALUES: Omit<PrecisionPolicyFormValues, "profileType"> =
  HIGH_PRECISION_DEFAULTS;

const PROFILE_CARDS: {
  id: PrecisionProfileType;
  title: string;
  description: string;
  values?: Omit<PrecisionPolicyFormValues, "profileType">;
}[] = [
  {
    id: "StandardCommercial",
    title: "Estándar comercial",
    description: "Ventas 2 · Compras 4 · Cantidad 4 · % 2 · Costo 6 · Costo prom. 6 · Factor 6",
    values: STANDARD_COMMERCIAL_VALUES,
  },
  {
    id: "HighPrecision",
    title: "Alta precisión",
    description: "Ventas 4 · Compras 6 · Cantidad 6 · % 4 · Costo 6 · Costo prom. 6 · Factor 8",
    values: HIGH_PRECISION_VALUES,
  },
  {
    id: "Custom",
    title: "Personalizado",
    description: "Configura cada campo dentro de su rango permitido.",
  },
];

const DISCLAIMER_TEXT =
  "Esta configuración define cómo la empresa captura y calcula valores unitarios, cantidades, costos y tolerancia operativa. No modifica impuestos, totales fiscales, caja, CxC/CxP, contabilidad ni documentos ya autorizados.";

export function PrecisionPolicySettingsSection() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("erp.companies.view");
  const canEdit = canShow("erp.companies.update");

  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const cfgState = useAsync(() => loadPrecisionPolicy());

  const {
    register,
    handleSubmit,
    reset,
    watch,
    setValue,
    setError: setFieldError,
    formState: { errors, isDirty },
  } = useForm<PrecisionPolicyFormValues>({
    resolver: zodResolver(precisionPolicySchema),
    defaultValues: { profileType: "StandardCommercial", ...STANDARD_COMMERCIAL_VALUES },
  });

  const [locked, setLocked] = useState<PrecisionPolicy | null>(null);

  useEffect(() => {
    if (!cfgState.data) return;
    reset(toFormValues(cfgState.data));
    setLocked(cfgState.data.isLocked ? cfgState.data : null);
  }, [cfgState.data, reset]);

  const profileType = watch("profileType");

  const selectProfile = (id: PrecisionProfileType) => {
    if (!canEdit || locked) return;
    const card = PROFILE_CARDS.find((c) => c.id === id);
    setValue("profileType", id, { shouldDirty: true });
    if (card?.values) {
      for (const [key, value] of Object.entries(card.values)) {
        setValue(key as keyof PrecisionPolicyFormValues, value, {
          shouldDirty: true,
        });
      }
    }
  };

  const onSubmit = handleSubmit(async (values) => {
    if (!canEdit || locked) return;
    setSaveError(null);
    setSaved(false);
    setSaving(true);
    try {
      const result = await savePrecisionPolicy(values);
      setSaved(true);
      if (result.isLocked) setLocked(result);
      cfgState.refetch();
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
      // 409 (policy bloqueada por operación real detectada en este mismo intento):
      // refetch para reflejar isLocked=true de inmediato, sin esperar recarga manual.
      cfgState.refetch();
    } finally {
      setSaving(false);
    }
  });

  const handleDiscard = () => {
    setSaveError(null);
    setSaved(false);
    if (cfgState.data) reset(toFormValues(cfgState.data));
  };

  if (!canView) return <NoAccessPage title={t("settings.company.title")} />;
  if (cfgState.loading) return <LoadingState />;

  const readOnly = !canEdit || !!locked;

  return (
    <>
      {cfgState.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={cfgState.error}
        />
      )}
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={saveError}
        />
      )}
      {saved && !locked && (
        <ZHPageNotice variant="success" message={t("settings.company.saved")} />
      )}
      {locked && (
        <ZHPageNotice
          variant="warning"
          message="Configuración bloqueada"
          detail={
            locked.lockedReason
              ? `Esta configuración está bloqueada porque la empresa ya inició operaciones. Para cambios posteriores se requiere autorización externa del representante de la empresa y proceso de soporte. (${locked.lockedReason})`
              : "Esta configuración está bloqueada porque la empresa ya inició operaciones. Para cambios posteriores se requiere autorización externa del representante de la empresa y proceso de soporte."
          }
        />
      )}

      <form onSubmit={onSubmit}>
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                decimal_increase
              </span>
              <p className="pg-section-label">Precisión operativa</p>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="zh-text-muted zh-mb-16">{DISCLAIMER_TEXT}</p>

            <div role="radiogroup" aria-label="Perfil de precisión" className="zh-mb-16">
              <ZHGrid cols={3}>
                {PROFILE_CARDS.map((card) => {
                  const active = profileType === card.id;
                  return (
                    <ZHBtn
                      key={card.id}
                      type="button"
                      variant={active ? "primary" : "secondary"}
                      size="md"
                      disabled={readOnly}
                      onClick={() => selectProfile(card.id)}
                      aria-checked={active}
                      role="radio"
                      style={{
                        flexDirection: "column",
                        alignItems: "flex-start",
                        height: "auto",
                        whiteSpace: "normal",
                        textAlign: "left",
                        gap: 4,
                      }}
                    >
                      <strong>{card.title}</strong>
                      <span className="zh-text-muted" style={{ fontSize: "0.85em", fontWeight: 400 }}>
                        {card.description}
                      </span>
                    </ZHBtn>
                  );
                })}
              </ZHGrid>
            </div>

            {profileType === "Custom" && (
              <ZHGrid cols={2}>
                <ZHField
                  label="Precio unitario de venta (decimales)"
                  error={errors.salesUnitPriceDecimals?.message}
                >
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("salesUnitPriceDecimals")}
                  />
                </ZHField>
                <ZHField
                  label="Precio unitario de compra (decimales)"
                  error={errors.purchaseUnitPriceDecimals?.message}
                >
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("purchaseUnitPriceDecimals")}
                  />
                </ZHField>
                <ZHField label="Cantidad (decimales)" error={errors.quantityDecimals?.message}>
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("quantityDecimals")}
                  />
                </ZHField>
                <ZHField label="Porcentaje (decimales)" error={errors.percentageDecimals?.message}>
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("percentageDecimals")}
                  />
                </ZHField>
                <ZHField label="Costo unitario (decimales)" error={errors.unitCostDecimals?.message}>
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("unitCostDecimals")}
                  />
                </ZHField>
                <ZHField
                  label="Costo promedio (decimales)"
                  error={errors.averageCostDecimals?.message}
                >
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("averageCostDecimals")}
                  />
                </ZHField>
                <ZHField
                  label="Factor de conversión (decimales)"
                  error={errors.conversionFactorDecimals?.message}
                >
                  <ZhNumberInput
                    disabled={readOnly}
                    positiveOnly
                    {...register("conversionFactorDecimals")}
                  />
                </ZHField>
                <ZHField
                  label="Tolerancia de cuadre (monto)"
                  error={errors.settlementToleranceAmount?.message}
                >
                  <ZhDecimalInput
                    disabled={readOnly}
                    decimals={2}
                    positiveOnly
                    {...register("settlementToleranceAmount")}
                  />
                </ZHField>
              </ZHGrid>
            )}
          </div>
        </div>

        {!locked && (
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
        )}
      </form>
    </>
  );
}

function toFormValues(cfg: PrecisionPolicy): PrecisionPolicyFormValues {
  return {
    profileType: cfg.profileType,
    salesUnitPriceDecimals: cfg.salesUnitPriceDecimals,
    purchaseUnitPriceDecimals: cfg.purchaseUnitPriceDecimals,
    quantityDecimals: cfg.quantityDecimals,
    percentageDecimals: cfg.percentageDecimals,
    unitCostDecimals: cfg.unitCostDecimals,
    averageCostDecimals: cfg.averageCostDecimals,
    conversionFactorDecimals: cfg.conversionFactorDecimals,
    settlementToleranceAmount: cfg.settlementToleranceAmount,
  };
}
