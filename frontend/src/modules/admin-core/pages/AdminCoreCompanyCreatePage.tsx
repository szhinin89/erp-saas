import { useEffect, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate, useSearchParams } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHFormSection, ZHGrid, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import {
  companyManagementFormSchema,
  type CompanyManagementFormValues,
} from "../../../schemas/companyManagementSchema";
import { companyRegistrationFacade } from "../../company-management/facades/companyRegistrationFacade";
import { adminCoreService } from "../api/adminCoreService";
import {
  clearCompanyCreateTenantHint,
  readCompanyCreateTenantHint,
} from "../utils/companyCreateTenantHint";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import { logoutSession } from "../../../lib/session/logoutSession";
import type { AdminCoreTenant } from "../../../types/adminCore";

const defaults = (): CompanyManagementFormValues => ({
  tenantId: "",
  taxId: "",
  legalName: "",
  tradeName: "",
  isActive: true,
});

/**
 * AdminGlobalCore — crear empresa. Reutiliza el schema y el servicio de company-management
 * (puros, sin acoplamiento a layout operativo) pero NO reutiliza CompanyManagementFormPage: su
 * onSubmit navega a /companies de forma fija y no expone un onSuccess inyectable. Tras crear, no
 * redirige — muestra éxito con 3 acciones.
 *
 * El tenant destino se elige por nombre (select), nunca escribiendo el GUID a mano — se deriva
 * de GET /api/v1/admin-core/companies vía adminCoreService.listTenants(). Desde el dashboard global
 * llega preseleccionado por un hint de un solo uso en sessionStorage (SEC-02, nunca en la URL),
 * que solo se aplica si el tenant figura en ese listado.
 */
export function AdminCoreCompanyCreatePage() {
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState<string | null>(null);
  const [tenants, setTenants] = useState<AdminCoreTenant[]>([]);
  const [loadingTenants, setLoadingTenants] = useState(true);
  const [tenantsError, setTenantsError] = useState("");

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setError: setFieldError,
    formState: { errors },
  } = useForm<CompanyManagementFormValues>({
    resolver: zodResolver(companyManagementFormSchema),
    defaultValues: defaults(),
  });

  useEffect(() => {
    let cancelled = false;
    const tenantHint = readCompanyCreateTenantHint();
    (async () => {
      try {
        const rows = await adminCoreService.listTenants();
        if (cancelled) return;
        setTenants(rows);

        if (tenantHint && rows.some((t) => t.tenantId === tenantHint)) {
          setValue("tenantId", tenantHint);
        } else if (rows.length === 1) {
          setValue("tenantId", rows[0].tenantId);
        }
      } catch (e) {
        if (!cancelled)
          setTenantsError(
            formatApiRequestError(e, {
              offline: "No se pudo conectar con el servidor.",
              generic: "No se pudo cargar el listado de tenants.",
            }),
          );
      } finally {
        if (!cancelled) {
          // Consumo único: aplicado, rechazado por la API o con error de carga, no se reutiliza.
          clearCompanyCreateTenantHint();
          setLoadingTenants(false);
        }
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // SEC-02: enlaces legacy con el tenant en la URL — se quita sin usarlo como preselección.
  useEffect(() => {
    if (!searchParams.has("tenantId")) return;
    const next = new URLSearchParams(searchParams);
    next.delete("tenantId");
    setSearchParams(next, { replace: true });
  }, [searchParams, setSearchParams]);

  const onSubmit = handleSubmit(async (values) => {
    setError("");
    setSaving(true);
    try {
      const tenantId = values.tenantId?.trim();
      if (!tenantId) {
        setFieldError("tenantId", {
          type: "manual",
          message: "Selecciona el tenant o grupo destino.",
        });
        setSaving(false);
        return;
      }

      const detail = await companyRegistrationFacade.create({
        tenantId,
        taxId: values.taxId.trim(),
        legalName: values.legalName.trim(),
        tradeName: values.tradeName?.trim() || null,
      });
      setCreated(detail.legalName);
    } catch (e) {
      const applied = applyServerErrors(e, setFieldError, (msg) => setError(msg));
      if (!applied) {
        setError(
          formatApiRequestError(e, {
            offline: "No se pudo conectar con el servidor.",
            generic: "No se pudo crear la empresa.",
          }),
        );
      }
    } finally {
      setSaving(false);
    }
  });

  if (created) {
    return (
      <PageShell title="Empresa creada" subtitle="AdminGlobalCore">
        <ZHCard>
          <ZHPageNotice variant="success" message={`Empresa "${created}" creada correctamente.`} />
          <div className="zh-form-actions-row zh-mt-16">
            <ZHBtn
              variant="primary"
              size="sm"
              type="button"
              onClick={() => {
                setCreated(null);
                reset(defaults());
              }}
            >
              Crear otra empresa
            </ZHBtn>
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => navigate("/admin-core/dashboard")}
            >
              Volver al dashboard global
            </ZHBtn>
            <ZHBtn
              variant="ghost"
              size="sm"
              type="button"
              onClick={() =>
                void logoutSession().finally(() => navigate("/admin-core/login"))
              }
            >
              Cerrar sesión
            </ZHBtn>
          </div>
        </ZHCard>
      </PageShell>
    );
  }

  return (
    <PageShell title="Nueva empresa" subtitle="AdminGlobalCore">
      <ZHCard>
        {loadingTenants ? (
          <p>Cargando tenants…</p>
        ) : tenantsError ? (
          <ZHPageNotice variant="error" message={tenantsError} />
        ) : tenants.length === 0 ? (
          <ZHPageNotice
            variant="warning"
            message="No hay tenants disponibles para crear empresas."
          />
        ) : (
          <form onSubmit={onSubmit}>
            {error ? <ZHPageNotice variant="error" message={error} /> : null}
            <ZHFormSection title="Identidad">
              <ZHGrid cols={2}>
                <ZHField
                  label="Tenant / grupo destino"
                  required
                  fieldError={errors.tenantId?.message}
                  hint="Selecciona el tenant o grupo donde se creará la nueva empresa."
                >
                  <ZhSelect disabled={saving} {...register("tenantId")}>
                    <option value="">Selecciona un tenant…</option>
                    {tenants.map((t) => (
                      <option key={t.tenantId} value={t.tenantId}>
                        {t.tenantName}
                        {t.tenantIsActive ? "" : " (inactivo)"}
                      </option>
                    ))}
                  </ZhSelect>
                </ZHField>
                <ZHField label="RUC" required fieldError={errors.taxId?.message}>
                  <ZhTextInput disabled={saving} {...register("taxId")} />
                </ZHField>
                <ZHField label="Razón social" required fieldError={errors.legalName?.message}>
                  <ZhTextInput disabled={saving} {...register("legalName")} />
                </ZHField>
                <ZHField label="Nombre comercial" fieldError={errors.tradeName?.message}>
                  <ZhTextInput disabled={saving} {...register("tradeName")} />
                </ZHField>
              </ZHGrid>
            </ZHFormSection>
            <div className="zh-form-actions-row zh-form-actions-row--end">
              <ZHBtn
                variant="ghost"
                size="sm"
                type="button"
                onClick={() => navigate("/admin-core/dashboard")}
              >
                Cancelar
              </ZHBtn>
              <ZHBtn variant="primary" size="sm" type="submit" disabled={saving}>
                {saving ? "Guardando…" : "Crear empresa"}
              </ZHBtn>
            </div>
          </form>
        )}
      </ZHCard>
    </PageShell>
  );
}
