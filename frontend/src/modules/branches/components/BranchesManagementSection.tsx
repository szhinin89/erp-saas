import { useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { BranchFormPanel } from "./BranchFormPanel";
import { BranchesListSection } from "./BranchesListSection";
import { useBranchesPage } from "../hooks/useBranchesPage";
import "../pages/branches-page.css";
import "../../../styles/shared/items-catalog.css";

export function BranchesManagementSection() {
  const ctx = useBranchesPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  const handleOpenCreate = () => {
    ctx.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = async (id: string) => {
    await ctx.openEdit(id);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    ctx.closePanel();
    setActiveTab("list");
  };

  if (!ctx.canView) return <NoAccessPage title={ctx.t("branches.title")} />;

  const editorLabel = ctx.editingId ? "Editar" : "Nueva Sucursal";
  const editorIcon = ctx.editingId ? "edit" : "add_box";

  return (
    <ConfigTabsLayout
      activeTab={activeTab}
      onTabChange={setActiveTab}
      editorLabel={editorLabel}
      editorIcon={editorIcon}
      error={
        ctx.error ? (
          <ZHPageNotice
            variant="error"
            message={ctx.t("common.errorPrefix")}
            detail={ctx.error}
          />
        ) : undefined
      }
      listContent={
        <BranchesListSection
          {...ctx}
          openCreate={handleOpenCreate}
          openEdit={handleOpenEdit}
        />
      }
      editorContent={
        ctx.panelOpen ? (
          <BranchFormPanel {...ctx} closePanel={handleCancel} />
        ) : (
          <div className="cfg-tabs-empty">
            <span className="material-symbols-outlined cfg-empty-panel__icon">
              business
            </span>
            <p className="cfg-empty-panel__title">
              Seleccione o cree una sucursal
            </p>
            <p className="cfg-empty-panel__sub">
              Use el botón <strong>Nueva Sucursal</strong> en la pestaña Lista,
              o seleccione una para editar.
            </p>
          </div>
        )
      }
    />
  );
}
