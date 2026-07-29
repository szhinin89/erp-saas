import { useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { NoAccessPage } from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../../components/shared/ConfigTabsLayout";
import { message } from "../../../../lib/messages";

import { useWarehousesPage } from "./useWarehousesPage";
import { WarehouseListadoTab } from "../components/WarehouseListTab";
import { WarehouseFormTab } from "../components/WarehouseFormTab";

import "../../../../styles/shared/items-catalog.css";
import "./BodegasPage.css";

export function BodegasPage() {
  const { t } = useI18n();
  const page = useWarehousesPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  if (!page.canView) {
    return <NoAccessPage title={t("warehouses.title", "Gestión de Bodegas")} />;
  }

  const handleOpenCreate = () => {
    page.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = async (id: string) => {
    await page.openEdit(id);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    page.closePanel();
    setActiveTab("list");
  };

  const editorLabel = page.editingId ? "Editar Bodega" : "Nueva Bodega";
  const editorIcon = page.editingId ? "edit" : "add_box";

  const listContent = (
    <>
      {!page.loading && (
        <div className="pg-kpis">
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">warehouse</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Total Bodegas</p>
              <p className="pg-kpi-value">{page.totals.total}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--primary">
              <span className="material-symbols-outlined">check_circle</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Activas</p>
              <p className="pg-kpi-value">{page.totals.active}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--error">
              <span className="material-symbols-outlined">block</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Inactivas</p>
              <p className="pg-kpi-value">{page.totals.inactive}</p>
            </div>
          </div>
          <div className="pg-kpi pg-kpi--h">
            <div className="pg-kpi-icon pg-kpi-icon--secondary">
              <span className="material-symbols-outlined">store</span>
            </div>
            <div className="pg-kpi-bottom">
              <p className="pg-kpi-label">Sucursales</p>
              <p className="pg-kpi-value">{page.totals.branches}</p>
            </div>
          </div>
        </div>
      )}
      <WarehouseListadoTab
        warehouses={page.items}
        loading={page.loading}
        toggling={false}
        canUpdate={page.canUpdate}
        canDelete={page.canDelete}
        branchName={page.branchName}
        onEdit={async (row) => {
          await handleOpenEdit(row.id);
        }}
        onToggle={async (row) => {
          await page.toggleStatus(row);
          message.info(
            row.isActive
              ? t("warehouses.toggle.success.disabled", "Bodega desactivada.")
              : t("warehouses.toggle.success.activated", "Bodega activada."),
          );
        }}
      />
    </>
  );

  return (
    <ErpPageTemplate
      kicker={t("warehouses.kicker", "Inventario")}
      title={t("warehouses.title", "Gestión de Bodegas")}
      action={
        page.canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={handleOpenCreate}
          >
            <span className="material-symbols-outlined">add</span>
            {t("warehouses.new", "Nueva Bodega")}
          </ZHBtn>
        ) : null
      }
    >
      {page.error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix", "Error:")}
          detail={page.error}
        />
      )}

      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={editorLabel}
        editorIcon={editorIcon}
        listContent={listContent}
        editorContent={
          page.panelOpen ? (
            <WarehouseFormTab
              editingId={page.editingId}
              editCode={page.editCode}
              saving={page.saving}
              saveError={page.saveError}
              branches={page.branches}
              register={page.form.register}
              control={page.form.control}
              errors={page.errors}
              onSave={() => void page.save()}
              onCancel={handleCancel}
            />
          ) : (
            <div className="cfg-tabs-empty">
              <span className="material-symbols-outlined cfg-empty-panel__icon">
                warehouse
              </span>
              <p className="cfg-empty-panel__title">
                Seleccione o cree una bodega
              </p>
              <p className="cfg-empty-panel__sub">
                Use el botón <strong>Nueva Bodega</strong> en la cabecera o
                seleccione una desde la lista para editar.
              </p>
            </div>
          )
        }
      />
    </ErpPageTemplate>
  );
}
