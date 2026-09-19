import { useState } from "react";
import { useI18n } from "../../../i18n/i18n";
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
 *
 * TREASURY-CASH-ARCHITECTURE-I18N-AUDIT-04 — el aviso informativo bajo el header sigue
 * exactamente el patrón ya existente en el proyecto para explicar una regla de pantalla
 * (`ZHPageNotice variant="info"`, ver `documentFlows.separationNotice` en
 * DocumentFlowPoliciesPage) — no se crea un componente "ArchitectureNotice" nuevo.
 */
export function CashMovementReasonsAdminPage() {
  const { t } = useI18n();
  const page = useCashMovementReasonsAdminPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  if (!page.canView) {
    return (
      <NoAccessPage title={t("caja.movementReasons.title", "Motivos de movimientos")} />
    );
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

  const architectureNotice = (
    <ZHPageNotice
      variant="info"
      message={t(
        "caja.movementReasons.architectureNotice",
        "Los motivos de movimientos clasifican los ingresos, egresos y retiros manuales de caja de esta empresa. El código y el tipo se definen al crear el motivo y luego no cambian, para mantener consistente el historial. Puede modificar el nombre, el orden y el estado; los movimientos ya registrados conservan siempre el motivo que usaron.",
      )}
    />
  );

  const listContent = (
    <>
      {!page.loading && (
        <div className="pg-kpis">
          <ReportKpiCard
            layout="horizontal"
            icon="list_alt"
            tone="primary"
            label={t("caja.movementReasons.kpi.total", "Total motivos")}
            value={String(page.totals.total)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="check_circle"
            tone="primary"
            label={t("caja.movementReasons.kpi.active", "Motivos activos")}
            value={String(page.totals.active)}
          />
          <ReportKpiCard
            layout="horizontal"
            icon="block"
            tone="error"
            label={t("caja.movementReasons.kpi.inactive", "Motivos inactivos")}
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
      kicker={t("caja.kicker", "Gestión de efectivo")}
      title={t("caja.movementReasons.title", "Motivos de movimientos")}
      subtitle={t(
        "caja.movementReasons.subtitle",
        "Catálogo de motivos disponibles al registrar un movimiento manual de efectivo en Caja.",
      )}
      action={
        page.canManage ? (
          <ZHBtn variant="primary" size="md" type="button" onClick={handleOpenCreate}>
            <span className="material-symbols-outlined">add</span>
            {t("caja.movementReasons.new", "Nuevo motivo")}
          </ZHBtn>
        ) : null
      }
    >
      {page.error && (
        <ZHPageNotice variant="error" message={t("common.errorPrefix", "Error:")} detail={page.error} />
      )}

      {architectureNotice}

      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={setActiveTab}
        editorLabel={
          page.editingId
            ? t("caja.movementReasons.editor.edit", "Editar motivo")
            : t("caja.movementReasons.editor.new", "Registrar motivo")
        }
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
              <p className="cfg-empty-panel__title">
                {t("caja.movementReasons.editor.emptyTitle", "Seleccione o cree un motivo")}
              </p>
              <p className="cfg-empty-panel__sub">
                {t(
                  "caja.movementReasons.editor.emptySub",
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
