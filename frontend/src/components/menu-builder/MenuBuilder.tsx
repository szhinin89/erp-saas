import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
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
import { ZHPromptModal } from '../zh/ZHPromptModal';
import {
  buildFuncionalidadMaps,
  createFolderEditorItem,
  createFormEditorItem,
  editorToMenuItems,
  flattenFuncionalidades,
  funcionalidadToEditorItem,
  isEditorFolder,
  type EditorMenuItem,
  type MenuItem,
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
  moveSiblingByDelta,
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
  /** Columnas: árbol | preview | biblioteca (vista configuración menú/plan). */
  workspaceVariant?: 'default' | 'crm';
  /** Oculta la barra de modos editor/preview (el padre fuerza split y radios de layout). */
  hideWorkspaceToolbar?: boolean;
  /** Títulos de paneles (vista CRM). */
  panelTitles?: { library?: string; canvas?: string; preview?: string };
  /** Controles extra encima del árbol (p. ej. deshacer/rehacer). */
  crmToolbar?: ReactNode;
  /** Bloque bajo el título del árbol (p. ej. pastillas de plan y búsqueda). */
  crmMasterStack?: ReactNode;
  /** Bloque bajo el título de biblioteca (filtros del catálogo). */
  crmLibraryStack?: ReactNode;
  /** Pie del panel árbol (acciones y ayuda). */
  crmMasterFooter?: ReactNode;
  /** Columna central: controles encima de la vista previa (toggle de layout). */
  crmPreviewExtras?: ReactNode;
  /** Columna central: contenido debajo de la vista previa (tarjeta de plan, simulación). */
  crmPreviewExtrasBottom?: ReactNode;
  /** Controls rendered inside the browser chrome bar of the preview simulation. */
  previewControls?: ReactNode;
  /** When true in CRM mode, hides the preview column so the caller can render MenuPreview externally. */
  hideCrmPreview?: boolean;
  /** Sustituye el menú simulado en la vista previa (p. ej. menú efectivo de otra empresa). */
  previewItemsOverride?: MenuItem[] | null;
  /** Activaciones por plan para la vista CRM (checkbox por nodo). */
  activeNodeIds?: Set<string>;
  /** Cambio de activación por nodo (uid del editor). */
  onToggleNodeActive?: (uid: string, checked: boolean) => void;
  treeSearchQuery?: string;
};

const EXPANDED_STORAGE_KEY = 'crmTreeExpandedState';
type PromptRequest = {
  kind: 'folder' | 'form';
  parentUid: ParentRef;
  title: string;
  label: string;
  defaultValue: string;
  placeholder?: string;
};

