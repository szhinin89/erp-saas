import { DndContext, DragOverlay, closestCorners } from '@dnd-kit/core';
import { ZHPromptModal } from '../zh/ZHPromptModal';
import { MenuBuilderCanvas } from './MenuBuilderCanvas';
import { MenuBuilderFormPreviewModal } from './MenuBuilderFormPreviewModal';
import { MenuBuilderLibraryPanel } from './MenuBuilderLibraryPanel';
import { MenuBuilderPreviewPanel } from './MenuBuilderPreviewPanel';
import { MenuBuilderWorkspaceToolbar } from './MenuBuilderWorkspaceToolbar';
import type { MenuBuilderProps } from './menuBuilderComponentTypes';
import { ROOT_PARENT } from './treeOps';
import { useMenuBuilderController } from './useMenuBuilderController';
import './menu-builder.css';

export type { MenuBuilderViewMode } from './menuBuilderComponentTypes';

export function MenuBuilder({
  catalogArbol,
  tree,
  onTreeChange,
  viewMode,
  onViewModeChange,
  previewLayout,
  onPreviewLayoutChange,
  onBuilderMessage,
  workspaceVariant = 'default',
  hideWorkspaceToolbar = false,
  panelTitles,
  crmToolbar,
  crmMasterStack,
  crmLibraryStack,
  crmMasterFooter,
  crmPreviewExtras,
  crmPreviewExtrasBottom,
  previewControls,
  hideCrmPreview = false,
  previewItemsOverride = null,
  activeNodeIds,
  onToggleNodeActive,
  treeSearchQuery = '',
}: MenuBuilderProps) {
  const ctrl = useMenuBuilderController({
    catalogArbol,
    tree,
    onTreeChange,
    onBuilderMessage,
    workspaceVariant,
    previewItemsOverride,
    activeNodeIds,
    treeSearchQuery,
  });

  const showEditor = viewMode === 'editor' || viewMode === 'split';
  const showPreview = viewMode === 'preview' || viewMode === 'split';
  const showPreviewPanel = showPreview && !(ctrl.crmUi && hideCrmPreview);

  const workspaceMod =
    viewMode === 'split'
      ? 'menu-builder-workspace--split'
      : viewMode === 'editor'
        ? 'menu-builder-workspace--editor'
        : 'menu-builder-workspace--preview';
  const crmMod =
    ctrl.crmUi && viewMode === 'split'
      ? hideCrmPreview
        ? ' menu-builder-workspace--crm-2col'
        : ' menu-builder-workspace--crm'
      : '';

  return (
    <DndContext
      sensors={ctrl.sensors}
      collisionDetection={closestCorners}
      onDragStart={ctrl.onDragStart}
      onDragEnd={ctrl.onDragEnd}
      onDragCancel={ctrl.onDragCancel}
    >
      <div className="menu-builder-root">
        {!hideWorkspaceToolbar ? (
          <MenuBuilderWorkspaceToolbar
            viewMode={viewMode}
            onViewModeChange={onViewModeChange}
            previewLayout={previewLayout}
            onPreviewLayoutChange={onPreviewLayoutChange}
            showPreview={showPreview}
          />
        ) : null}

        <div className={`menu-builder-workspace ${workspaceMod}${crmMod}`}>
          {showEditor ? (
            <>
              <MenuBuilderLibraryPanel
                crmUi={ctrl.crmUi}
                panelTitle={panelTitles?.library}
                crmLibraryStack={crmLibraryStack}
                availableLib={ctrl.availableLib}
                onPreviewForm={ctrl.crmUi ? ctrl.setPreviewForm : undefined}
              />

              <MenuBuilderCanvas
                crmUi={ctrl.crmUi}
                panelTitle={panelTitles?.canvas}
                crmToolbar={crmToolbar}
                crmMasterStack={crmMasterStack}
                crmMasterFooter={crmMasterFooter}
                treeEmpty={ctrl.treeEmpty}
                treeForRender={ctrl.treeForRender}
                activeNodeIds={activeNodeIds}
                onToggleNodeActive={onToggleNodeActive}
                expandedIds={ctrl.expandedIds}
                onToggleExpand={ctrl.toggleExpand}
                forceExpandedIds={ctrl.forceExpandedIds}
                searchNeedle={ctrl.searchNeedle}
                onMoveUp={ctrl.treeOps.moveUp}
                onMoveDown={ctrl.treeOps.moveDown}
                onPatch={ctrl.treeOps.patch}
                onIndent={ctrl.treeOps.indent}
                onOutdent={ctrl.treeOps.outdent}
                onRemove={ctrl.treeOps.remove}
                onAddRootFolder={() => ctrl.addChildFolder(ROOT_PARENT)}
                onAddRootForm={ctrl.crmUi ? () => ctrl.addChildForm(ROOT_PARENT) : undefined}
                onAddChildFolder={ctrl.addChildFolder}
                onAddChildForm={ctrl.crmUi ? ctrl.addChildForm : undefined}
              />
            </>
          ) : null}

          {showPreviewPanel ? (
            <MenuBuilderPreviewPanel
              crmUi={ctrl.crmUi}
              panelTitle={panelTitles?.preview}
              previewData={ctrl.previewData}
              previewLayout={previewLayout}
              previewControls={previewControls}
              crmPreviewExtras={crmPreviewExtras}
              crmPreviewExtrasBottom={crmPreviewExtrasBottom}
            />
          ) : null}
        </div>

        {ctrl.previewForm ? (
          <MenuBuilderFormPreviewModal previewForm={ctrl.previewForm} onClose={() => ctrl.setPreviewForm(null)} />
        ) : null}

        {ctrl.promptRequest ? (
          <ZHPromptModal
            title={ctrl.promptRequest.title}
            label={ctrl.promptRequest.label}
            initialValue={ctrl.promptRequest.defaultValue}
            placeholder={ctrl.promptRequest.placeholder}
            confirmLabel="Aceptar"
            cancelLabel="Cancelar"
            onCancel={() => ctrl.setPromptRequest(null)}
            onConfirm={ctrl.confirmCreateNode}
          />
        ) : null}
      </div>

      <DragOverlay dropAnimation={null}>
        {ctrl.activeId ? <div className="menu-builder-drag-overlay">{ctrl.overlayLabel || '…'}</div> : null}
      </DragOverlay>
    </DndContext>
  );
}
