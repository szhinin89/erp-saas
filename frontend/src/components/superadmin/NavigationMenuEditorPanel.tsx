import { useCallback, useEffect, useRef, useState, type FormEvent } from 'react';
import { LoadingState, TableCard } from '../PageShell';
import { ZHPageNotice } from '../zh/ZHPageNotice';
import { collectExpandableIds, expandKey, NavigationBarMenuEditor } from './NavigationMenuTree';
import {
  cloneMenu,
  collectItemLevels,
  findAncestorPathIncludingSelf,
  findItemLocation,
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
import { formatApiError } from '../../modules/lib/formatApiError';
import { ZHBtn } from '../zh/ZHForm';
import { ZHInlineRowRight } from '../zh/ZHLayout';
import '../../pages/SuperAdminNavMenuPage.css';

/**
 * Editor global del menú principal (grupos de barra + ítems). Misma API que la antigua página solo SuperAdmin.
 */
export function NavigationMenuEditorPanel() {
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

  if (!isSuperAdmin || hasSelectedTenant) {
    return (
      <p className="zh-help-text subtle">{t('superadmin.sectionLoadHint')}</p>
    );
  }

  return (
    <>
      {error ? <ZHPageNotice variant="error" message={t('common.errorPrefix')} detail={error} /> : null}
      {success ? <ZHPageNotice variant="success" message={t('superadmin.navigationMenu.saved')} /> : null}

      <TableCard>
        {loading ? (
          <LoadingState />
        ) : menu ? (
          <div className="sa-navmenu-page">
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
          </div>
        ) : (
          <div className="empty-state">{t('superadmin.sectionLoadHint')}</div>
        )}
      </TableCard>
    </>
  );
}
