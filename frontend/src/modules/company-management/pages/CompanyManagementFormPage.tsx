import { useEffect, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import {
  companyManagementFormSchema,
  type CompanyManagementFormValues,
} from "../../../schemas/companyManagementSchema";
import { companyManagementService } from "../api/companyManagementService";
import { PageShell } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import {
  ZHFormSection,
  ZHGrid,
  ZHField,
  ZHToggle,
} from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhTextInput } from "../../../components/zh/inputs";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";

const defaults = (): CompanyManagementFormValues => ({
  taxId: "",
  legalName: "",
  tradeName: "",
  isActive: true,
});

export function CompanyManagementFormPage({
  mode,
}: {
  mode: "create" | "edit";
}) {
  const { t } = useI18n();
  const navigate = useNavigate();
  const { id } = useParams<{ id: string }>();
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const {
    register,
    handleSubmit,
    reset,
    control,
    setError: setFieldError,
    formState: { errors },
  } = useForm<CompanyManagementFormValues>({
    resolver: zodResolver(companyManagementFormSchema),
    defaultValues: defaults(),
  });

  useEffect(() => {
    if (mode !== "edit" || !id) return;
    let cancelled = false;
    (async () => {
      try {
        const detail = await companyManagementService.getById(id);
        if (!cancelled) {
          reset({
            taxId: detail.taxId,
            legalName: detail.legalName,
            tradeName: detail.tradeName ?? "",
            isActive: detail.isActive,
          });
        }
      } catch {
        if (!cancelled) setError(t("companyManagement.error.load"));
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [mode, id, reset, t]);

  const onSubmit = handleSubmit(async (values) => {
    setError("");
    setSaving(true);
    try {
      const payload = {
        taxId: values.taxId.trim(),
        legalName: values.legalName.trim(),
        tradeName: values.tradeName?.trim() || null,
      };
      if (mode === "create") {
        await companyManagementService.create(payload);
      } else if (id) {
        await companyManagementService.update(id, {
          id,
          ...payload,
          isActive: values.isActive ?? true,
        });
      }
      navigate("/companies", { replace: true });
    } catch (e) {
      const generic =
        mode === "create"
          ? t("companyManagement.error.create")
          : t("companyManagement.error.update");
      const applied = applyServerErrors(e, setFieldError, (msg) =>
        setError(msg),
      );
      if (!applied) {
        setError(
          formatApiRequestError(e, {
            offline: t("common.apiUnreachable"),
            generic,
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  return (
    <PageShell
      title={
        mode === "create"
          ? t("companyManagement.create")
          : t("companyManagement.edit")
      }
      subtitle={t("companyManagement.formSubtitle")}
    >
      <ZHCard>
        <form onSubmit={onSubmit}>
          {error ? <ZHPageNotice variant="error" message={error} /> : null}
          <ZHFormSection title={t("companyManagement.sectionIdentity")}>
            <ZHGrid cols={2}>
              <ZHField
                label={t("companyManagement.taxId")}
                required
                fieldError={errors.taxId?.message}
              >
                <ZhTextInput
                  disabled={saving || mode === "edit"}
                  {...register("taxId")}
                />
              </ZHField>
              <ZHField
                label={t("companyManagement.legalName")}
                required
                fieldError={errors.legalName?.message}
              >
                <ZhTextInput disabled={saving} {...register("legalName")} />
              </ZHField>
              <ZHField
                label={t("companyManagement.tradeName")}
                fieldError={errors.tradeName?.message}
              >
                <ZhTextInput disabled={saving} {...register("tradeName")} />
              </ZHField>
            </ZHGrid>
          </ZHFormSection>
          {mode === "edit" ? (
            <p className="zh-text-muted zh-text-xs zh-mb-8">
              Contacto, marca, moneda, zona horaria y datos fiscales se
              administran desde{" "}
              <Link to="/settings/company" className="zh-link">
                Configuración de Empresa
              </Link>{" "}
              (empresa activa).
            </p>
          ) : null}
          {mode === "edit" ? (
            <Controller
              name="isActive"
              control={control}
              render={({ field }) => (
                <ZHToggle
                  label={t("common.active")}
                  description={t("companyManagement.isActiveHint")}
                  value={!!field.value}
                  onChange={field.onChange}
                  disabled={saving}
                />
              )}
            />
          ) : null}
          <div className="zh-form-actions-row zh-form-actions-row--end">
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => navigate("/companies")}
            >
              {t("common.cancel")}
            </ZHBtn>
            <ZHBtn variant="primary" size="sm" type="submit" disabled={saving}>
              {saving ? t("common.saving") : t("common.save")}
            </ZHBtn>
          </div>
        </form>
      </ZHCard>
    </PageShell>
  );
}
