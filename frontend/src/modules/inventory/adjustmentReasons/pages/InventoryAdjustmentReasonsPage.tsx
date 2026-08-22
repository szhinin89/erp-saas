import { useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { NoAccessPage } from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../../components/shared/ConfigTabsLayout";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { useInventoryAdjustmentReasonsPage } from "./useInventoryAdjustmentReasonsPage";
import { AdjustmentReasonListTab } from "../components/AdjustmentReasonListTab";
import { AdjustmentReasonFormTab } from "../components/AdjustmentReasonFormTab";
import type { InventoryAdjustmentReasonDto } from "../types";

import "../../../../styles/shared/items-catalog.css";
import "./AdjustmentReasonsPage.css";

/**
 * INVENTORY-ADJUSTMENTS-03 — Pantalla 3: catálogo de motivos de ajuste.
 * Réplica del patrón obligatorio Lista → Editor (`ConfigTabsLayout`) que ya usa Bodegas: mismo
 * armazón de página, KPIs, tabs y editor a ancho completo — no se inventa un layout de
 * configuración nuevo.
 */
export function InventoryAdjustmentReasonsPage() {
  const { t } = useI18n();
  const page = useInventoryAdjustmentReasonsPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  if (!page.canView) {
    return (
      <NoAccessPage
        title={t("inventory.adjustmentReasons.title", "Motivos de ajuste")}
      />
    );
  }

  const handleOpenCreate = () => {
    page.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = (row: InventoryAdjustmentReasonDto) => {
    page.openEdit(row);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    page.closePanel();
    setActiveTab("list");
  };

  const listContent = (
    <>
      {!page.loading && (
        <div className="pg-kpis">
          <ReportKpiCard
            layout="horizontal"
            icon="list_alt"
            tone="primary"
            label={t("inventory.adjustmentReasons.kpi.total", "Total motivos")}
            value={String(page.totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label={t("inventory.adjustmentReasons.kpi.active", "Motivos activos")}
            value={String(page.totals.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label={t(
              "inventory.adjustmentReasons.kpi.inactive",
              "Motivos inactivos",
            )}
            value={String(page.totals.inactive)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="edit_note"
            tone="secondary"
            label={t(
              "inventory.adjustmentReasons.kpi.requiringNotes",
              "Exigen observación",
            )}
            value={String(page.totals.requiringNotes)}
          />
        </div>
      )}
      <AdjustmentReasonListTab
        reasons={page.items}
        loading={page.loading}
        toggling={page.toggling}
        canManage={page.canManage}
        onEdit={handleOpenEdit}
        onToggle={page.toggleStatus}
      />
    </>
  );

  return (
    <ErpPageTemplate
      kicker={t("inventory.adjustments.kicker", "Inventario")}
      title={t("inventory.adjustmentReasons.title", "Motivos de ajuste")}
      subtitle={t(
        "inventory.adjustmentReasons.description",
        "Catálogo de motivos disponibles al registrar un ajuste de inventario.",
      )}
      action={
        page.canManage ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            onClick={handleOpenCreate}
          >
            <span className="material-symbols-outlined">add</span>
            {t("inventory.adjustmentReasons.new", "Nuevo motivo")}
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
        // La pestaña del editor NO repite el texto del botón de cabecera ("Nuevo motivo"):
        // son dos controles distintos y duplicar el rótulo confunde al usuario (y a un lector
        // de pantalla, que anunciaría dos elementos idénticos con comportamientos distintos).
        editorLabel={
          page.editingId
            ? t("inventory.adjustmentReasons.editor.edit", "Editar motivo")
            : t("inventory.adjustmentReasons.editor.new", "Registrar motivo")
        }
        editorIcon={page.editingId ? "edit" : "add_box"}
        listContent={listContent}
        editorContent={
          page.panelOpen ? (
            <AdjustmentReasonFormTab
              editingId={page.editingId}
              saving={page.saving}
              saveError={page.saveError}
              register={page.form.register}
              control={page.form.control}
              errors={page.errors}
              onSave={() => void page.save()}
              onCancel={handleCancel}
            />
          ) : (
            <div className="cfg-tabs-empty">
              <span className="material-symbols-outlined cfg-empty-panel__icon">
                list_alt
              </span>
              <p className="cfg-empty-panel__title">
                {t(
                  "inventory.adjustmentReasons.editor.emptyTitle",
                  "Seleccione o cree un motivo",
                )}
              </p>
              <p className="cfg-empty-panel__sub">
                {t(
                  "inventory.adjustmentReasons.editor.emptySub",
                  "Use el botón Nuevo motivo en la cabecera o seleccione uno desde la lista para editar.",
                )}
              </p>
            </div>
          )
        }
      />
    </ErpPageTemplate>
  );
}
