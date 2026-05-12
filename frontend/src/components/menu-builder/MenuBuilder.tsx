import { useCallback, useMemo, useState } from 'react';
import {
  DndContext,
  DragOverlay,
  PointerSensor,
  closestCorners,
  useDraggable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import { useI18n } from '../../i18n/i18n';
import type { FuncionalidadArbolDto } from '../../services/superAdminService';
import {
  buildFuncionalidadMaps,
  createFolderEditorItem,
  editorToMenuItems,
  flattenFuncionalidades,
  funcionalidadToEditorItem,
  isEditorFolder,
  type EditorMenuItem,
} from './menuBuilderTypes';
import { MenuPreview, type MenuPreviewLayout } from './MenuPreview';
import { SortableTreeBranch } from './TreeNode';
import {
  ROOT_PARENT,
  deleteNode,
  findLocation,
  findNodeByUid,
  indentNode,
  insertChildAt,
  libDragId,
  moveNodeBeforeSibling,
  moveNodeToGap,
  outdentNode,
  parseGapId,
  parseLibDragId,
  parseSortableTreeId,
  reorderSibling,
  updateNodeFields,
  type ParentRef,
} from './treeOps';
import './menu-builder.css';

export type MenuBuilderViewMode = 'editor' | 'preview' | 'split';

type Props = {
  catalogArbol: FuncionalidadArbolDto[];
  tree: EditorMenuItem[];
  onTreeChange: (next: EditorMenuItem[]) => void;
  viewMode: MenuBuilderViewMode;
  onViewModeChange: (mode: MenuBuilderViewMode) => void;
  previewLayout: MenuPreviewLayout;
  onPreviewLayoutChange: (layout: MenuPreviewLayout) => void;
  /** Mensajes de validación DnD (p. ej. soltar en hoja). */
  onBuilderMessage?: (message: string) => void;
};

function LibraryRow({ node }: { node: FuncionalidadArbolDto }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: libDragId(node.id),
  });
  const style = transform ? { transform: CSS.Translate.toString(transform) } : undefined;
  const iconChar = (node.icono ?? '').trim().slice(0, 2) || '◇';
  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`menu-builder-lib-row ${isDragging ? 'is-dragging' : ''}`}
      {...listeners}
      {...attributes}
    >
      <span className="menu-builder-lib-icon" aria-hidden>
        {iconChar}
      </span>
      <div className="menu-builder-lib-text">
        <div className="menu-builder-lib-name">{node.nombre}</div>
        <div className="menu-builder-lib-perm" title={node.permiso}>
          {node.permiso}
        </div>
      </div>
    </div>
  );
}

