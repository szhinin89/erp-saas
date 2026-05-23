import type { ReactNode } from 'react';
import { useI18n } from '../../i18n/i18n';
import { SortableTreeBranch } from './TreeNode';
import type { EditorMenuItem } from './menuBuilderTypes';
import { ROOT_PARENT, type ParentRef } from './treeOps';

type Props = {
  crmUi: boolean;
  panelTitle?: string;
  crmToolbar?: ReactNode;
  crmMasterStack?: ReactNode;
  crmMasterFooter?: ReactNode;
  treeEmpty: boolean;
  treeForRender: EditorMenuItem[];
  activeNodeIds?: Set<string>;
  onToggleNodeActive?: (uid: string, checked: boolean) => void;
  expandedIds: Set<string>;
  onToggleExpand: (uid: string) => void;
  forceExpandedIds: Set<string>;
  searchNeedle: string;
  onMoveUp: (uid: string) => void;
  onMoveDown: (uid: string) => void;
  onPatch: (uid: string, patch: Partial<EditorMenuItem>) => void;
  onIndent: (uid: string) => void;
  onOutdent: (uid: string) => void;
  onRemove: (uid: string) => void;
  onAddRootFolder: () => void;
  onAddRootForm?: () => void;
  onAddChildFolder: (uid: ParentRef) => void;
  onAddChildForm?: (uid: ParentRef) => void;
};

export function MenuBuilderCanvas({
  crmUi,
  panelTitle,
  crmToolbar,
  crmMasterStack,
  crmMasterFooter,
  treeEmpty,
  treeForRender,
  activeNodeIds,
  onToggleNodeActive,
  expandedIds,
  onToggleExpand,
  forceExpandedIds,
  searchNeedle,
  onMoveUp,
  onMoveDown,
  onPatch,
  onIndent,
  onOutdent,
  onRemove,
  onAddRootFolder,
  onAddRootForm,
  onAddChildFolder,
  onAddChildForm,
}: Props) {
  const { t } = useI18n();

  return (
    <section className="menu-builder-panel menu-builder-panel--canvas">
      <header className="menu-builder-panel__head">
        {crmUi ? (
          <div className="menu-builder-panel__crmTitleRow">
            <h4 className="menu-builder-panel__title">{panelTitle ?? t('platform.menuBuilder.canvasTitle')}</h4>
            {crmToolbar}
          </div>
        ) : (
          <h4 className="menu-builder-panel__title">{panelTitle ?? t('platform.menuBuilder.canvasTitle')}</h4>
        )}
        {crmUi && crmMasterStack ? <div className="menu-builder-panel__crmStack">{crmMasterStack}</div> : null}
        <div className="menu-builder-panel__headRow">
          <p className="menu-builder-panel__hint" title={t('platform.menuBuilder.canvasHint')}>
            {crmUi
              ? 'Reordena arrastrando o con las flechas; deshacer/rehacer solo afecta esta sesión.'
              : t('platform.menuBuilder.canvasHintShort')}
          </p>
          <div className="menu-builder-panel__headActions">
            <button
              type="button"
              className="zh-btn zh-btn--ghost zh-btn--sm"
              onClick={onAddRootFolder}
              aria-label="Agregar carpeta en la raíz del árbol"
            >
              {crmUi ? '📁 Carpeta raíz' : t('platform.menuBuilder.addRootFolder')}
            </button>
            {crmUi && onAddRootForm ? (
              <button
                type="button"
                className="zh-btn zh-btn--ghost zh-btn--sm"
                onClick={onAddRootForm}
                aria-label="Agregar formulario en la raíz del árbol"
              >
                📄 Formulario raíz
              </button>
            ) : null}
          </div>
        </div>
      </header>
      <div className="menu-builder-panel__body menu-builder-canvas-inner">
        <div className="menu-builder-canvas-branch-wrap">
          {treeEmpty ? (
            <div className="menu-builder-canvas-empty-float">
              <span className="menu-builder-canvas-empty__icon" aria-hidden>
                ⎘
              </span>
              {crmUi
                ? 'Arrastra formularios desde la columna derecha o añade carpeta/formulario raíz.'
                : t('platform.menuBuilder.canvasEmpty')}
            </div>
          ) : null}
          <SortableTreeBranch
            parentUid={ROOT_PARENT}
            nodes={treeForRender}
            depth={0}
            crmLayout={crmUi}
            activeIds={activeNodeIds}
            onToggleActive={onToggleNodeActive}
            expandedIds={expandedIds}
            onToggleExpand={onToggleExpand}
            forceExpandedIds={forceExpandedIds}
            searchNeedle={searchNeedle}
            onMoveUp={onMoveUp}
            onMoveDown={onMoveDown}
            onPatch={onPatch}
            onIndent={onIndent}
            onOutdent={onOutdent}
            onRemove={onRemove}
            onAddChildFolder={onAddChildFolder}
            onAddChildForm={onAddChildForm}
          />
        </div>
        {crmUi && crmMasterFooter ? <div className="menu-builder-panel__crmFooter">{crmMasterFooter}</div> : null}
      </div>
    </section>
  );
}
