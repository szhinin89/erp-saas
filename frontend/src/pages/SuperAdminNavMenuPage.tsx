import { useCallback, useEffect, useState } from 'react';
import { NavLink } from 'react-router-dom';
import { ErrorState, LoadingState, PageShell, TableCard } from '../components/PageShell';
import { useAuthStore } from '../store/authStore';
import { useI18n } from '../i18n/i18n';
import {
  superAdminService,
  type AdminNavItemRow,
  type AdminNavigationMenu,
  type NavItemSiblingOrderLevel,
} from '../services/superAdminService';
import { formatApiError } from '../modules/lib/formatApiError';
import { ZHBtn } from '../components/zh/ZHForm';
import { ZHCardSection, ZHInlineRowRight } from '../components/zh/ZHLayout';
import './SuperAdminNavMenuPage.css';

function cloneMenu(m: AdminNavigationMenu): AdminNavigationMenu {
  return typeof structuredClone === 'function'
    ? structuredClone(m)
    : (JSON.parse(JSON.stringify(m)) as AdminNavigationMenu);
}

function moveInPlace<T>(arr: T[], index: number, delta: number): void {
  const j = index + delta;
  if (j < 0 || j >= arr.length) return;
  const t = arr[index]!;
  arr[index] = arr[j]!;
  arr[j] = t;
}

function findSiblingArray(items: AdminNavItemRow[], itemId: string): AdminNavItemRow[] | null {
  if (items.some((i) => i.id === itemId)) return items;
  for (const it of items) {
    const ch = it.children ?? [];
    if (ch.length) {
      const inner = findSiblingArray(ch, itemId);
      if (inner) return inner;
    }
  }
  return null;
}

function collectItemLevels(
  groupId: string,
  items: AdminNavItemRow[],
  parentItemId: string | null,
  out: NavItemSiblingOrderLevel[],
): void {
  if (items.length === 0) return;
  out.push({ groupId, parentItemId, orderedItemIds: items.map((i) => i.id) });
  for (const it of items) {
    const ch = it.children ?? [];
    if (ch.length) collectItemLevels(groupId, ch, it.id, out);
  }
}