export function MenuBuilder({
  catalogArbol,
  tree,
  onTreeChange,
  viewMode,
  onViewModeChange,
  previewLayout,
  onPreviewLayoutChange,
  onBuilderMessage,
}: Props) {
  const { t } = useI18n();
  const [activeId, setActiveId] = useState<string | null>(null);
  const flatLib = useMemo(() => flattenFuncionalidades(catalogArbol), [catalogArbol]);
  const { byId } = useMemo(() => buildFuncionalidadMaps(catalogArbol), [catalogArbol]);

  const parentAcceptsLibraryDrop = useCallback(
    (parentUid: ParentRef) => {
      if (parentUid === ROOT_PARENT) return true;
      const p = findNodeByUid(tree, parentUid as string);
      return !!p && isEditorFolder(p);
    },
    [tree],
  );

  const addChildFolder = useCallback(
    (parentUid: ParentRef) => {
      const folder = createFolderEditorItem();
      if (parentUid === ROOT_PARENT) {
        onTreeChange(insertChildAt(tree, ROOT_PARENT, tree.length, folder));
        return;
      }
      const p = findNodeByUid(tree, parentUid as string);
      if (!p || !isEditorFolder(p)) return;
      onTreeChange(insertChildAt(tree, parentUid, p.children.length, folder));
    },
    [onTreeChange, tree],
  );

  const sensors = useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: 6 },
    }),
  );

  const onDragStart = useCallback((e: DragStartEvent) => {
    setActiveId(String(e.active.id));
  }, []);

  const onDragEnd = useCallback(
    (event: DragEndEvent) => {
      setActiveId(null);
      const { active, over } = event;
      if (!over) return;
      const aid = String(active.id);
      const oid = String(over.id);

      const libId = parseLibDragId(aid);
      const gap = parseGapId(oid);
      const overTree = parseSortableTreeId(oid);

      if (libId) {
        const f = byId.get(libId);
        if (!f) return;
        const node = funcionalidadToEditorItem(f);
        if (gap) {
          if (!parentAcceptsLibraryDrop(gap.parent)) {
            onBuilderMessage?.(t('superadmin.menuBuilder.dropOnLeafBlocked'));
            return;
          }
          onTreeChange(insertChildAt(tree, gap.parent, gap.index, node));
          return;
        }
        if (overTree) {
          const loc = findLocation(tree, overTree);
          if (!loc) return;
          if (!parentAcceptsLibraryDrop(loc.parent)) {
            onBuilderMessage?.(t('superadmin.menuBuilder.dropOnLeafBlocked'));
            return;
          }
          onTreeChange(insertChildAt(tree, loc.parent, loc.index, node));
        }
        return;
      }

      const activeUid = parseSortableTreeId(aid);
      if (!activeUid) return;

      if (gap) {
        onTreeChange(moveNodeToGap(tree, activeUid, gap.parent, gap.index));
        return;
      }

      if (overTree && overTree !== activeUid) {
        const la = findLocation(tree, activeUid);
        const lo = findLocation(tree, overTree);
        if (la && lo && la.parent === lo.parent) {
          onTreeChange(reorderSibling(tree, la.parent, activeUid, overTree));
        } else {
          onTreeChange(moveNodeBeforeSibling(tree, activeUid, overTree));
        }
      }
    },
    [byId, onBuilderMessage, onTreeChange, parentAcceptsLibraryDrop, t, tree],
  );

  const onDragCancel = useCallback(() => setActiveId(null), []);

  const overlayLabel = useMemo(() => {
    if (!activeId) return '';
    const lid = parseLibDragId(activeId);
    if (lid) return byId.get(lid)?.nombre ?? '';
    const tid = parseSortableTreeId(activeId);
    if (tid) {
      const loc = findLocation(tree, tid);
      if (!loc) return '';
      const walk = (nodes: EditorMenuItem[]): EditorMenuItem | null => {
        for (const n of nodes) {
          if (n.uid === tid) return n;
          const c = walk(n.children);
          if (c) return c;
        }
        return null;
      };
      return walk(tree)?.nombre ?? '';
    }
    return '';
  }, [activeId, byId, tree]);

  const showEditor = viewMode === 'editor' || viewMode === 'split';
  const showPreview = viewMode === 'preview' || viewMode === 'split';

  const previewItems = useMemo(() => editorToMenuItems(tree), [tree]);

  const workspaceMod =
    viewMode === 'split' ? 'menu-builder-workspace--split' : viewMode === 'editor' ? 'menu-builder-workspace--editor' : 'menu-builder-workspace--preview';

  const treeEmpty = tree.length === 0;

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      onDragStart={onDragStart}
      onDragEnd={onDragEnd}
      onDragCancel={onDragCancel}
    >
      <div className="menu-builder-root">
        <div className="menu-builder-toolbar" role="toolbar" aria-label={t('superadmin.menuBuilder.visualMode')}>
          <span className="menu-builder-toolbar__label">{t('superadmin.menuBuilder.visualMode')}</span>
          <button
            type="button"
            className={`zh-btn zh-btn--sm ${viewMode === 'split' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
            onClick={() => onViewModeChange('split')}
          >
            {t('superadmin.menuBuilder.modeSplit')}
          </button>
          <button
            type="button"
            className={`zh-btn zh-btn--sm ${viewMode === 'editor' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
            onClick={() => onViewModeChange('editor')}
          >
            {t('superadmin.menuBuilder.modeEditor')}
          </button>
          <button
            type="button"
            className={`zh-btn zh-btn--sm ${viewMode === 'preview' ? 'zh-btn--primary' : 'zh-btn--ghost'}`}
            onClick={() => onViewModeChange('preview')}
          >
            {t('superadmin.menuBuilder.modePreview')}
          </button>
          {showPreview ? (
            <>
              <span className="menu-builder-toolbar__sep" aria-hidden />
              <span className="menu-builder-toolbar__label">{t('superadmin.menuBuilder.previewLayout')}</span>
              <button
                type="button"
                className={`zh-btn zh-btn--sm ${previewLayout === 'vertical' ? 'zh-btn--secondary' : 'zh-btn--ghost'}`}
                onClick={() => onPreviewLayoutChange('vertical')}
              >
                {t('superadmin.menuBuilder.layoutVertical')}
              </button>
              <button
                type="button"
                className={`zh-btn zh-btn--sm ${previewLayout === 'horizontal' ? 'zh-btn--secondary' : 'zh-btn--ghost'}`}
                onClick={() => onPreviewLayoutChange('horizontal')}
              >
                {t('superadmin.menuBuilder.layoutHorizontal')}
              </button>
            </>
          ) : null}
        </div>

        <div className={`menu-builder-workspace ${workspaceMod}`}>
          {showEditor ? (
            <>
              <aside className="menu-builder-panel menu-builder-panel--library">
                <header className="menu-builder-panel__head">
                  <h4 className="menu-builder-panel__title">{t('superadmin.menuBuilder.libraryTitle')}</h4>
                  <p className="menu-builder-panel__hint" title={t('superadmin.menuBuilder.libraryHint')}>
                    {t('superadmin.menuBuilder.libraryHintShort')}
                  </p>
                </header>
                <div className="menu-builder-panel__body">
                  {flatLib.length === 0 ? (
                    <p className="menu-preview-empty" style={{ border: 'none', minHeight: '6rem' }}>
                      {t('common.noData')}
                    </p>
                  ) : (
                    <div className="menu-builder-lib-stack">
                      {flatLib.map((n) => (
                        <LibraryRow key={n.id} node={n} />
                      ))}
                    </div>
                  )}
                </div>
              </aside>

              <section className="menu-builder-panel menu-builder-panel--canvas">
                <header className="menu-builder-panel__head">
                  <h4 className="menu-builder-panel__title">{t('superadmin.menuBuilder.canvasTitle')}</h4>
                  <div className="menu-builder-panel__headRow">
                    <p className="menu-builder-panel__hint" title={t('superadmin.menuBuilder.canvasHint')}>
                      {t('superadmin.menuBuilder.canvasHintShort')}
                    </p>
                    <button
                      type="button"
                      className="zh-btn zh-btn--ghost zh-btn--sm"
                      onClick={() => addChildFolder(ROOT_PARENT)}
                    >
                      {t('superadmin.menuBuilder.addRootFolder')}
                    </button>
                  </div>
                </header>
                <div className="menu-builder-panel__body menu-builder-canvas-inner">
                  {treeEmpty ? (
                    <div className="menu-builder-canvas-empty-float">
                      <span className="menu-builder-canvas-empty__icon" aria-hidden>
                        ⎘
                      </span>
                      {t('superadmin.menuBuilder.canvasEmpty')}
                    </div>
                  ) : null}
                  <div className="menu-builder-canvas-branch-wrap">
                    <SortableTreeBranch
                      parentUid={ROOT_PARENT}
                      nodes={tree}
                      depth={0}
                      onPatch={(uid, patch) => onTreeChange(updateNodeFields(tree, uid, patch))}
                      onIndent={(uid) => onTreeChange(indentNode(tree, uid))}
                      onOutdent={(uid) => onTreeChange(outdentNode(tree, uid))}
                      onRemove={(uid) => onTreeChange(deleteNode(tree, uid))}
                      onAddChildFolder={(uid) => addChildFolder(uid)}
                    />
                  </div>
                </div>
              </section>
            </>
          ) : null}

          {showPreview ? (
            <aside className="menu-builder-panel menu-builder-panel--preview">
              <header className="menu-builder-panel__head">
                <h4 className="menu-builder-panel__title">{t('superadmin.menuBuilder.livePreview')}</h4>
                <p className="menu-builder-panel__hint">{t('superadmin.menuBuilder.previewHintShort')}</p>
              </header>
              <div className="menu-builder-panel__body">
                <MenuPreview items={previewItems} layout={previewLayout} />
              </div>
            </aside>
          ) : null}
        </div>
      </div>

      <DragOverlay dropAnimation={null}>
        {activeId ? <div className="menu-builder-drag-overlay">{overlayLabel || '…'}</div> : null}
      </DragOverlay>
    </DndContext>
  );
}
