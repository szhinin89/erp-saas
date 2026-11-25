import { useCallback, useEffect, useState, type ReactNode } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { useUnsavedChangesGuard } from "../../../hooks/useUnsavedChangesGuard";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, EmptyState, ErrorState, LoadingState, NoAccessPage } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { EmployeeAccessTab } from "../components/EmployeeAccessTab";
import { EmployeeBranchesTab } from "../components/EmployeeBranchesTab";
import { EmployeeGeneralTab } from "../components/EmployeeGeneralTab";
import { useEmployeeDetailPage } from "./useEmployeeDetailPage";
import "../../../styles/shared/items-catalog.css";

type TabId = "general" | "branches" | "access";
const TABS: readonly TabId[] = ["general", "branches", "access"];

/** Ficha `/employees/:id`: General, Sucursales y Acceso ERP (`?tab=`). */
export function EmployeeDetailPage() {
  const { t } = useI18n();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const page = useEmployeeDetailPage(id);
  const requested = searchParams.get("tab") as TabId | null;
  const activeTab: TabId = requested && TABS.includes(requested) ? requested : "general";
  const [visited, setVisited] = useState<ReadonlySet<TabId>>(new Set([activeTab]));
  const [dirtyMap, setDirtyMap] = useState<Record<string, boolean>>({});

  useEffect(() => {
    setVisited((prev) => (prev.has(activeTab) ? prev : new Set(prev).add(activeTab)));
  }, [activeTab]);

  const reportDirty = useCallback((key: string, dirty: boolean) => {
    setDirtyMap((prev) => (!!prev[key] === dirty ? prev : { ...prev, [key]: dirty }));
  }, []);
  // Bloquea salir de la ficha con cambios; cambiar de pestaña (`?tab=`) no sale de la ruta.
  useUnsavedChangesGuard(Object.values(dirtyMap).some(Boolean));

  const title = t("employees.detail.title", "Ficha del empleado");
  if (!page.canView) return <NoAccessPage title={title} />;

  const { employee, permissions, saving, runWrite } = page;
  const labels: Record<TabId, string> = {
    general: t("employees.detail.tabs.general", "General"),
    branches: t("employees.detail.tabs.branches", "Sucursales"),
    access: t("employees.detail.tabs.access", "Acceso ERP"),
  };

  const panel = (tab: TabId, content: () => ReactNode) => (
    <div
      role="tabpanel"
      id={`emp-panel-${tab}`}
      aria-labelledby={`emp-tab-${tab}`}
      hidden={tab !== activeTab}
      className="prd-tab-content"
    >
      {visited.has(tab) || tab === activeTab ? content() : null}
    </div>
  );

  return (
    <ErpPageTemplate
      title={employee?.businessPartner.displayName ?? title}
      subtitle={employee?.businessPartner.identificationNumber ?? undefined}
      action={
        <ZHBtn variant="ghost" type="button" icon="arrow_back" onClick={() => navigate("/employees")}>
          {t("employees.detail.back", "Volver a empleados")}
        </ZHBtn>
      }
    >
      {page.loading && <LoadingState />}
      {!page.loading && page.notFound && (
        <EmptyState message={t("employees.detail.notFound", "El empleado no existe en la empresa activa.")} />
      )}
      {!page.loading && page.error && <ErrorState message={page.error} />}

      {!page.loading && employee && (
        <>
          <div>
            <Badge
              size="md"
              variant={employee.isActive ? "success" : "neutral"}
              label={
                employee.isActive
                  ? t("employees.status.active", "Activo")
                  : t("employees.status.terminated", "De baja")
              }
            />
          </div>
          {page.membershipWarning && (
            <ZHPageNotice
              variant="warning"
              message={t(
                "employees.detail.activeMembership",
                "El usuario vinculado conserva su membresía activa.",
              )}
              detail={t(
                "employees.detail.activeMembershipDetail",
                "La baja laboral no revoca automáticamente el acceso al ERP.",
              )}
            />
          )}

          <ZHTabBar<TabId>
            ariaLabel={t("employees.detail.tabs.aria", "Secciones de la ficha")}
            idPrefix="emp-tab"
            panelIdPrefix="emp-panel"
            tabs={TABS.map((tab) => ({ id: tab, label: labels[tab] }))}
            activeTab={activeTab}
            onChange={(tab) => setSearchParams({ tab })}
          />

          {panel("general", () => (
            <EmployeeGeneralTab
              key={`${employee.id}:${page.resetToken}`}
              employee={employee}
              permissions={permissions}
              catalogs={page.catalogs}
              saving={saving}
              runWrite={runWrite}
              reload={page.reload}
              onDirtyChange={reportDirty}
            />
          ))}
          {panel("branches", () => (
            <EmployeeBranchesTab
              employee={employee}
              permissions={permissions}
              saving={saving}
              runWrite={runWrite}
              onDirtyChange={reportDirty}
            />
          ))}
          {panel("access", () => (
            <EmployeeAccessTab
              employee={employee}
              permissions={permissions}
              saving={saving}
              runWrite={runWrite}
              onDirtyChange={reportDirty}
            />
          ))}
        </>
      )}
    </ErpPageTemplate>
  );
}
