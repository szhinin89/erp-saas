import type { Dispatch, ReactNode, SetStateAction } from 'react';
import type { AdminNavGroupRow, AdminNavItemRow } from '../../services/superAdminService';

export type NavMenuItemTrailingRenderArgs = { groupId: string; item: AdminNavItemRow };

export function expandKey(groupId: string, itemId: string): string {
  return `${groupId}::${itemId}`;
}

export function collectExpandableIds(items: AdminNavItemRow[], acc: string[]): void {
  for (const it of items) {
    const ch = it.children ?? [];
    if (ch.length) {
      acc.push(it.id);
      collectExpandableIds(ch, acc);
    }
  }
}

/** Props compartidos: árbol recursivo de ítems bajo un grupo. */
export type NavMenuItemLevelsProps = {
  groupId: string;
  items: AdminNavItemRow[];
  disabled: boolean;
  expandedMap: Record<string, boolean>;
  setExpandedMap: Dispatch<SetStateAction<Record<string, boolean>>>;
  selectedKey: string | null;
  setSelectedKey: Dispatch<SetStateAction<string | null>>;
  pinMap: Record<string, boolean>;
  onTogglePin: (itemId: string) => void;
  onExpandAllItems: () => void;
  onCollapseAllItems: () => void;
  onMoveItem: (groupId: string, itemId: string, delta: number) => void;
  onIndentItem: (groupId: string, itemId: string) => void;
  onOutdentItem: (groupId: string, itemId: string) => void;
  onRequestAddRootItem?: () => void;
  onRequestAddChildItem?: (parentItemId: string) => void;
  /** Contenido opcional a la derecha de los botones de edición (p. ej. asignación plan ↔ features). */
  renderItemTrailing?: (args: NavMenuItemTrailingRenderArgs) => ReactNode;
  t: (key: string, fallback?: string) => string;
};

/**
 * Un solo árbol de ítems (todos los niveles) para un grupo: función interna `renderLevel`
 * recursiva, sin subcomponentes React adicionales.
 */
