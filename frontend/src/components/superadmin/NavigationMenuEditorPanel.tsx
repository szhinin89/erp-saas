import { useCallback, useEffect, useMemo, useRef, useState, type FormEvent } from 'react';
import { LoadingState } from '../PageShell';
import { Card } from '../ui';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { collectExpandableIds, expandKey, NavigationBarMenuEditor } from './NavigationMenuTree';
import {
  cloneMenu,
  collectItemLevels,
  countNavSubtreeNodes,
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
  type AdminNavItemRow,
  type AdminNavigationMenu,
  type NavItemSiblingOrderLevel,
} from '../../services/superAdminService';
import { menuService } from '../../services/menuService';
import { formatApiError } from '../../modules/lib/formatApiError';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHInlineRowRight } from '../zh/ZHLayout';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { MenuPreview, type MenuPreviewLayout } from '../menu-builder/MenuPreview';
import { editorToMenuItems, sessionGroupsToEditorTree } from '../menu-builder/menuBuilderTypes';
import { adminNavigationToSessionMenu } from '../../modules/superadmin/adminNavigationToSessionMenu';
import '../../pages/SuperAdminNavMenuPage.css';

function parseNavRowKey(key: string | null): { groupId: string; itemId: string } | null {
  if (!key) return null;
  const parts = key.split('::');
  if (parts.length !== 2 || !parts[0] || !parts[1]) return null;
  return { groupId: parts[0]!, itemId: parts[1]! };
}

export type NavigationMenuEditorPanelProps = {
  /** Vista 2:1: árbol a la izquierda, propiedades + vista previa a la derecha. */
  splitWorkspace?: boolean;
};

/**
 * Editor global del menú principal (grupos de barra + ítems). Misma API que la antigua página solo SuperAdmin.
 */
