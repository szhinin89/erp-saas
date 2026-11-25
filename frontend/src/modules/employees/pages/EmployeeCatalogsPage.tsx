import { useI18n } from "../../../i18n/i18n";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { EmptyState, NoAccessPage } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { EmployeeCatalogListTab } from "../components/EmployeeCatalogListTab";
import { EmployeeCatalogFormTab } from "../components/EmployeeCatalogFormTab";
import type { EmployeeCatalogKind } from "../types/employee.types";
import { CATALOG_PAGE_SIZE, useEmployeeCatalogsPage } from "./useEmployeeCatalogsPage";
import "../../../styles/shared/items-catalog.css";

/** `/employees/catalogs`: una pestaña por catálogo, patrón Lista → Editor. Sin borrado físico. */
export function EmployeeCatalogsPage() {
  const { t } = useI18n();
  const page = useEmployeeCatalogsPage();
  const title = t("employees.catalogs.title", "Catálogos laborales");

  if (!page.canView) return <NoAccessPage title={title} />;

  const tabs: { id: EmployeeCatalogKind; label: string }[] = [
    { id: "job-positions", label: t("employees.catalogs.jobPositions", "Cargos") },
    { id: "professions", label: t("employees.catalogs.professions", "Profesiones") },
    { id: "specialties", label: t("employees.catalogs.specialties", "Especialidades") },
  ];

  return (
    <ErpPageTemplate
      title={title}
      subtitle={t("employees.catalogs.subtitle", "Cargos, profesiones y especialidades de la empresa activa.")}
      action={
        page.canManage ? (
          <ZHBtn variant="primary" size="md" type="button" icon="add" onClick={() => page.openEditor(null)}>
            {t("employees.catalogs.new", "Nuevo registro")}
          </ZHBtn>
        ) : null
      }
    >
      <ZHTabBar<EmployeeCatalogKind>
        ariaLabel={title}
        tabs={tabs}
        activeTab={page.kind}
        onChange={page.changeKind}
      />

      <ConfigTabsLayout
        activeTab={page.view}
        onTabChange={page.setView}
        editorLabel={
          page.editing
            ? t("employees.catalogs.edit", "Editar registro")
            : t("employees.catalogs.new", "Nuevo registro")
        }
        editorIcon={page.editing ? "edit" : "add_box"}
        error={page.error ? <ZHPageNotice variant="error" message={page.error} /> : undefined}
        listContent={
          <EmployeeCatalogListTab
            items={page.items}
            loading={page.loading}
            saving={page.saving}
            canManage={page.canManage}
            search={page.searchText}
            status={page.status}
            page={page.page}
            pageSize={CATALOG_PAGE_SIZE}
            totalCount={page.totalCount}
            onSearch={page.setSearch}
            onStatus={page.setStatus}
            onPage={page.setPage}
            onEdit={page.openEditor}
            onSetActive={page.setActive}
          />
        }
        editorContent={
          page.editorOpen && page.canManage ? (
            <EmployeeCatalogFormTab
              form={page.form}
              editing={!!page.editing}
              saving={page.saving}
              saveError={page.saveError}
              onSubmit={page.save}
              onCancel={page.cancel}
            />
          ) : (
            <EmptyState
              message={
                page.canManage
                  ? t("employees.catalogs.editorEmpty", "Seleccione un registro de la lista o cree uno nuevo.")
                  : t("employees.catalogs.noManagePermission", "No tiene permiso para administrar catálogos laborales.")
              }
            />
          )
        }
      />
    </ErpPageTemplate>
  );
}