function LibraryRow({ node, dense, onPreview }: { node: FuncionalidadArbolDto; dense?: boolean; onPreview?: (node: FuncionalidadArbolDto) => void }) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: libDragId(node.id),
  });
  const style = transform ? { transform: CSS.Translate.toString(transform) } : undefined;
  const iconChar = (node.icon ?? '').trim().slice(0, 2) || '◇';
  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`menu-builder-lib-row ${isDragging ? 'is-dragging' : ''}${dense ? ' menu-builder-lib-row--crm' : ''}`}
      {...listeners}
      {...attributes}
    >
      <span className="menu-builder-lib-icon" aria-hidden>
        {iconChar}
      </span>
      <div className="menu-builder-lib-text">
        <div className="menu-builder-lib-name">{node.name}</div>
        <div className="menu-builder-lib-perm" title={node.permission}>
          {node.path?.trim() ? `${node.path} · ` : ''}
          {node.permission}
        </div>
      </div>
      {dense ? (
        <div className="menu-builder-lib-previewRow">
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--xs"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={(e) => {
              e.stopPropagation();
              onPreview?.(node);
            }}
            aria-label="Previsualizar formulario del catálogo"
          >
            👁 Previsualizar
          </button>
        </div>
      ) : null}
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
}: Props) {
  const { t } = useI18n();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [previewForm, setPreviewForm] = useState<FuncionalidadArbolDto | null>(null);
  const [promptRequest, setPromptRequest] = useState<PromptRequest | null>(null);
  const [expandedIds, setExpandedIds] = useState<Set<string>>(() => {
    if (typeof window === 'undefined') return new Set<string>();
    try {
      const raw = window.localStorage.getItem(EXPANDED_STORAGE_KEY);
      if (!raw) return new Set<string>();
      const parsed = JSON.parse(raw) as string[];
      return new Set(Array.isArray(parsed) ? parsed : []);
    } catch {
      return new Set<string>();
    }
  });
  const crmUi = workspaceVariant === 'crm';
  const flatLib = useMemo(() => flattenFuncionalidades(catalogArbol), [catalogArbol]);
  const availableLib = useMemo(() => {
    if (!crmUi) return flatLib;
    const existingLeaves = new Set<string>();
    const walk = (nodes: EditorMenuItem[]) => {
      for (const n of nodes) {
        if (!isEditorFolder(n)) {
          const perm = (n.permiso ?? '').trim();
          const ruta = (n.ruta ?? '').trim();
          if (perm) existingLeaves.add(`perm:${perm.toLowerCase()}`);
          if (ruta) existingLeaves.add(`ruta:${ruta.toLowerCase()}`);
        }
        walk(n.children);
      }
    };
    walk(tree);
    return flatLib.filter((n) => {
      const perm = (n.permission ?? '').trim().toLowerCase();
      const ruta = (n.path ?? '').trim().toLowerCase();
      if (perm && existingLeaves.has(`perm:${perm}`)) return false;
      if (ruta && existingLeaves.has(`ruta:${ruta}`)) return false;
      return true;
    });
  }, [crmUi, flatLib, tree]);
  const { byId } = useMemo(() => buildFuncionalidadMaps(catalogArbol), [catalogArbol]);
  const searchNeedle = treeSearchQuery.trim().toLowerCase();

  const forceExpandedIds = useMemo(() => {
    if (!searchNeedle) return new Set<string>();
    const out = new Set<string>();
    const walk = (nodes: EditorMenuItem[], ancestors: string[]) => {
      for (const n of nodes) {
        const hay =
          n.nombre.toLowerCase().includes(searchNeedle) ||
          (n.ruta ?? '').toLowerCase().includes(searchNeedle) ||
          (n.permiso ?? '').toLowerCase().includes(searchNeedle);
        const nextAncestors = [...ancestors, n.uid];
        if (hay) ancestors.forEach((a) => out.add(a));
        walk(n.children, nextAncestors);
      }
    };
    walk(tree, []);
    return out;
  }, [searchNeedle, tree]);

  const visibleIds = useMemo(() => {
    if (!searchNeedle) return null;
    const out = new Set<string>();
    const walk = (nodes: EditorMenuItem[]): boolean => {
      let any = false;
      for (const n of nodes) {
        const self =
          n.nombre.toLowerCase().includes(searchNeedle) ||
          (n.ruta ?? '').toLowerCase().includes(searchNeedle) ||
          (n.permiso ?? '').toLowerCase().includes(searchNeedle);
        const child = walk(n.children);
        if (self || child) {
          out.add(n.uid);
          any = true;
        }
      }
      return any;
    };
    walk(tree);
    return out;
  }, [searchNeedle, tree]);

  const treeForRender = useMemo(() => {
    if (!visibleIds) return tree;
    const prune = (nodes: EditorMenuItem[]): EditorMenuItem[] =>
      nodes
        .filter((n) => visibleIds.has(n.uid))
        .map((n) => ({ ...n, children: prune(n.children) }));
    return prune(tree);
  }, [tree, visibleIds]);

  useEffect(() => {
    if (!crmUi || typeof window === 'undefined') return;
    window.localStorage.setItem(EXPANDED_STORAGE_KEY, JSON.stringify(Array.from(expandedIds)));
  }, [crmUi, expandedIds]);

  useEffect(() => {
    if (!previewForm || typeof window === 'undefined') return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setPreviewForm(null);
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [previewForm]);

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
      const defaultName = 'Nueva carpeta';
      if (crmUi) {
        setPromptRequest({
          kind: 'folder',
          parentUid,
          title: 'Nueva carpeta',
          label: 'Nombre de carpeta',
          defaultValue: '',
          placeholder: defaultName,
        });
        return;
      }
      const folder = createFolderEditorItem(defaultName);
      if (parentUid === ROOT_PARENT) {
        onTreeChange(insertChildAt(tree, ROOT_PARENT, tree.length, folder));
        return;
      }
      const p = findNodeByUid(tree, parentUid as string);
      if (!p || !isEditorFolder(p)) return;
      onTreeChange(insertChildAt(tree, parentUid, p.children.length, folder));
    },
    [crmUi, onTreeChange, tree],
  );

  const addChildForm = useCallback(
    (parentUid: ParentRef) => {
      const defaultName = 'Nuevo formulario';
      if (crmUi) {
        setPromptRequest({
          kind: 'form',
          parentUid,
          title: 'Nuevo formulario',
          label: 'Nombre de formulario',
          defaultValue: '',
          placeholder: defaultName,
        });
        return;
      }
      const form = createFormEditorItem(defaultName);
      if (parentUid === ROOT_PARENT) {
        onTreeChange(insertChildAt(tree, ROOT_PARENT, tree.length, form));
        return;
      }
      const p = findNodeByUid(tree, parentUid as string);
      if (!p || !isEditorFolder(p)) return;
      onTreeChange(insertChildAt(tree, parentUid, p.children.length, form));
    },
    [crmUi, onTreeChange, tree],
  );

  const confirmCreateNode = useCallback(
    (name: string) => {
      if (!promptRequest) return;
      const parentUid = promptRequest.parentUid;
      const node = promptRequest.kind === 'folder'
        ? createFolderEditorItem(name)
        : createFormEditorItem(name);

      if (parentUid === ROOT_PARENT) {
        onTreeChange(insertChildAt(tree, ROOT_PARENT, tree.length, node));
        setPromptRequest(null);
        return;
      }

      const p = findNodeByUid(tree, parentUid as string);
      if (!p || !isEditorFolder(p)) {
        setPromptRequest(null);
        return;
      }

      onTreeChange(insertChildAt(tree, parentUid, p.children.length, node));
      setPromptRequest(null);
    },
    [onTreeChange, promptRequest, tree],
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
    if (lid) return byId.get(lid)?.name ?? '';
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
  const previewData = useMemo(() => {
    const base = previewItemsOverride ?? previewItems;
    if (!activeNodeIds || activeNodeIds.size === 0) return [];
    const filterTree = (nodes: MenuItem[]): MenuItem[] => {
      const out: MenuItem[] = [];
      for (const n of nodes) {
        const children = filterTree(n.children ?? []);
        const keep = activeNodeIds.has(n.id) || children.length > 0;
        if (!keep) continue;
        out.push({ ...n, children });
      }
      return out;
    };
    return filterTree(base);
  }, [activeNodeIds, previewItems, previewItemsOverride]);

  const showPreviewPanel = showPreview && !(crmUi && hideCrmPreview);

  const workspaceMod =
    viewMode === 'split' ? 'menu-builder-workspace--split' : viewMode === 'editor' ? 'menu-builder-workspace--editor' : 'menu-builder-workspace--preview';
  const crmMod = crmUi && viewMode === 'split'
    ? hideCrmPreview
      ? ' menu-builder-workspace--crm-2col'
      : ' menu-builder-workspace--crm'
    : '';

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
        {!hideWorkspaceToolbar ? (
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
        ) : null}

        <div className={`menu-builder-workspace ${workspaceMod}${crmMod}`}>
          {showEditor ? (
            <>
              <aside className="menu-builder-panel menu-builder-panel--library">
                <header className="menu-builder-panel__head">
                  <h4 className="menu-builder-panel__title">{panelTitles?.library ?? t('superadmin.menuBuilder.libraryTitle')}</h4>
                  <p className="menu-builder-panel__hint" title={t('superadmin.menuBuilder.libraryHint')}>
                    {crmUi ? 'Arrastra y suelta hacia el árbol maestro.' : t('superadmin.menuBuilder.libraryHintShort')}
                  </p>
                  {crmUi && crmLibraryStack ? <div className="menu-builder-panel__crmStack">{crmLibraryStack}</div> : null}
                </header>
                <div className="menu-builder-panel__body">
                  {availableLib.length === 0 ? (
                    <p className="menu-preview-empty menu-preview-empty--library">
                      {t('common.noData')}
                    </p>
                  ) : (
                    <div className={`menu-builder-lib-stack${crmUi ? ' menu-builder-lib-stack--crm' : ''}`}>
                      {availableLib.map((n) => (
                        <LibraryRow
                          key={n.id}
                          node={n}
                          dense={crmUi}
                          onPreview={
                            crmUi
                              ? (form) => {
                                  setPreviewForm(form);
                                }
                              : undefined
                          }
                        />
                      ))}
                    </div>
                  )}
                </div>
              </aside>

              <section className="menu-builder-panel menu-builder-panel--canvas">
                <header className="menu-builder-panel__head">
                  {crmUi ? (
                    <div className="menu-builder-panel__crmTitleRow">
                      <h4 className="menu-builder-panel__title">{panelTitles?.canvas ?? t('superadmin.menuBuilder.canvasTitle')}</h4>
                      {crmToolbar}
                    </div>
                  ) : (
                    <h4 className="menu-builder-panel__title">{panelTitles?.canvas ?? t('superadmin.menuBuilder.canvasTitle')}</h4>
                  )}
                  {crmUi && crmMasterStack ? <div className="menu-builder-panel__crmStack">{crmMasterStack}</div> : null}
                  <div className="menu-builder-panel__headRow">
                    <p className="menu-builder-panel__hint" title={t('superadmin.menuBuilder.canvasHint')}>
                      {crmUi
                        ? 'Reordena arrastrando o con las flechas; deshacer/rehacer solo afecta esta sesión.'
                        : t('superadmin.menuBuilder.canvasHintShort')}
                    </p>
                    <div className="menu-builder-panel__headActions">
                      <button
                        type="button"
                        className="zh-btn zh-btn--ghost zh-btn--sm"
                        onClick={() => addChildFolder(ROOT_PARENT)}
                        aria-label="Agregar carpeta en la raíz del árbol"
                      >
                        {crmUi ? '📁 Carpeta raíz' : t('superadmin.menuBuilder.addRootFolder')}
                      </button>
                      {crmUi ? (
                        <button
                          type="button"
                          className="zh-btn zh-btn--ghost zh-btn--sm"
                          onClick={() => addChildForm(ROOT_PARENT)}
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
                        {crmUi ? 'Arrastra formularios desde la columna derecha o añade carpeta/formulario raíz.' : t('superadmin.menuBuilder.canvasEmpty')}
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
                      onToggleExpand={(uid) => {
                        setExpandedIds((prev) => {
                          const next = new Set(prev);
                          if (next.has(uid)) next.delete(uid);
                          else next.add(uid);
                          return next;
                        });
                      }}
                      forceExpandedIds={forceExpandedIds}
                      searchNeedle={searchNeedle}
                      onMoveUp={(uid) => onTreeChange(moveSiblingByDelta(tree, uid, -1))}
                      onMoveDown={(uid) => onTreeChange(moveSiblingByDelta(tree, uid, 1))}
                      onPatch={(uid, patch) => onTreeChange(updateNodeFields(tree, uid, patch))}
                      onIndent={(uid) => onTreeChange(indentNode(tree, uid))}
                      onOutdent={(uid) => onTreeChange(outdentNode(tree, uid))}
                      onRemove={(uid) => onTreeChange(deleteNode(tree, uid))}
                      onAddChildFolder={(uid) => addChildFolder(uid)}
                      onAddChildForm={crmUi ? (uid) => addChildForm(uid) : undefined}
                    />
                  </div>
                  {crmUi && crmMasterFooter ? <div className="menu-builder-panel__crmFooter">{crmMasterFooter}</div> : null}
                </div>
              </section>
            </>
          ) : null}

          {showPreviewPanel ? (
            <aside className="menu-builder-panel menu-builder-panel--preview">
              {!crmUi ? (
                <header className="menu-builder-panel__head">
                  <h4 className="menu-builder-panel__title">{panelTitles?.preview ?? t('superadmin.menuBuilder.livePreview')}</h4>
                  <p className="menu-builder-panel__hint">{t('superadmin.menuBuilder.previewHintShort')}</p>
                </header>
              ) : null}
              <div className="menu-builder-panel__body">
                {crmUi && crmPreviewExtras ? <div className="menu-builder-panel__crmPreviewTop">{crmPreviewExtras}</div> : null}
                <MenuPreview items={previewData} layout={previewLayout} controls={previewControls} />
                {crmUi && crmPreviewExtrasBottom ? <div className="menu-builder-panel__crmPreviewBottom">{crmPreviewExtrasBottom}</div> : null}
              </div>
            </aside>
          ) : null}
        </div>

        {previewForm ? (
          <div
            className="zh-modal-overlay menu-builder-form-preview-backdrop"
            role="dialog"
            aria-modal="true"
            aria-label="Mockup de formulario"
            onClick={() => setPreviewForm(null)}
          >
            <div className="menu-builder-form-preview-card" onClick={(e) => e.stopPropagation()}>
              <div className="menu-builder-form-preview-head">
                <h4>Mockup de formulario</h4>
                <button type="button" className="zh-btn zh-btn--ghost zh-btn--xs" onClick={() => setPreviewForm(null)} aria-label="Cerrar mockup">
                  ✕
                </button>
              </div>
              <p className="menu-builder-form-preview-subtle">
                Vista previa visual de <strong>{previewForm.name}</strong>
              </p>
              <div className="menu-builder-form-preview-meta">
                <span>
                  <strong>Ruta:</strong> {previewForm.path?.trim() || '—'}
                </span>
                <span>
                  <strong>Permiso:</strong> {previewForm.permission?.trim() || '—'}
                </span>
              </div>
              <div className="menu-builder-form-preview-body">
                <div className="menu-builder-form-preview-field">
                  <label>Campo principal</label>
                  <input className="zh-input" value="" readOnly placeholder="Ejemplo..." />
                </div>
                <div className="menu-builder-form-preview-field">
                  <label>Descripción</label>
                  <textarea className="zh-input" value="" readOnly placeholder="Contenido de ejemplo..." />
                </div>
                <div className="menu-builder-form-preview-actions">
                  <button type="button" className="zh-btn zh-btn--primary zh-btn--sm" disabled>
                    Guardar
                  </button>
                  <button type="button" className="zh-btn zh-btn--ghost zh-btn--sm" disabled>
                    Cancelar
                  </button>
                </div>
              </div>
            </div>
          </div>
        ) : null}

        {promptRequest ? (
          <ZHPromptModal
            title={promptRequest.title}
            label={promptRequest.label}
            initialValue={promptRequest.defaultValue}
            placeholder={promptRequest.placeholder}
            confirmLabel="Aceptar"
            cancelLabel="Cancelar"
            onCancel={() => setPromptRequest(null)}
            onConfirm={confirmCreateNode}
          />
        ) : null}
      </div>

      <DragOverlay dropAnimation={null}>
        {activeId ? <div className="menu-builder-drag-overlay">{overlayLabel || '…'}</div> : null}
      </DragOverlay>
    </DndContext>
  );
}
