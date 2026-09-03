import { useEffect, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import {
  ZHBtn,
  ZHFormSection,
  ZHGrid,
  ZHField,
  ZHToggle,
} from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput, ZhDateInput } from "../../../components/zh/inputs";
import {
  systemProviderSettingsFormSchema,
  type SystemProviderSettingsFormValues,
} from "../../../schemas/systemProviderSettingsSchema";
import { systemProviderSettingsService } from "../api/systemProviderSettingsService";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";

const defaults = (): SystemProviderSettingsFormValues => ({
  ruc: "",
  legalName: "",
  ciiuCode: "",
  effectiveDate: "",
  enabled: false,
});

/**
 * AdminGlobalCore — configuración global del proveedor del sistema de facturación electrónica
 * (SystemProviderSettingsController, singleton por instancia del ERP, no por empresa). Nunca
 * llama endpoints operativos: la única llamada de red aquí es GET/PUT
 * /api/v1/system/provider-settings, protegido por policy PlatformAdmin en el backend.
 */
export function AdminCoreSystemProviderSettingsPage() {
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [saveError, setSaveError] = useState("");
  const [saving, setSaving] = useState(false);
  const [savedAt, setSavedAt] = useState<string | null>(null);

  const {
    register,
    handleSubmit,
    reset,
    control,
    setError: setFieldError,
    formState: { errors },
  } = useForm<SystemProviderSettingsFormValues>({
    resolver: zodResolver(systemProviderSettingsFormSchema),
    defaultValues: defaults(),
  });

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const current = await systemProviderSettingsService.get();
        if (cancelled) return;
        reset({
          ruc: current?.ruc ?? "",
          legalName: current?.legalName ?? "",
          ciiuCode: current?.ciiuCode ?? "",
          effectiveDate: current?.effectiveDate ?? "",
          enabled: current?.enabled ?? false,
        });
      } catch (e) {
        if (!cancelled)
          setLoadError(
            formatApiRequestError(e, {
              offline: "No se pudo conectar con el servidor.",
              generic: "No se pudo cargar la configuración global del proveedor tecnológico.",
            }),
          );
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [reset]);

  const onSubmit = handleSubmit(async (values) => {
    setSaveError("");
    setSavedAt(null);
    setSaving(true);
    try {
      const updated = await systemProviderSettingsService.update({
        ruc: values.ruc?.trim() || null,
        legalName: values.legalName?.trim() || null,
        ciiuCode: values.ciiuCode?.trim() || null,
        effectiveDate: values.effectiveDate?.trim() || null,
        enabled: values.enabled ?? false,
      });
      reset({
        ruc: updated.ruc ?? "",
        legalName: updated.legalName ?? "",
        ciiuCode: updated.ciiuCode ?? "",
        effectiveDate: updated.effectiveDate ?? "",
        enabled: updated.enabled,
      });
      setSavedAt(new Date().toISOString());
    } catch (e) {
      const applied = applyServerErrors(e, setFieldError, (msg) => setSaveError(msg));
      if (!applied) {
        setSaveError(
          formatApiRequestError(e, {
            offline: "No se pudo conectar con el servidor.",
            generic: "No se pudo guardar la configuración global del proveedor tecnológico.",
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  return (
    <PageShell
      title="Proveedor tecnológico SRI"
      subtitle="Configuración global de los datos del proveedor tecnológico usados por el sistema para el envío de comprobantes electrónicos."
    >
      <ZHPageNotice
        variant="info"
        message="Esta sección configura los datos del proveedor tecnológico del sistema requeridos para facturación electrónica. Estos datos aplican a toda la plataforma y no corresponden al RUC, firma electrónica, ambiente SRI, establecimientos ni puntos de emisión de una empresa."
      />
      <p className="zh-text-muted zh-text-xs zh-mb-8">
        Para configurar la firma electrónica o los parámetros SRI de una empresa, ingresa
        primero a la empresa desde AdminCore o usa el módulo de facturación electrónica dentro
        del ERP operativo.
      </p>
      <ZHCard>
        {loading ? (
          <p>Cargando configuración…</p>
        ) : loadError ? (
          <ZHPageNotice variant="error" message={loadError} />
        ) : (
          <form onSubmit={onSubmit}>
            {saveError ? <ZHPageNotice variant="error" message={saveError} /> : null}
            {savedAt ? (
              <ZHPageNotice
                variant="success"
                message="Configuración global del proveedor tecnológico guardada correctamente."
              />
            ) : null}
            <ZHFormSection title="Identidad del proveedor tecnológico">
              <ZHGrid cols={2}>
                <ZHField
                  label="RUC"
                  fieldError={errors.ruc?.message}
                  hint="RUC del proveedor tecnológico autorizado."
                >
                  <ZhTextInput disabled={saving} {...register("ruc")} />
                </ZHField>
                <ZHField
                  label="Razón social"
                  fieldError={errors.legalName?.message}
                  hint="Razón social registrada del proveedor tecnológico."
                >
                  <ZhTextInput disabled={saving} {...register("legalName")} />
                </ZHField>
                <ZHField
                  label="Código CIIU"
                  fieldError={errors.ciiuCode?.message}
                  hint="Actividad económica registrada para el proveedor tecnológico, si aplica."
                >
                  <ZhTextInput disabled={saving} {...register("ciiuCode")} />
                </ZHField>
                <ZHField
                  label="Fecha de vigencia"
                  fieldError={errors.effectiveDate?.message}
                  hint="Fecha desde la cual esta configuración global está vigente."
                >
                  <ZhDateInput disabled={saving} {...register("effectiveDate")} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
            <Controller
              name="enabled"
              control={control}
              render={({ field }) => (
                <ZHToggle
                  label="Habilitado"
                  description="Activa esta configuración global cuando los datos del proveedor tecnológico estén completos y vigentes."
                  value={!!field.value}
                  onChange={field.onChange}
                  disabled={saving}
                />
              )}
            />
            {errors.enabled?.message ? (
              <ZHPageNotice variant="error" message={errors.enabled.message} />
            ) : null}
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn variant="primary" size="sm" type="submit" disabled={saving}>
                {saving ? "Guardando…" : "Guardar configuración global"}
              </ZHBtn>
            </div>
          </form>
        )}
      </ZHCard>
    </PageShell>
  );
}
