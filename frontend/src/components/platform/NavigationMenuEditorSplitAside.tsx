import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHCardSection, ZHInlineRowRight } from '../zh/ZHLayout';
import { MenuPreview } from '../menu-builder/MenuPreview';
import type { NavigationMenuEditorPanelState } from './useNavigationMenuEditorPanel';

type Props = Pick<
  NavigationMenuEditorPanelState,
  | 't'
  | 'selection'
  | 'editDisplayLabel'
  | 'setEditDisplayLabel'
  | 'editRoutePath'
  | 'setEditRoutePath'
  | 'editModuleKey'
  | 'setEditModuleKey'
  | 'editPermissionKey'
  | 'setEditPermissionKey'
  | 'editFeatureId'
  | 'setEditFeatureId'
  | 'savingEdit'
  | 'saving'
  | 'creatingItem'
  | 'previewLayout'
  | 'setPreviewLayout'
  | 'previewMenuItems'
  | 'cancelSplitEdit'
  | 'handleEditNavItemSubmit'
  | 'setDeleteOpen'
>;

export function NavigationMenuEditorSplitAside({
  t,
  selection,
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
  saving,
  creatingItem,
  previewLayout,
  setPreviewLayout,
  previewMenuItems,
  cancelSplitEdit,
  handleEditNavItemSubmit,
  setDeleteOpen,
}: Props) {
  return (
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
  );
}
