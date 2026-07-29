import { useState } from "react";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { useEstablishmentsPage } from "../hooks/useEstablishmentsPage";
import { EstablishmentsListSection } from "./EstablishmentsListSection";
import { EstablishmentsFormPanel } from "./EstablishmentsFormPanel";
import type { EstablishmentListItemDto } from "../api/establishmentService";
import "../../../styles/shared/items-catalog.css";

export function EstablishmentsManagementSection() {
  const page = useEstablishmentsPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  const handleOpenCreate = async () => {
    await page.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = async (item: EstablishmentListItemDto) => {
    await page.openEdit(item);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    page.closePanel();
    setActiveTab("list");
  };

  const editorLabel = page.editingId ? "Editar" : "Nuevo Establecimiento";
  const editorIcon = page.editingId ? "edit" : "add_box";

  return (
    <ConfigTabsLayout
      activeTab={activeTab}
      onTabChange={setActiveTab}
      editorLabel={editorLabel}
      editorIcon={editorIcon}
      error={
        page.error ? (
          <ZHPageNotice variant="error" message={page.error} />
        ) : undefined
      }
      listContent={
        <EstablishmentsListSection
          loading={page.loading}
          items={page.items}
          filtered={page.filtered}
          totals={page.totals}
          search={page.search}
          setSearch={page.setSearch}
          activeStatus={page.activeStatus}
          setActiveStatus={page.setActiveStatus}
          canCreate={page.canCreate}
          canUpdate={page.canUpdate}
          canDisable={page.canDisable}
          selectedId={page.selectedId}
          openCreate={handleOpenCreate}
          openEdit={handleOpenEdit}
          toggleDisable={page.toggleDisable}
          fetchList={page.fetchList}
        />
      }
      editorContent={
        page.panelOpen ? (
          <EstablishmentsFormPanel
            editingId={page.editingId}
            editingCode={page.editingCode}
            editingName={page.editingName}
            saving={page.saving}
            saveError={page.saveError}
            branches={page.branches}
            loadingBranches={page.loadingBranches}
            register={page.register}
            control={page.control}
            errors={page.errors}
            closePanel={handleCancel}
            save={page.save}
          />
        ) : (
          <div className="cfg-tabs-empty">
            <span className="material-symbols-outlined cfg-empty-panel__icon">
              receipt_long
            </span>
            <p className="cfg-empty-panel__title">
              Seleccione o cree un establecimiento
            </p>
            <p className="cfg-empty-panel__sub">
              Use el botón <strong>Nuevo Establecimiento</strong> en la pestaña
              Lista, o seleccione uno para editar.
            </p>
          </div>
        )
      }
    />
  );
}
