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
              generic: "No se pudo cargar la configuración del proveedor de sistema.",
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
            generic: "No se pudo guardar la configuración del proveedor de sistema.",
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  return (
    <PageShell
      title="Proveedor SRI"
      subtitle="Configuración global del proveedor del sistema de facturación electrónica"
    >
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
                message="Configuración del proveedor de sistema guardada correctamente."
              />
            ) : null}
            <ZHFormSection title="Identidad del proveedor">
              <ZHGrid cols={2}>
                <ZHField label="RUC" fieldError={errors.ruc?.message}>
                  <ZhTextInput disabled={saving} {...register("ruc")} />
                </ZHField>
                <ZHField label="Razón social" fieldError={errors.legalName?.message}>
                  <ZhTextInput disabled={saving} {...register("legalName")} />
                </ZHField>
                <ZHField label="Código CIIU" fieldError={errors.ciiuCode?.message}>
                  <ZhTextInput disabled={saving} {...register("ciiuCode")} />
                </ZHField>
                <ZHField label="Fecha de vigencia" fieldError={errors.effectiveDate?.message}>
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
                  description="Requiere RUC, razón social y CIIU completos."
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
                {saving ? "Guardando…" : "Guardar"}
              </ZHBtn>
            </div>
          </form>
        )}
      </ZHCard>
    </PageShell>
  );
}
