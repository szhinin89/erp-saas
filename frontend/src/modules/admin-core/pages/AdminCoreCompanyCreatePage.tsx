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
  adminCoreCompanyCreateSchema,
  type AdminCoreCompanyCreateValues,
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
import type { InitialAdministratorCredentials } from "../../../types/companyManagement";
import { TemporaryCredentialsPanel } from "../components/TemporaryCredentialsPanel";

const defaults = (): AdminCoreCompanyCreateValues => ({
  tenantId: "",
  taxId: "",
  legalName: "",
  tradeName: "",
  isActive: true,
  adminUsername: "",
  adminFirstName: "",
  adminLastName: "",
  adminEmail: "",
});

/** Empresa recién creada y, si aplica, credenciales temporales del administrador (solo en memoria). */
interface CreatedState {
  legalName: string;
  credentials: InitialAdministratorCredentials | null;
}

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
  const [created, setCreated] = useState<CreatedState | null>(null);
  // «Ya copié las credenciales»: descarta la contraseña del estado; no se puede volver a mostrar.
  const [credentialsDismissed, setCredentialsDismissed] = useState(false);
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
  } = useForm<AdminCoreCompanyCreateValues>({
    resolver: zodResolver(adminCoreCompanyCreateSchema),
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
        initialAdministrator: {
          username: values.adminUsername.trim(),
          firstName: values.adminFirstName.trim(),
          lastName: values.adminLastName.trim(),
          email: values.adminEmail.trim(),
        },
      });
      setCredentialsDismissed(false);
      setCreated({
        legalName: detail.legalName,
        credentials: detail.initialAdministrator ?? null,
      });
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
          <ZHPageNotice variant="success" message={`Empresa "${created.legalName}" creada correctamente.`} />
          {created.credentials && !credentialsDismissed ? (
            <>
              <TemporaryCredentialsPanel
                title="Credenciales del administrador inicial de la empresa."
                username={created.credentials.username}
                temporaryPassword={created.credentials.temporaryPassword}
              />
              <ZHBtn
                variant="primary"
                size="sm"
                type="button"
                onClick={() => {
                  setCredentialsDismissed(true);
                  setCreated({ ...created, credentials: null });
                }}
              >
                Ya copié las credenciales
              </ZHBtn>
            </>
          ) : created.credentials === null ? (
            <ZHPageNotice
              variant="info"
              message="Las credenciales ya no se pueden consultar."
              detail="Si se perdieron, genera una nueva contraseña temporal desde el dashboard global (Administradores)."
            />
          ) : null}
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
            <ZHFormSection title="Administrador inicial">
              <ZHPageNotice
                variant="info"
                message="La contraseña temporal se genera automáticamente y se mostrará una sola vez al crear la empresa. El administrador deberá cambiarla en su primer ingreso."
              />
              <ZHGrid cols={2}>
                <ZHField label="Usuario" required fieldError={errors.adminUsername?.message}>
                  <ZhTextInput
                    disabled={saving}
                    autoComplete="off"
                    {...register("adminUsername")}
                  />
                </ZHField>
                <ZHField label="Correo electrónico" required fieldError={errors.adminEmail?.message}>
                  <ZhTextInput
                    disabled={saving}
                    inputMode="email"
                    autoComplete="off"
                    {...register("adminEmail")}
                  />
                </ZHField>
                <ZHField label="Nombres" required fieldError={errors.adminFirstName?.message}>
                  <ZhTextInput disabled={saving} {...register("adminFirstName")} />
                </ZHField>
                <ZHField label="Apellidos" required fieldError={errors.adminLastName?.message}>
                  <ZhTextInput disabled={saving} {...register("adminLastName")} />
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