export function NavigationMenuEditorPanel({ splitWorkspace = false }: NavigationMenuEditorPanelProps) {
  const { t } = useI18n();
  const { isSuperAdmin, hasSelectedTenant } = useSuperAdminGate();

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
    if (!isSuperAdmin || hasSelectedTenant) return;
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
  }, [isSuperAdmin, hasSelectedTenant, load]);

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

  if (!isSuperAdmin || hasSelectedTenant) {
    return (
      <p className="zh-help-text subtle">{t('superadmin.sectionLoadHint')}</p>
    );
  }

  return (
    <>
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      {success ? <ZHPageNotice variant="success" message={t('superadmin.navigationMenu.saved')} /> : null}

      <Card>
        {loading ? (
          <LoadingState />
        ) : menu ? (
          <div className={`sa-navmenu-page${splitWorkspace ? ' sa-navmenu-page--split' : ''}`}>
            <div className="nm-splitMain">
              <section className="nm-barEditor" aria-label={t('superadmin.navigationMenu.groups')}>
                <h2 className="nm-sectionTitle">{t('superadmin.navigationMenu.groups')}</h2>
                <NavigationBarMenuEditor
                groups={menu.groups}
                disabled={saving || creatingItem}
                expandedGroups={expandedGroups}
                setExpandedGroups={setExpandedGroups}
                expandedMap={expandedMap}
                setExpandedMap={setExpandedMap}
                selectedKey={selectedKey}
                setSelectedKey={setSelectedKey}
                pinMap={pinMap}
                onTogglePin={togglePin}
                onExpandAllItemsInGroup={(groupId, rootItems) => setExpandedForGroup(groupId, rootItems, true)}
                onCollapseAllItemsInGroup={(groupId, rootItems) => setExpandedForGroup(groupId, rootItems, false)}
                onMoveGroup={moveGroup}
                onMoveItem={moveItem}
                onIndentItem={indentItem}
                onOutdentItem={outdentItem}
                onRequestAddNavItem={openCreateNavItem}
                t={t}
              />
              </section>

              {!splitWorkspace && selection ? (
                <div className="nm-selectionBar">
                  <span className="nm-selectionBar__label">
                    {selection.item.displayLabel?.trim() || t(selection.item.labelKey)}
                  </span>
                  <ZHInlineRowRight>
                    <ZHBtn type="button" variant="secondary" disabled={saving || creatingItem} onClick={openEditNavItem}>
                      {t('superadmin.navigationMenu.editItem')}
                    </ZHBtn>
                    <ZHBtn
                      type="button"
                      variant="destructive"
                      disabled={saving || creatingItem}
                      onClick={() => setDeleteOpen(true)}
                    >
                      {t('superadmin.navigationMenu.deleteItem')}
                    </ZHBtn>
                  </ZHInlineRowRight>
                </div>
              ) : null}
            </div>

            {splitWorkspace ? (
              <aside className="nm-splitAside" aria-label={t('superadmin.navigationMenu.splitAsideLabel')}>
                <ZHCardSection title={t('superadmin.navigationMenu.splitPropertiesHeading')}>
                  {!selection ? (
                    <p className="zh-help-text subtle">{t('superadmin.navigationMenu.selectNodeHint')}</p>
                  ) : (
                    <>
                      <p className="nm-splitAside__nodeName">
                        <strong>{t('superadmin.navigationMenu.splitSelectedLabel')}</strong>{' '}
                        {selection.item.displayLabel?.trim() || t(selection.item.labelKey)}
                      </p>
                      <form onSubmit={(e) => void handleEditNavItemSubmit(e)}>
                        <ZHField label={t('superadmin.navigationMenu.createItemDisplayLabel')}>
                          <input
                            className="zh-input"
                            value={editDisplayLabel}
                            onChange={(e) => setEditDisplayLabel(e.target.value)}
                            disabled={savingEdit || saving || creatingItem}
                            autoComplete="off"
                            maxLength={200}
                          />
                        </ZHField>
                        <ZHField label={t('superadmin.navigationMenu.createItemRoutePath')}>
                          <input
                            className="zh-input"
                            value={editRoutePath}
                            onChange={(e) => setEditRoutePath(e.target.value)}
                            disabled={savingEdit || saving || creatingItem}
                            autoComplete="off"
                          />
                        </ZHField>
                        <ZHField label={t('superadmin.navigationMenu.createItemModuleKey')}>
                          <input
                            className="zh-input"
                            value={editModuleKey}
                            onChange={(e) => setEditModuleKey(e.target.value)}
                            disabled={savingEdit || saving || creatingItem}
                            autoComplete="off"
                          />
                        </ZHField>
                        <ZHField label={t('superadmin.navigationMenu.createItemPermissionKey')}>
                          <input
                            className="zh-input"
                            value={editPermissionKey}
                            onChange={(e) => setEditPermissionKey(e.target.value)}
                            disabled={savingEdit || saving || creatingItem}
                            autoComplete="off"
                          />
                        </ZHField>
                        <ZHField label={t('superadmin.navigationMenu.editFeatureId')}>
                          <input
                            className="zh-input"
                            value={editFeatureId}
                            onChange={(e) => setEditFeatureId(e.target.value)}
                            disabled={savingEdit || saving || creatingItem}
                            autoComplete="off"
                            placeholder={t('superadmin.navigationMenu.editFeatureIdHint')}
                          />
                        </ZHField>
                        <p className="zh-help-text subtle">{t('superadmin.navigationMenu.splitFolderHint')}</p>
                        <ZHInlineRowRight>
                          <ZHBtn type="button" variant="secondary" disabled={savingEdit} onClick={cancelSplitEdit}>
                            {t('superadmin.navigationMenu.createItemCancel')}
                          </ZHBtn>
                          <ZHBtn type="submit" variant="primary" disabled={savingEdit || saving || creatingItem}>
                            {savingEdit ? t('common.saving') : t('superadmin.navigationMenu.saveItem')}
                          </ZHBtn>
                          <ZHBtn
                            type="button"
                            variant="destructive"
                            disabled={saving || creatingItem || savingEdit}
                            onClick={() => setDeleteOpen(true)}
                          >
                            {t('superadmin.navigationMenu.deleteItem')}
                          </ZHBtn>
                        </ZHInlineRowRight>
                      </form>
                    </>
                  )}
                </ZHCardSection>
                <ZHCardSection title={t('superadmin.navigationMenu.splitPreviewHeading')}>
                  <div className="nm-splitPreviewLayout" role="radiogroup" aria-label={t('superadmin.menuBuilder.previewLayout')}>
                    <label className="nm-splitPreviewLayout__opt">
                      <input
                        type="radio"
                        name="nm-prev-layout"
                        checked={previewLayout === 'horizontal'}
                        onChange={() => setPreviewLayout('horizontal')}
                      />{' '}
                      {t('superadmin.menuBuilder.layoutHorizontal')}
                    </label>
                    <label className="nm-splitPreviewLayout__opt">
                      <input
                        type="radio"
                        name="nm-prev-layout"
                        checked={previewLayout === 'vertical'}
                        onChange={() => setPreviewLayout('vertical')}
                      />{' '}
                      {t('superadmin.menuBuilder.layoutVertical')}
                    </label>
                  </div>
                  <MenuPreview items={previewMenuItems} layout={previewLayout} />
                </ZHCardSection>
              </aside>
            ) : null}

            <div className="nm-footerBar">
              <ZHInlineRowRight>
                <ZHBtn type="button" variant="secondary" disabled={saving || creatingItem} onClick={() => void load()}>
                  {t('superadmin.navigationMenu.reload')}
                </ZHBtn>
                <ZHBtn type="button" variant="primary" disabled={saving || creatingItem} onClick={() => void handleSave()}>
                  {saving ? t('common.saving') : t('superadmin.navigationMenu.save')}
                </ZHBtn>
              </ZHInlineRowRight>
            </div>

            {createTarget ? (
              <div
                className="nm-createBackdrop"
                role="presentation"
                onClick={(ev) => {
                  if (ev.target === ev.currentTarget) closeCreateNavItem();
                }}
              >
                <dialog className="nm-createDialog" open aria-labelledby="nm-create-title">
                  <h3 id="nm-create-title">{t('superadmin.navigationMenu.createItemTitle')}</h3>
                  <p className="nm-createHint">
                    {createTarget.parentItemId
                      ? t('superadmin.navigationMenu.createItemHintChild')
                      : t('superadmin.navigationMenu.createItemHintRoot')}
                  </p>
                  <form onSubmit={(e) => void handleCreateNavItemSubmit(e)}>
                    <div className="nm-createField">
                      <label htmlFor="nm-create-label">{t('superadmin.navigationMenu.createItemDisplayLabel')}</label>
                      <input
                        id="nm-create-label"
                        value={createDisplayLabel}
                        onChange={(e) => setCreateDisplayLabel(e.target.value)}
                        disabled={creatingItem}
                        autoComplete="off"
                        maxLength={200}
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-create-route">{t('superadmin.navigationMenu.createItemRoutePath')}</label>
                      <input
                        id="nm-create-route"
                        value={createRoutePath}
                        onChange={(e) => setCreateRoutePath(e.target.value)}
                        disabled={creatingItem}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-create-mod">{t('superadmin.navigationMenu.createItemModuleKey')}</label>
                      <input
                        id="nm-create-mod"
                        value={createModuleKey}
                        onChange={(e) => setCreateModuleKey(e.target.value)}
                        disabled={creatingItem}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-create-perm">{t('superadmin.navigationMenu.createItemPermissionKey')}</label>
                      <input
                        id="nm-create-perm"
                        value={createPermissionKey}
                        onChange={(e) => setCreatePermissionKey(e.target.value)}
                        disabled={creatingItem}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createActions">
                      <ZHBtn type="button" variant="secondary" disabled={creatingItem} onClick={closeCreateNavItem}>
                        {t('superadmin.navigationMenu.createItemCancel')}
                      </ZHBtn>
                      <ZHBtn type="submit" variant="primary" disabled={creatingItem}>
                        {creatingItem ? t('superadmin.navigationMenu.createItemCreating') : t('superadmin.navigationMenu.createItemSubmit')}
                      </ZHBtn>
                    </div>
                  </form>
                </dialog>
              </div>
            ) : null}

            {editOpen && selection && !splitWorkspace ? (
              <div
                className="nm-createBackdrop"
                role="presentation"
                onClick={(ev) => {
                  if (ev.target === ev.currentTarget) closeEditNavItem();
                }}
              >
                <dialog className="nm-createDialog" open aria-labelledby="nm-edit-title">
                  <h3 id="nm-edit-title">{t('superadmin.navigationMenu.editItemTitle')}</h3>
                  <form onSubmit={(e) => void handleEditNavItemSubmit(e)}>
                    <div className="nm-createField">
                      <label htmlFor="nm-edit-label">{t('superadmin.navigationMenu.createItemDisplayLabel')}</label>
                      <input
                        id="nm-edit-label"
                        value={editDisplayLabel}
                        onChange={(e) => setEditDisplayLabel(e.target.value)}
                        disabled={savingEdit}
                        autoComplete="off"
                        maxLength={200}
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-edit-route">{t('superadmin.navigationMenu.createItemRoutePath')}</label>
                      <input
                        id="nm-edit-route"
                        value={editRoutePath}
                        onChange={(e) => setEditRoutePath(e.target.value)}
                        disabled={savingEdit}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-edit-mod">{t('superadmin.navigationMenu.createItemModuleKey')}</label>
                      <input
                        id="nm-edit-mod"
                        value={editModuleKey}
                        onChange={(e) => setEditModuleKey(e.target.value)}
                        disabled={savingEdit}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-edit-perm">{t('superadmin.navigationMenu.createItemPermissionKey')}</label>
                      <input
                        id="nm-edit-perm"
                        value={editPermissionKey}
                        onChange={(e) => setEditPermissionKey(e.target.value)}
                        disabled={savingEdit}
                        autoComplete="off"
                      />
                    </div>
                    <div className="nm-createField">
                      <label htmlFor="nm-edit-feat">{t('superadmin.navigationMenu.editFeatureId')}</label>
                      <input
                        id="nm-edit-feat"
                        value={editFeatureId}
                        onChange={(e) => setEditFeatureId(e.target.value)}
                        disabled={savingEdit}
                        autoComplete="off"
                        placeholder={t('superadmin.navigationMenu.editFeatureIdHint')}
                      />
                    </div>
                    <div className="nm-createActions">
                      <ZHBtn type="button" variant="secondary" disabled={savingEdit} onClick={closeEditNavItem}>
                        {t('superadmin.navigationMenu.createItemCancel')}
                      </ZHBtn>
                      <ZHBtn type="submit" variant="primary" disabled={savingEdit}>
                        {savingEdit ? t('common.saving') : t('superadmin.navigationMenu.saveItem')}
                      </ZHBtn>
                    </div>
                  </form>
                </dialog>
              </div>
            ) : null}

            {deleteOpen && selection ? (
              <ZHConfirmModal
                title={t('superadmin.navigationMenu.deleteItemTitle')}
                message={
                  <>
                    {t('superadmin.navigationMenu.deleteItemConfirmPrefix')}{' '}
                    <strong>{countNavSubtreeNodes(selection.item)}</strong>{' '}
                    {t('superadmin.navigationMenu.deleteItemConfirmSuffix')}
                  </>
                }
                loading={deletingItem}
                onCancel={() => (deletingItem ? undefined : setDeleteOpen(false))}
                onConfirm={() => void handleDeleteNavItem()}
              />
            ) : null}
          </div>
        ) : (
          <div className="empty-state">{t('superadmin.sectionLoadHint')}</div>
        )}
      </Card>
    </>
  );
}
