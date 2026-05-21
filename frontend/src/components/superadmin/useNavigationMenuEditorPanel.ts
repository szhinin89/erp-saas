import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import {
  cloneMenu,
  collectItemLevels,
  findAncestorPathIncludingSelf,
  findItemLocation,
  findNavItemInMenu,
  findSiblingArray,
  moveInPlace,
} from '../../modules/superadmin/navigationMenuEditorModel';
import { useSuperAdminGate } from '../../hooks/useSuperAdminGate';
import { useI18n } from '../../i18n/i18n';
import {
  superAdminService,
  type AdminNavigationMenu,
  type AdminNavItemRow,
  type NavItemSiblingOrderLevel,
} from '../../modules/superadmin/api/superAdminService';
import { menuService } from '../../modules/superadmin/api/menuService';
import { formatApiError } from '../../modules/lib/formatApiError';
import { editorToMenuItems, sessionGroupsToEditorTree } from '../menu-builder/menuBuilderTypes';
import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import { adminNavigationToSessionMenu } from '../../modules/superadmin/adminNavigationToSessionMenu';
import { collectExpandableIds, expandKey } from './NavigationMenuTree';
import { parseNavRowKey } from './navigationMenuEditorUtils';

export function useNavigationMenuEditorPanel(splitWorkspace: boolean) {
  const { t } = useI18n();
  const { isSuperAdmin, hasSelectedSubscriber } = useSuperAdminGate();

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [menu, setMenu] = useState<AdminNavigationMenu | null>(null);
  const [expandedMap, setExpandedMap] = useState<Record<string, boolean>>({});
  const [expandedGroups, setExpandedGroups] = useState<Record<string, boolean>>({});
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [pinMap, setPinMap] = useState<Record<string, boolean>>({});
  const [createTarget, setCreateTarget] = useState<{ groupId: string; parentItemId: string | null } | null>(null);
  const [createDisplayLabel, setCreateDisplayLabel] = useState('');
  const [createRoutePath, setCreateRoutePath] = useState('/');
  const [createModuleKey, setCreateModuleKey] = useState('');
  const [createPermissionKey, setCreatePermissionKey] = useState('');
  const [creatingItem, setCreatingItem] = useState(false);
  const [editOpen, setEditOpen] = useState(false);
  const [editDisplayLabel, setEditDisplayLabel] = useState('');
  const [editRoutePath, setEditRoutePath] = useState('/');
  const [editModuleKey, setEditModuleKey] = useState('');
  const [editPermissionKey, setEditPermissionKey] = useState('');
  const [editFeatureId, setEditFeatureId] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deletingItem, setDeletingItem] = useState(false);
  const [previewLayout, setPreviewLayout] = useState<MenuPreviewLayout>('vertical');
  const menuRef = useRef<AdminNavigationMenu | null>(null);
  menuRef.current = menu;

  const load = useCallback(async () => {
    setError('');
    setSuccess(false);
    const data = await superAdminService.getNavigationMenu();
    setMenu(cloneMenu(data));
    setExpandedMap({});
    setExpandedGroups({});
    setSelectedKey(null);
  }, []);

  useEffect(() => {
    if (!isSuperAdmin || hasSelectedSubscriber) return;
    let cancelled = false;
    void (async () => {
      try {
        setLoading(true);
        await load();
      } catch (e) {
        if (!cancelled) setError(formatApiError(e));
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [isSuperAdmin, hasSelectedSubscriber, load]);

  const moveGroup = (index: number, delta: number) => {
    setMenu((prev) => {
      if (!prev) return prev;
      const next = cloneMenu(prev);
      moveInPlace(next.groups, index, delta);
      return next;
    });
  };

  const moveItem = (groupId: string, itemId: string, delta: number) => {
    setMenu((prev) => {
      if (!prev) return prev;
      const next = cloneMenu(prev);
      const g = next.groups.find((x) => x.id === groupId);
      if (!g) return prev;
      const arr = findSiblingArray(g.rootItems, itemId);
      if (!arr) return prev;
      const idx = arr.findIndex((i) => i.id === itemId);
      if (idx < 0) return prev;
      moveInPlace(arr, idx, delta);
      return next;
    });
  };

  const indentItem = (groupId: string, itemId: string) => {
    setMenu((prev) => {
      if (!prev) return prev;
      const next = cloneMenu(prev);
      const g = next.groups.find((x) => x.id === groupId);
      if (!g) return prev;
      const loc = findItemLocation(g.rootItems, itemId);
      if (!loc || loc.index === 0) return prev;
      const prevSibling = loc.siblings[loc.index - 1]!;
      const [node] = loc.siblings.splice(loc.index, 1);
      if (!prevSibling.children) prevSibling.children = [];
      prevSibling.children.push(node);
      return next;
    });
  };

  const outdentItem = (groupId: string, itemId: string) => {
    setMenu((prev) => {
      if (!prev) return prev;
      const next = cloneMenu(prev);
      const g = next.groups.find((x) => x.id === groupId);
      if (!g) return prev;
      const loc = findItemLocation(g.rootItems, itemId);
      if (!loc || !loc.parent) return prev;
      const [node] = loc.siblings.splice(loc.index, 1);
      const pLoc = findItemLocation(g.rootItems, loc.parent.id);
      if (!pLoc) return prev;
      pLoc.siblings.splice(pLoc.index + 1, 0, node);
      return next;
    });
  };

  const setExpandedForGroup = (groupId: string, items: AdminNavItemRow[], value: boolean) => {
    const ids: string[] = [];
    collectExpandableIds(items, ids);
    setExpandedMap((prev) => {
      const next = { ...prev };
      for (const id of ids) {
        next[expandKey(groupId, id)] = value;
      }
      return next;
    });
  };

  const togglePin = (itemId: string) => {
    setPinMap((prev) => ({ ...prev, [itemId]: !prev[itemId] }));
  };

  const openCreateNavItem = useCallback((groupId: string, parentItemId: string | null) => {
    setCreateDisplayLabel('');
    setCreateRoutePath('/');
    setCreateModuleKey('');
    setCreatePermissionKey('');
    setCreateTarget({ groupId, parentItemId });
    setError('');
    setExpandedGroups((prev) => ({ ...prev, [groupId]: true }));
    const m = menuRef.current;
    if (parentItemId && m) {
      const g = m.groups.find((x) => x.id === groupId);
      const chain = g ? findAncestorPathIncludingSelf(g.rootItems, parentItemId) : null;
      if (chain?.length) {
        setExpandedMap((emap) => {
          const next = { ...emap };
          for (const id of chain) {
            next[expandKey(groupId, id)] = true;
          }
          return next;
        });
      }
    }
  }, []);

  const closeCreateNavItem = () => {
    if (creatingItem) return;
    setCreateTarget(null);
  };

  const handleCreateNavItemSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!createTarget) return;
    const displayLabel = createDisplayLabel.trim();
    const routePath = createRoutePath.trim();
    if (!displayLabel) {
      setError(t('superadmin.navigationMenu.createItemErrorDisplayRequired'));
      return;
    }
    if (!routePath.startsWith('/')) {
      setError(t('superadmin.navigationMenu.createItemErrorRoute'));
      return;
    }
    setCreatingItem(true);
    setError('');
    try {
      await superAdminService.createNavigationMenuItem({
        groupId: createTarget.groupId,
        parentItemId: createTarget.parentItemId,
        routePath,
        displayLabel,
        moduleKey: createModuleKey.trim() || null,
        permissionKey: createPermissionKey.trim() || null,
      });
      setCreateTarget(null);
      await load();
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setCreatingItem(false);
    }
  };

  const handleSave = async () => {
    if (!menu) return;
    setSaving(true);
    setError('');
    setSuccess(false);
    try {
      const orderedGroupIds = menu.groups.map((g) => g.id);
      await superAdminService.reorderNavigationGroups(orderedGroupIds);
      const levels: NavItemSiblingOrderLevel[] = [];
      for (const g of menu.groups) {
        collectItemLevels(g.id, g.rootItems, null, levels);
      }
      await superAdminService.reorderNavigationItemLevels(levels);
      setSuccess(true);
      await load();
    } catch (e) {
      setError(formatApiError(e));
    } finally {
      setSaving(false);
    }
  };

  const selection = useMemo(() => {
    if (!menu || !selectedKey) return null;
    const p = parseNavRowKey(selectedKey);
    if (!p) return null;
    return findNavItemInMenu(menu, p.groupId, p.itemId);
  }, [menu, selectedKey]);

  const previewMenuItems = useMemo(() => {
    if (!menu) return [];
    const groups = adminNavigationToSessionMenu(menu);
    const tree = sessionGroupsToEditorTree(groups, new Map());
    return editorToMenuItems(tree);
  }, [menu]);

  useEffect(() => {
    if (!splitWorkspace) return;
    if (!selection) {
      setEditDisplayLabel('');
      setEditRoutePath('/');
      setEditModuleKey('');
      setEditPermissionKey('');
      setEditFeatureId('');
      return;
    }
    const it = selection.item;
    setEditDisplayLabel(it.displayLabel?.trim() || '');
    setEditRoutePath(it.routePath?.trim() || '/');
    setEditModuleKey(it.moduleKey?.trim() || '');
    setEditPermissionKey(it.permissionKey?.trim() || '');
    setEditFeatureId(it.saasFeatureDefinitionId?.trim() || '');
  }, [splitWorkspace, selection]);

  const cancelSplitEdit = () => {
    if (!selection || savingEdit) return;
    const it = selection.item;
    setEditDisplayLabel(it.displayLabel?.trim() || '');
    setEditRoutePath(it.routePath?.trim() || '/');
    setEditModuleKey(it.moduleKey?.trim() || '');
    setEditPermissionKey(it.permissionKey?.trim() || '');
    setEditFeatureId(it.saasFeatureDefinitionId?.trim() || '');
    setError('');
  };

  const openEditNavItem = () => {
    if (!selection || splitWorkspace) return;
    const it = selection.item;
    setEditDisplayLabel(it.displayLabel?.trim() || '');
    setEditRoutePath(it.routePath?.trim() || '/');
    setEditModuleKey(it.moduleKey?.trim() || '');
    setEditPermissionKey(it.permissionKey?.trim() || '');
    setEditFeatureId(it.saasFeatureDefinitionId?.trim() || '');
    setEditOpen(true);
    setError('');
  };

  const closeEditNavItem = () => {
    if (savingEdit) return;
    setEditOpen(false);
  };

  const handleEditNavItemSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!selection) return;
    const displayLabel = editDisplayLabel.trim();
    const routePath = editRoutePath.trim();
    if (!displayLabel) {
      setError(t('superadmin.navigationMenu.createItemErrorDisplayRequired'));
      return;
    }
    if (!routePath.startsWith('/')) {
      setError(t('superadmin.navigationMenu.createItemErrorRoute'));
      return;
    }
    setSavingEdit(true);
    setError('');
    try {
      await menuService.updateMenuItem(selection.item.id, {
        displayLabel,
        routePath,
        moduleKey: editModuleKey.trim() || null,
        permissionKey: editPermissionKey.trim() || null,
        saasFeatureDefinitionId: editFeatureId.trim() || null,
      });
      if (!splitWorkspace) setEditOpen(false);
      setSelectedKey(null);
      await load();
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setSavingEdit(false);
    }
  };

  const handleDeleteNavItem = async () => {
    if (!selection) return;
    setDeletingItem(true);
    setError('');
    try {
      await menuService.deleteMenuItem(selection.item.id);
      setDeleteOpen(false);
      setSelectedKey(null);
      await load();
    } catch (err) {
      setError(formatApiError(err));
    } finally {
      setDeletingItem(false);
    }
  };

  return {
    t,
    isSuperAdmin,
    hasSelectedSubscriber,
    loading,
    saving,
    error,
    success,
    menu,
    expandedMap,
    setExpandedMap,
    expandedGroups,
    setExpandedGroups,
    selectedKey,
    setSelectedKey,
    pinMap,
    createTarget,
    createDisplayLabel,
    setCreateDisplayLabel,
    createRoutePath,
    setCreateRoutePath,
    createModuleKey,
    setCreateModuleKey,
    createPermissionKey,
    setCreatePermissionKey,
    creatingItem,
    editOpen,
    editDisplayLabel,
    setEditDisplayLabel,
    editRoutePath,
    setEditRoutePath,
    editModuleKey,
    setEditModuleKey,
    editPermissionKey,
    setEditPermissionKey,
    editFeatureId,
    setEditFeatureId,
    savingEdit,
    deleteOpen,
    setDeleteOpen,
    deletingItem,
    previewLayout,
    setPreviewLayout,
    load,
    moveGroup,
    moveItem,
    indentItem,
    outdentItem,
    setExpandedForGroup,
    togglePin,
    openCreateNavItem,
    closeCreateNavItem,
    handleCreateNavItemSubmit,
    handleSave,
    selection,
    previewMenuItems,
    cancelSplitEdit,
    openEditNavItem,
    closeEditNavItem,
    handleEditNavItemSubmit,
    handleDeleteNavItem,
  };
}

export type NavigationMenuEditorPanelState = ReturnType<typeof useNavigationMenuEditorPanel>;
