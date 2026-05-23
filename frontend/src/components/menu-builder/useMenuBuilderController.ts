import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragStartEvent,
} from '@dnd-kit/core';
import { useI18n } from '../../i18n/i18n';
import type { FuncionalidadArbolDto } from '../../modules/platform/api/platformService';
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
import type { MenuBuilderPromptRequest } from './menuBuilderComponentTypes';
import {
  ROOT_PARENT,
  deleteNode,
  findLocation,
  findNodeByUid,
  indentNode,
  insertChildAt,
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

const EXPANDED_STORAGE_KEY = 'crmTreeExpandedState';

type Params = {
  catalogArbol: FuncionalidadArbolDto[];
  tree: EditorMenuItem[];
  onTreeChange: (next: EditorMenuItem[]) => void;
  onBuilderMessage?: (message: string) => void;
  workspaceVariant?: 'default' | 'crm';
  previewItemsOverride?: MenuItem[] | null;
  activeNodeIds?: Set<string>;
  treeSearchQuery?: string;
};

export function useMenuBuilderController({
  catalogArbol,
  tree,
  onTreeChange,
  onBuilderMessage,
  workspaceVariant = 'default',
  previewItemsOverride = null,
  activeNodeIds,
  treeSearchQuery = '',
}: Params) {
  const { t } = useI18n();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [previewForm, setPreviewForm] = useState<FuncionalidadArbolDto | null>(null);
  const [promptRequest, setPromptRequest] = useState<MenuBuilderPromptRequest | null>(null);
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
      const node =
        promptRequest.kind === 'folder' ? createFolderEditorItem(name) : createFormEditorItem(name);

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
            onBuilderMessage?.(t('platform.menuBuilder.dropOnLeafBlocked'));
            return;
          }
          onTreeChange(insertChildAt(tree, gap.parent, gap.index, node));
          return;
        }
        if (overTree) {
          const loc = findLocation(tree, overTree);
          if (!loc) return;
          if (!parentAcceptsLibraryDrop(loc.parent)) {
            onBuilderMessage?.(t('platform.menuBuilder.dropOnLeafBlocked'));
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

  const toggleExpand = useCallback((uid: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(uid)) next.delete(uid);
      else next.add(uid);
      return next;
    });
  }, []);

  return {
    crmUi,
    activeId,
    previewForm,
    setPreviewForm,
    promptRequest,
    setPromptRequest,
    availableLib,
    treeForRender,
    expandedIds,
    forceExpandedIds,
    searchNeedle,
    sensors,
    onDragStart,
    onDragEnd,
    onDragCancel,
    overlayLabel,
    previewData,
    treeEmpty: tree.length === 0,
    addChildFolder,
    addChildForm,
    confirmCreateNode,
    toggleExpand,
    treeOps: {
      moveUp: (uid: string) => onTreeChange(moveSiblingByDelta(tree, uid, -1)),
      moveDown: (uid: string) => onTreeChange(moveSiblingByDelta(tree, uid, 1)),
      patch: (uid: string, patch: Partial<EditorMenuItem>) => onTreeChange(updateNodeFields(tree, uid, patch)),
      indent: (uid: string) => onTreeChange(indentNode(tree, uid)),
      outdent: (uid: string) => onTreeChange(outdentNode(tree, uid)),
      remove: (uid: string) => onTreeChange(deleteNode(tree, uid)),
    },
  };
}