export function SuperAdminNavMenuPage() {
  const { t } = useI18n();
  const { user } = useAuthStore();
  const isSuperAdmin = user?.role === 'SuperAdmin';
  const hasSelectedTenant = !!user?.tenantId && user.tenantId !== '00000000-0000-0000-0000-000000000000';

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState(false);
  const [menu, setMenu] = useState<AdminNavigationMenu | null>(null);

  const load = useCallback(async () => {
    setError('');
    setSuccess(false);
    const data = await superAdminService.getNavigationMenu();
    setMenu(cloneMenu(data));
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

  if (!isSuperAdmin) {
    return (
      <PageShell kicker={t('app.nav.group.home')} title={t('superadmin.navigationMenu.title')}>
        <TableCard>
          <div className="empty-state">{t('superadmin.noAccess')}</div>
        </TableCard>
      </PageShell>
    );
  }

  if (hasSelectedTenant) {
    return (
      <PageShell
        kicker={t('app.nav.group.home')}
        title={t('superadmin.navigationMenu.title')}
        action={<NavLink to="/superadmin">{t('superadmin.backToGlobal')}</NavLink>}
      >
        <TableCard>
          <div className="empty-state">{t('superadmin.alreadyInTenant')}</div>
        </TableCard>
      </PageShell>
    );
  }

  return (
    <PageShell
      kicker={t('app.nav.group.home')}
      title={t('superadmin.navigationMenu.title')}
      subtitle={t('superadmin.navigationMenu.subtitle')}
      action={<NavLink to="/superadmin">← {t('app.nav.superadmin')}</NavLink>}
    >
      {error ? <ErrorState message={error} /> : null}
      {success ? (
        <p className="subtle" role="status">
          {t('superadmin.navigationMenu.saved')}
        </p>
      ) : null}

      <TableCard>
        {loading ? (
          <LoadingState />
        ) : menu ? (
          <>
            <ZHCardSection title={t('superadmin.navigationMenu.groups')}>
              <ul className="sa-navmenu-list">
                {menu.groups.map((g, gi) => (
                  <li key={g.id} className="sa-navmenu-group">
                    <span className="sa-navmenu-groupLabel">
                      {g.icon} {t(g.labelKey)} <span className="mono subtle">({g.code})</span>
                    </span>
                    <span className="sa-navmenu-actions">
                      <button
                        type="button"
                        className="sa-navmenu-btn"
                        aria-label={t('superadmin.navigationMenu.up')}
                        disabled={saving || gi === 0}
                        onClick={() => moveGroup(gi, -1)}
                      >
                        ↑
                      </button>
                      <button
                        type="button"
                        className="sa-navmenu-btn"
                        aria-label={t('superadmin.navigationMenu.down')}
                        disabled={saving || gi >= menu.groups.length - 1}
                        onClick={() => moveGroup(gi, 1)}
                      >
                        ↓
                      </button>
                    </span>
                  </li>
                ))}
              </ul>
            </ZHCardSection>

            <ZHCardSection title={t('superadmin.navigationMenu.items')}>
              {menu.groups.map((g) => (
                <div key={g.id} className="sa-navmenu-block">
                  <h3 className="sa-navmenu-blockTitle">
                    {g.icon} {t(g.labelKey)}
                  </h3>
                  <ItemTreeEditor
                    groupId={g.id}
                    items={g.rootItems}
                    depth={0}
                    disabled={saving}
                    onMoveItem={moveItem}
                    t={t}
                  />
                </div>
              ))}
            </ZHCardSection>

            <ZHInlineRowRight>
              <ZHBtn type="button" variant="secondary" disabled={saving} onClick={() => void load()}>
                {t('superadmin.navigationMenu.reload')}
              </ZHBtn>
              <ZHBtn type="button" variant="primary" disabled={saving} onClick={() => void handleSave()}>
                {saving ? t('common.saving') : t('superadmin.navigationMenu.save')}
              </ZHBtn>
            </ZHInlineRowRight>
          </>
        ) : (
          <div className="empty-state">{t('superadmin.sectionLoadHint')}</div>
        )}
      </TableCard>
    </PageShell>
  );
}

function ItemTreeEditor({
  groupId,
  items,
  depth,
  disabled,
  onMoveItem,
  t,
}: {
  groupId: string;
  items: AdminNavItemRow[];
  depth: number;
  disabled: boolean;
  onMoveItem: (groupId: string, itemId: string, delta: number) => void;
  t: (key: string) => string;
}) {
  if (!items.length) return <p className="subtle">—</p>;
  return (
    <ul className={`sa-navmenu-itemTree${depth > 0 ? ' is-nested' : ''}`}>
      {items.map((it, ii) => (
        <li key={it.id}>
          <div className={`sa-navmenu-itemRow sa-navmenu-itemRow--d${Math.min(depth, 12)}`}>
            <span className="sa-navmenu-itemLabel">
              {t(it.labelKey)} <span className="mono subtle">{it.routePath}</span>
            </span>
            <span className="sa-navmenu-actions">
              <button
                type="button"
                className="sa-navmenu-btn"
                aria-label={t('superadmin.navigationMenu.up')}
                disabled={disabled || ii === 0}
                onClick={() => onMoveItem(groupId, it.id, -1)}
              >
                ↑
              </button>
              <button
                type="button"
                className="sa-navmenu-btn"
                aria-label={t('superadmin.navigationMenu.down')}
                disabled={disabled || ii >= items.length - 1}
                onClick={() => onMoveItem(groupId, it.id, 1)}
              >
                ↓
              </button>
            </span>
          </div>
          {(it.children ?? []).length > 0 ? (
            <ItemTreeEditor
              groupId={groupId}
              items={it.children ?? []}
              depth={depth + 1}
              disabled={disabled}
              onMoveItem={onMoveItem}
              t={t}
            />
          ) : null}
        </li>
      ))}
    </ul>
  );
}
