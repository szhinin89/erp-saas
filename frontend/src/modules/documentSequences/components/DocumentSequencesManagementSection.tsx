import { useState } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ConfigTabsLayout } from "../../../components/shared/ConfigTabsLayout";
import { useDocumentSequencesPage } from "../hooks/useDocumentSequencesPage";
import { DocumentSequencesListSection } from "./DocumentSequencesListSection";
import { DocumentSequencesFormPanel } from "./DocumentSequencesFormPanel";
import type { DocumentSequenceRow } from "../hooks/useDocumentSequencesPage";
import "../../../styles/shared/items-catalog.css";

/**
 * DOCUMENT-SEQUENCES-CONFIG-UI-04 — auditoría de reutilización (obligatoria antes de UI nueva,
 * ver frontend/CLAUDE.md § Auditoría de reutilización):
 * 1. Plantillas revisadas: EmissionPointsManagementSection/EmissionPointsListSection/
 *    EmissionPointsFormPanel (mismo dominio, Settings), DocumentFlowPoliciesPage (config List→Editor).
 * 2. Reutilizado tal cual: ConfigTabsLayout (patrón Lista→Editor obligatorio para módulos de
 *    configuración), ZHDataTable, ZHField/ZHGrid/ZHBtn, ZhSelect/ZhTextInput/ZhNumberInput, Badge,
 *    message.confirm/success/error, applyServerErrors, formatApiRequestError, usePermissionsUi.
 * 3. Extendido: el "editor" no crea/edita una entidad CRUD sino que configura un valor puntual
 *    (nextNumber) sobre una fila de una matriz calculada (puntos de emisión x tipos de documento) —
 *    misma mecánica de paneles Lista/Editor, dato distinto.
 * 4. Nada nuevo se crea a nivel de componente Design System — cero componentes nuevos.
 * 5. Confirmado: no existe pantalla equivalente para secuencias documentales en el repo.
 */
export function DocumentSequencesManagementSection() {
  const ctx = useDocumentSequencesPage();
  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");

  const handleOpenConfigure = (row: DocumentSequenceRow) => {
    ctx.openConfigure(row);
    setActiveTab("editor");
  };

  const handleCancel = () => {
    ctx.closePanel();
    setActiveTab("list");
  };

  if (!ctx.canManage)
    return <NoAccessPage title="Secuencias documentales" />;

  return (
    <ConfigTabsLayout
      activeTab={activeTab}
      onTabChange={setActiveTab}
      editorLabel="Configurar"
      editorIcon="format_list_numbered"
      listContent={
        <DocumentSequencesListSection
          loading={ctx.loading}
          error={ctx.error}
          emissionPoints={ctx.emissionPoints}
          selectedEmissionPointId={ctx.selectedEmissionPointId}
          setSelectedEmissionPointId={ctx.setSelectedEmissionPointId}
          rows={ctx.rows}
          canManage={ctx.canManage}
          openConfigure={handleOpenConfigure}
          fetchAll={ctx.fetchAll}
        />
      }
      editorContent={
        ctx.panelOpen ? (
          <DocumentSequencesFormPanel
            editingRow={ctx.editingRow}
            selectedEmissionPoint={ctx.selectedEmissionPoint}
            saving={ctx.saving}
            saveError={ctx.saveError}
            register={ctx.register}
            errors={ctx.errors}
            closePanel={handleCancel}
            save={ctx.save}
          />
        ) : (
          <div className="cfg-tabs-empty">
            <span className="material-symbols-outlined cfg-empty-panel__icon">
              format_list_numbered
            </span>
            <p className="cfg-empty-panel__title">
              Seleccione un tipo de documento para configurar
            </p>
            <p className="cfg-empty-panel__sub">
              En la pestaña Lista, elija un punto de emisión y use{" "}
              <strong>Configurar</strong>/<strong>Editar</strong> sobre el tipo
              de documento deseado.
            </p>
          </div>
        )
      }
    />
  );
}
