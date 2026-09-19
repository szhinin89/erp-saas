import { useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { ReportKpiCard } from "../../../components/ReportPageTemplate";
import { useCashMovementReasonsAdminPage } from "./useCashMovementReasonsAdminPage";
import { CashMovementReasonListTab } from "../components/CashMovementReasonListTab";
import { CashMovementReasonFormTab } from "../components/CashMovementReasonFormTab";
import type { CashMovementReasonDto } from "../api/cajaService";

import "../../../styles/shared/items-catalog.css";
import "./CashMovementReasonsAdminPage.css";

/**
 * TREASURY-CASH-MOVEMENT-REASONS-ADMIN-03 — pantalla de administración del catálogo
 * CashMovementReason (/treasury/cash/movement-reasons). Réplica del patrón obligatorio
 * Lista → Editor (`ConfigTabsLayout`) que ya usan Bodegas/Motivos de ajuste — no se inventa un
 * layout de configuración nuevo. Usa exclusivamente la API existente de
 * TREASURY-CASH-MANUAL-MOVEMENTS-01 (cajaService), sin lógica ni endpoint duplicados.
 */
export function CashMovementReasonsAdminPage() {
  const page = useCashMovementReasonsAdminPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  if (!page.canView) {
    return <NoAccessPage title="Motivos de movimientos" />;
  }

  const handleOpenCreate = () => {
    page.openCreate();
    setActiveTab("editor");
  };

  const handleOpenEdit = (row: CashMovementReasonDto) => {
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
            label="Total motivos"
            value={String(page.totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label="Motivos activos"
            value={String(page.totals.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label="Motivos inactivos"
            value={String(page.totals.inactive)}
          />
        </div>
      )}
      <CashMovementReasonListTab
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
      kicker="Tesorería"
      title="Motivos de movimientos"
      subtitle="Catálogo de motivos disponibles al registrar un movimiento manual de efectivo en Caja."
      action={
        page.canManage ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={handleOpenCreate}>
            <span className="material-symbols-outlined">add</span>
            Nuevo motivo
          </ZHBtn>
        ) : null
      }
    >
      {page.error && (
        <ZHPageNotice variant="error" message="Error:" detail={page.error} />
      )}

      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={page.editingId ? "Editar motivo" : "Registrar motivo"}
        editorIcon={page.editingId ? "edit" : "add_box"}
        listContent={listContent}
        editorContent={
          page.panelOpen ? (
            <CashMovementReasonFormTab
              editingId={page.editingId}
              saving={page.saving}
              saveError={page.saveError}
              register={page.form.register}
              errors={page.errors}
              onSave={() => void page.save()}
              onCancel={handleCancel}
            />
          ) : (
            <div className="cfg-tabs-empty">
              <span className="material-symbols-outlined cfg-empty-panel__icon">
                list_alt
              </span>
              <p className="cfg-empty-panel__title">Seleccione o cree un motivo</p>
              <p className="cfg-empty-panel__sub">
                Use el botón Nuevo motivo en la cabecera o seleccione uno desde la lista para
                editar.
              </p>
            </div>
          )
        }
      />
    </ErpPageTemplate>
  );
}