export function NavMenuItemLevels({
  groupId,
  items,
  disabled,
  expandedMap,
  setExpandedMap,
  selectedKey,
  setSelectedKey,
  pinMap,
  onTogglePin,
  onExpandAllItems,
  onCollapseAllItems,
  onMoveItem,
  onIndentItem,
  onOutdentItem,
  onRequestAddRootItem,
  onRequestAddChildItem,
  renderItemTrailing,
  t,
}: NavMenuItemLevelsProps) {
  function renderLevel(levelItems: AdminNavItemRow[], depth: number, insidePanel: boolean): ReactNode {
    if (!levelItems.length) {
      return <p className="nm-emptyBranch">{t('superadmin.navigationMenu.emptyGroup')}</p>;
    }

    return (
      <ul className={insidePanel ? 'nm-list nm-list--panel' : 'nm-list'} role="list">
        {levelItems.map((it, ii) => {
          const children = it.children ?? [];
          const hasChildren = children.length > 0;
          const ek = expandKey(groupId, it.id);
          const expanded = expandedMap[ek] ?? false;
          const rowKey = expandKey(groupId, it.id);
          const isActive = selectedKey === rowKey;
          const canIndent = ii > 0;
          const canOutdent = depth > 0;
          const isLeaf = !hasChildren;
          const pinOn = !!pinMap[it.id];

          const rowClass = [
            'nm-row',
            depth === 0 && !insidePanel ? 'nm-row--root' : '',
            insidePanel || depth > 0 ? 'nm-row--inStack' : '',
            isLeaf && (insidePanel || depth > 0) ? 'nm-row--leaf' : '',
            hasChildren ? 'nm-row--parent' : '',
            isActive ? 'nm-row--active' : '',
          ]
            .filter(Boolean)
            .join(' ');

          const label = it.displayLabel?.trim() || t(it.labelKey);

          return (
            <li key={it.id} className="nm-node">
              <div className={rowClass}>
                {hasChildren ? (
                  <button
                    type="button"
                    className={`nm-chevron${expanded ? ' is-open' : ''}`}
                    aria-expanded={expanded}
                    aria-label={t('superadmin.navigationMenu.toggleSubmenu')}
                    disabled={disabled}
                    onClick={(e) => {
                      e.stopPropagation();
                      setExpandedMap((prev) => ({ ...prev, [ek]: !expanded }));
                    }}
                  >
                    {expanded ? '▼' : '▶'}
                  </button>
                ) : (
                  <span className="nm-chevronSpacer" aria-hidden />
                )}

                <button
                  type="button"
                  className="nm-rowMain"
                  disabled={disabled}
                  title={it.routePath}
                  onClick={() => setSelectedKey((k) => (k === rowKey ? null : rowKey))}
                >
                  <span className="nm-rowLabel">{label}</span>
                  <span className="nm-rowRoute mono">{it.routePath}</span>
                </button>

                <span className="nm-editBtns" onClick={(e) => e.stopPropagation()}>
                  <button
                    type="button"
                    className="nm-miniBtn"
                    aria-label={t('superadmin.navigationMenu.outdent')}
                    disabled={disabled || !canOutdent}
                    onClick={() => onOutdentItem(groupId, it.id)}
                  >
                    ←
                  </button>
                  <button
                    type="button"
                    className="nm-miniBtn"
                    aria-label={t('superadmin.navigationMenu.indent')}
                    disabled={disabled || !canIndent}
                    onClick={() => onIndentItem(groupId, it.id)}
                  >
                    →
                  </button>
                  <button
                    type="button"
                    className="nm-miniBtn"
                    aria-label={t('superadmin.navigationMenu.up')}
                    disabled={disabled || ii === 0}
                    onClick={() => onMoveItem(groupId, it.id, -1)}
                  >
                    ↑
                  </button>
                  <button
                    type="button"
                    className="nm-miniBtn"
                    aria-label={t('superadmin.navigationMenu.down')}
                    disabled={disabled || ii >= levelItems.length - 1}
                    onClick={() => onMoveItem(groupId, it.id, 1)}
                  >
                    ↓
                  </button>
                  {onRequestAddChildItem ? (
                    <button
                      type="button"
                      className="nm-miniBtn nm-miniBtn--add"
                      aria-label={t('superadmin.navigationMenu.addChildItem')}
                      disabled={disabled}
                      onClick={() => onRequestAddChildItem(it.id)}
                    >
                      ＋
                    </button>
                  ) : null}
                </span>

                {renderItemTrailing ? (
                  <div className="nm-rowTrailing" onClick={(e) => e.stopPropagation()}>
                    {renderItemTrailing({ groupId, item: it })}
                  </div>
                ) : null}

                {isLeaf ? (
                  <button
                    type="button"
                    className={`nm-star${pinOn ? ' is-on' : ''}`}
                    aria-pressed={pinOn}
                    aria-label={t('superadmin.navigationMenu.pinToggle')}
                    disabled={disabled}
                    onClick={() => onTogglePin(it.id)}
                  >
                    {pinOn ? '★' : '☆'}
                  </button>
                ) : (
                  <span className="nm-starSpacer" aria-hidden />
                )}
              </div>

              {hasChildren && expanded ? (
                <div className="nm-childrenPanel">
                  <div className="nm-childrenHead">{label}</div>
                  {renderLevel(children, depth + 1, true)}
                </div>
              ) : null}
            </li>
          );
        })}
      </ul>
    );
  }

  return (
    <div className="nm-groupItemsWrap">
      <div className="nm-groupItemToolbar">
        <button type="button" className="nm-textLink" disabled={disabled} onClick={onExpandAllItems}>
          {t('superadmin.navigationMenu.expandAll')}
        </button>
        <span className="nm-dot" aria-hidden>
          ·
        </span>
        <button type="button" className="nm-textLink" disabled={disabled} onClick={onCollapseAllItems}>
          {t('superadmin.navigationMenu.collapseAll')}
        </button>
        {onRequestAddRootItem ? (
          <>
            <span className="nm-dot" aria-hidden>
              ·
            </span>
            <button type="button" className="nm-textLink" disabled={disabled} onClick={onRequestAddRootItem}>
              {t('superadmin.navigationMenu.addRootItem')}
            </button>
          </>
        ) : null}
      </div>
      <nav className="nm-groupItemsNav" aria-label={t('superadmin.navigationMenu.itemsInsideGroup')}>
        {items.length === 0 ? (
          <div className="nm-emptyGroupBody">
            <p className="nm-emptyBranch">{t('superadmin.navigationMenu.emptyGroup')}</p>
            {onRequestAddRootItem ? (
              <button type="button" className="nm-textLink" disabled={disabled} onClick={onRequestAddRootItem}>
                {t('superadmin.navigationMenu.addFirstItem')}
              </button>
            ) : null}
          </div>
        ) : (
          renderLevel(items, 0, false)
        )}
      </nav>
    </div>
  );
}

