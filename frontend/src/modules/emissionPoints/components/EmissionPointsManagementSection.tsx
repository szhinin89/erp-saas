import { useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { useEmissionPointsPage } from "../hooks/useEmissionPointsPage";
import { EmissionPointsListSection } from "./EmissionPointsListSection";
import { EmissionPointsFormPanel } from "./EmissionPointsFormPanel";
import type { EmissionPointListItemDto } from "../api/emissionPointsService";
import "../../../styles/shared/items-catalog.css";

export function EmissionPointsManagementSection() {
  const ctx = useEmissionPointsPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  const handleOpenCreate = async () => {
    await ctx.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = async (item: EmissionPointListItemDto) => {
    await ctx.openEdit(item);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    ctx.closePanel();
    setActiveTab("list");
  };

  if (!ctx.canView) return <NoAccessPage title="Puntos de Emisión" />;

  const editorLabel = ctx.editingId ? "Editar" : "Nuevo Punto";
  const editorIcon = ctx.editingId ? "edit" : "add_box";

  return (
    <ConfigTabsLayout
      activeTab={activeTab}
      onTabChange={setActiveTab}
      editorLabel={editorLabel}
      editorIcon={editorIcon}
      error={
        ctx.error ? (
          <ZHPageNotice variant="error" message="Error:" detail={ctx.error} />
        ) : undefined
      }
      listContent={
        <EmissionPointsListSection
          loading={ctx.loading}
          items={ctx.items}
          totals={ctx.totals}
          search={ctx.search}
          setSearch={ctx.setSearch}
          filtered={ctx.filtered}
          canUpdate={ctx.canUpdate}
          canDelete={ctx.canDelete}
          canCreate={ctx.canCreate}
          selectedId={ctx.selectedId}
          openCreate={handleOpenCreate}
          openEdit={handleOpenEdit}
          toggleDisable={ctx.toggleDisable}
          fetchList={ctx.fetchList}
        />
      }
      editorContent={
        ctx.panelOpen ? (
          <EmissionPointsFormPanel
            editingId={ctx.editingId}
            editingCode={ctx.editingCode}
            editingName={ctx.editingName}
            saving={ctx.saving}
            saveError={ctx.saveError}
            establishments={ctx.establishments}
            loadingEstablishments={ctx.loadingEstablishments}
            register={ctx.register}
            control={ctx.control}
            errors={ctx.errors}
            closePanel={handleCancel}
            save={ctx.save}
          />
        ) : (
          <div className="cfg-tabs-empty">
            <span className="material-symbols-outlined cfg-empty-panel__icon">
              point_of_sale
            </span>
            <p className="cfg-empty-panel__title">
              Seleccione o cree un punto de emisión
            </p>
            <p className="cfg-empty-panel__sub">
              Use el botón <strong>Nuevo Punto</strong> en la pestaña Lista, o
              seleccione uno para editar.
            </p>
          </div>
        )
      }
    />
  );
}
