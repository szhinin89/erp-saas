import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, NoAccessPage } from "../../../components/PageShell";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHIconButton } from "../../../components/zh/ZHIconButton";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { ZhDateInput } from "../../../components/zh/inputs/ZhDateInput";
import { ZhSelect } from "../../../components/zh/inputs/ZhSelect";
import { ZhTextInput } from "../../../components/zh/inputs/ZhTextInput";
import { EmployeeCreateModal } from "../components/EmployeeCreateModal";
import type { EmployeeCatalogEntry, EmployeeListItem } from "../types/employee.types";
import { EMPLOYEES_PAGE_SIZE, useEmployeesPage, type EmployeeStatusFilter } from "./useEmployeesPage";

export function EmployeesPage() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const page = useEmployeesPage();
  const [creating, setCreating] = useState(false);
  const title = t("employees.list.title", "Empleados");

  if (!page.canView) return <NoAccessPage title={title} />;

  const { filters, setFilter } = page;
  const open = (id: string) => navigate(`/employees/${id}`);
  const inactiveSuffix = t("employees.inactiveSuffix", "(inactivo)");
  const optionLabel = (name: string, isActive: boolean) => (isActive ? name : `${name} ${inactiveSuffix}`);
  const catalogCell = (ref: EmployeeListItem["jobPosition"]) =>
    ref ? optionLabel(ref.name, ref.isActive) : "—";

  const catalogFilter = (
    key: "jobPositionId" | "professionId" | "specialtyId",
    label: string,
    rows: EmployeeCatalogEntry[] | undefined,
  ) => (
    <ZHField label={label} density="compact">
      <ZhSelect aria-label={label} value={filters[key]} onChange={(e) => setFilter(key, e.target.value)}>
        <option value="">{t("employees.filters.all", "Todos")}</option>
        {(rows ?? []).map((r) => (
          <option key={r.id} value={r.id}>
            {optionLabel(r.name, r.isActive)}
          </option>
        ))}
      </ZhSelect>
    </ZHField>
  );

  const columns: ZHDataTableColumn<EmployeeListItem>[] = [
    {
      key: "identification",
      header: t("employees.fields.identification", "Identificación"),
      render: (row) => <span className="mono">{row.businessPartner.identificationNumber ?? "—"}</span>,
    },
    {
      key: "name",
      header: t("employees.fields.name", "Nombre"),
      render: (row) => row.businessPartner.displayName ?? "—",
    },
    {
      key: "jobPosition",
      header: t("employees.fields.jobPosition", "Cargo"),
      render: (row) => catalogCell(row.jobPosition),
    },
    {
      key: "profession",
      header: t("employees.fields.profession", "Profesión"),
      render: (row) => catalogCell(row.profession),
    },
    {
      key: "status",
      header: t("employees.fields.status", "Estado"),
      render: (row) => (
        <Badge
          size="md"
          variant={row.isActive ? "success" : "neutral"}
          label={
            row.isActive
              ? t("employees.status.active", "Activo")
              : t("employees.status.terminated", "De baja")
          }
        />
      ),
    },
    {
      key: "actions",
      header: t("employees.fields.actions", "Acciones"),
      align: "right",
      render: (row) => (
        <ZHIconButton
          icon="visibility"
          variant="ghost"
          title={t("employees.list.open", "Ver ficha")}
          ariaLabel={`${t("employees.list.open", "Ver ficha")} ${row.businessPartner.displayName ?? ""}`}
          // La fila también navega: sin detener el evento se apilaría la ficha dos veces.
          onClick={(e) => {
            e.stopPropagation();
            open(row.id);
          }}
        />
      ),
    },
  ];

  const missingFilterPermissions = [
    page.canViewCatalogs
      ? null
      : t(
          "employees.list.noCatalogFilters",
          "Los filtros por cargo, profesión y especialidad requieren el permiso de consulta de catálogos laborales.",
        ),
    page.canViewBranches
      ? null
      : t(
          "employees.list.noBranchFilter",
          "El filtro por sucursal requiere el permiso de consulta de sucursales.",
        ),
  ].filter(Boolean);

  return (
    <ErpPageTemplate
      title={title}
      subtitle={t("employees.list.subtitle", "Fichas laborales de la empresa activa.")}
      action={
        page.canCreate ? (
          <ZHBtn variant="primary" size="md" type="button" icon="add" onClick={() => setCreating(true)}>
            {t("employees.list.new", "Nuevo empleado")}
          </ZHBtn>
        ) : null
      }
    >
      {page.error && <ZHPageNotice variant="error" message={page.error} />}
      {page.catalogs.error && <ZHPageNotice variant="error" message={page.catalogs.error} />}
      {page.branches.error && <ZHPageNotice variant="error" message={page.branches.error} />}
      {missingFilterPermissions.length > 0 && (
        <ZHPageNotice variant="info" message={missingFilterPermissions.join(" ")} />
      )}

      <ZHCard>
        <ZHFilterBar plain onClear={page.clearFilters} clearLabel={t("employees.filters.clear", "Limpiar filtros")}>
          <ZHField label={t("employees.filters.search", "Buscar")} density="compact">
            <ZhTextInput
              aria-label={t("employees.filters.search", "Buscar")}
              placeholder={t("employees.filters.searchPlaceholder", "Nombre o identificación")}
              maxLength={150}
              value={filters.search}
              onChange={(e) => setFilter("search", e.target.value)}
            />
          </ZHField>
          <ZHField label={t("employees.fields.status", "Estado")} density="compact">
            <ZhSelect
              aria-label={t("employees.fields.status", "Estado")}
              value={filters.status}
              onChange={(e) => setFilter("status", e.target.value as EmployeeStatusFilter)}
            >
              <option value="">{t("employees.filters.all", "Todos")}</option>
              <option value="active">{t("employees.status.active", "Activo")}</option>
              <option value="inactive">{t("employees.status.terminated", "De baja")}</option>
            </ZhSelect>
          </ZHField>
          {page.canViewCatalogs && (
            <>
              {catalogFilter("jobPositionId", t("employees.fields.jobPosition", "Cargo"), page.catalogs.data?.jobPositions)}
              {catalogFilter("professionId", t("employees.fields.profession", "Profesión"), page.catalogs.data?.professions)}
              {catalogFilter("specialtyId", t("employees.fields.specialty", "Especialidad"), page.catalogs.data?.specialties)}
            </>
          )}
          {page.canViewBranches && (
            <>
              <ZHField label={t("employees.fields.branch", "Sucursal")} density="compact">
                <ZhSelect
                  aria-label={t("employees.fields.branch", "Sucursal")}
                  value={filters.branchId}
                  onChange={(e) => setFilter("branchId", e.target.value)}
                >
                  <option value="">{t("employees.filters.all", "Todos")}</option>
                  {(page.branches.data ?? []).map((b) => (
                    <option key={b.id} value={b.id}>
                      {optionLabel(b.name, b.isActive)}
                    </option>
                  ))}
                </ZhSelect>
              </ZHField>
              <ZHField label={t("employees.filters.asOf", "Asignado en la fecha")} density="compact">
                <ZhDateInput
                  aria-label={t("employees.filters.asOf", "Asignado en la fecha")}
                  density="compact"
                  disabled={!filters.branchId}
                  value={filters.asOf}
                  onChange={(e) => setFilter("asOf", e.target.value)}
                />
              </ZHField>
            </>
          )}
        </ZHFilterBar>

        <ZHDataTable
          columns={columns}
          rows={page.items}
          rowKey={(row) => row.id}
          loading={page.loading}
          emptyMessage={t("employees.list.empty", "No hay empleados que coincidan con la búsqueda.")}
          onRowClick={(row) => open(row.id)}
          page={page.page}
          pageSize={EMPLOYEES_PAGE_SIZE}
          total={page.totalCount}
          onPageChange={page.setPage}
        />
      </ZHCard>

      {creating && (
        <EmployeeCreateModal
          canViewCatalogs={page.canViewCatalogs}
          onClose={() => setCreating(false)}
          onCreated={(employee) => {
            setCreating(false);
            page.refetch();
            open(employee.id);
          }}
        />
      )}
    </ErpPageTemplate>
  );
}
