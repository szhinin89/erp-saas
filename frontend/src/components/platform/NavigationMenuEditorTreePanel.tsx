import { ZHBtn } from '../zh/ZHForm';
import { ZHInlineRowRight } from '../zh/ZHLayout';
import { NavigationBarMenuEditor } from './NavigationMenuTree';
import type { NavigationMenuEditorPanelState } from './useNavigationMenuEditorPanel';

type Props = Pick<
  NavigationMenuEditorPanelState,
  | 't'
  | 'menu'
  | 'saving'
  | 'creatingItem'
  | 'expandedGroups'
  | 'setExpandedGroups'
  | 'expandedMap'
  | 'setExpandedMap'
  | 'selectedKey'
  | 'setSelectedKey'
  | 'pinMap'
  | 'togglePin'
  | 'setExpandedForGroup'
  | 'moveGroup'
  | 'moveItem'
  | 'indentItem'
  | 'outdentItem'
  | 'openCreateNavItem'
  | 'selection'
  | 'openEditNavItem'
  | 'setDeleteOpen'
> & {
  splitWorkspace: boolean;
};

export function NavigationMenuEditorTreePanel({
  t,
  menu,
  saving,
  creatingItem,
  expandedGroups,
  setExpandedGroups,
  expandedMap,
  setExpandedMap,
  selectedKey,
  setSelectedKey,
  pinMap,
  togglePin,
  setExpandedForGroup,
  moveGroup,
  moveItem,
  indentItem,
  outdentItem,
  openCreateNavItem,
  selection,
  openEditNavItem,
  setDeleteOpen,
  splitWorkspace,
}: Props) {
  if (!menu) return null;

  return (
    <div className="nm-splitMain">
      <section className="nm-barEditor" aria-label={t('platform.navigationMenu.groups')}>
        <h2 className="nm-sectionTitle">{t('platform.navigationMenu.groups')}</h2>
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
              {t('platform.navigationMenu.editItem')}
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="destructive"
              disabled={saving || creatingItem}
              onClick={() => setDeleteOpen(true)}
            >
              {t('platform.navigationMenu.deleteItem')}
            </ZHBtn>
          </ZHInlineRowRight>
        </div>
      ) : null}
    </div>
  );
}