export type NavigationBarMenuEditorProps = {
  groups: AdminNavGroupRow[];
  disabled: boolean;
  /** Grupo de barra expandido → muestra ítems anidados. */
  expandedGroups: Record<string, boolean>;
  setExpandedGroups: Dispatch<SetStateAction<Record<string, boolean>>>;
  expandedMap: Record<string, boolean>;
  setExpandedMap: Dispatch<SetStateAction<Record<string, boolean>>>;
  selectedKey: string | null;
  setSelectedKey: Dispatch<SetStateAction<string | null>>;
  pinMap: Record<string, boolean>;
  onTogglePin: (itemId: string) => void;
  onExpandAllItemsInGroup: (groupId: string, rootItems: AdminNavItemRow[]) => void;
  onCollapseAllItemsInGroup: (groupId: string, rootItems: AdminNavItemRow[]) => void;
  onMoveGroup: (groupIndex: number, delta: number) => void;
  onMoveItem: (groupId: string, itemId: string, delta: number) => void;
  onIndentItem: (groupId: string, itemId: string) => void;
  onOutdentItem: (groupId: string, itemId: string) => void;
  /** Abre el flujo para crear un ítem (raíz si parentItemId es null). */
  onRequestAddNavItem: (groupId: string, parentItemId: string | null) => void;
  renderItemTrailing?: (args: NavMenuItemTrailingRenderArgs) => ReactNode;
  t: (key: string, fallback?: string) => string;
};

/**
 * Lista de grupos de la barra (tarjetas como la referencia); cada uno se expande
 * para mostrar el mismo árbol de ítems con reubicación en todos los niveles.
 */
export function NavigationBarMenuEditor({
  groups,
  disabled,
  expandedGroups,
  setExpandedGroups,
  expandedMap,
  setExpandedMap,
  selectedKey,
  setSelectedKey,
  pinMap,
  onTogglePin,
  onExpandAllItemsInGroup,
  onCollapseAllItemsInGroup,
  onMoveGroup,
  onMoveItem,
  onIndentItem,
  onOutdentItem,
  onRequestAddNavItem,
  renderItemTrailing,
  t,
}: NavigationBarMenuEditorProps) {
  return (
    <ul className="nm-groupCardList" role="list">
      {groups.map((g, gi) => {
        const groupOpen = expandedGroups[g.id] ?? false;
        return (
          <li key={g.id} className="nm-groupCard">
            <div className="nm-groupCardHeader">
              <button
                type="button"
                className={`nm-groupChevron${groupOpen ? ' is-open' : ''}`}
                aria-expanded={groupOpen}
                aria-controls={`nm-group-body-${g.id}`}
                id={`nm-group-head-${g.id}`}
                aria-label={t('superadmin.navigationMenu.toggleGroupItems')}
                disabled={disabled}
                onClick={() =>
                  setExpandedGroups((prev) => ({
                    ...prev,
                    [g.id]: !groupOpen,
                  }))
                }
              >
                {groupOpen ? '▼' : '▶'}
              </button>
              <span className="nm-groupRowLabel">
                <span className="nm-groupIcon" aria-hidden>
                  {g.icon}
                </span>
                <span className="nm-groupText">{t(g.labelKey)}</span>
                <span className="nm-groupCode mono">{g.code}</span>
              </span>
              <span className="nm-editBtns">
                <button
                  type="button"
                  className="nm-miniBtn"
                  aria-label={t('superadmin.navigationMenu.up')}
                  disabled={disabled || gi === 0}
                  onClick={() => onMoveGroup(gi, -1)}
                >
                  ↑
                </button>
                <button
                  type="button"
                  className="nm-miniBtn"
                  aria-label={t('superadmin.navigationMenu.down')}
                  disabled={disabled || gi >= groups.length - 1}
                  onClick={() => onMoveGroup(gi, 1)}
                >
                  ↓
                </button>
              </span>
            </div>

            {groupOpen ? (
              <div
                className="nm-groupCardBody"
                id={`nm-group-body-${g.id}`}
                role="region"
                aria-labelledby={`nm-group-head-${g.id}`}
              >
                <NavMenuItemLevels
                  groupId={g.id}
                  items={g.rootItems}
                  disabled={disabled}
                  expandedMap={expandedMap}
                  setExpandedMap={setExpandedMap}
                  selectedKey={selectedKey}
                  setSelectedKey={setSelectedKey}
                  pinMap={pinMap}
                  onTogglePin={onTogglePin}
                  onExpandAllItems={() => onExpandAllItemsInGroup(g.id, g.rootItems)}
                  onCollapseAllItems={() => onCollapseAllItemsInGroup(g.id, g.rootItems)}
                  onMoveItem={onMoveItem}
                  onIndentItem={onIndentItem}
                  onOutdentItem={onOutdentItem}
                  onRequestAddRootItem={() => onRequestAddNavItem(g.id, null)}
                  onRequestAddChildItem={(parentId) => onRequestAddNavItem(g.id, parentId)}
                  renderItemTrailing={renderItemTrailing}
                  t={t}
                />
              </div>
            ) : null}
          </li>
        );
      })}
    </ul>
  );
}
